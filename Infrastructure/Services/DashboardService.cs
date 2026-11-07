using Core.Models;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DashboardService(AppDbContext db) : IDashboardService
{
    /// <summary>53 calendar weeks: the heatmap's width, and enough for the 1-year range.</summary>
    private const int MinWeeks = 53;

    /// <summary>
    /// Exercises in this muscle group are warm-up activities, not training volume.
    /// </summary>
    private const string WarmupMuscleGroup = MuscleGroups.WarmUp;

    public async Task<DashboardData> GetAsync(string ownerId, DateOnly to, int days, CancellationToken cancellationToken = default)
    {
        // Enough whole weeks that the first one starts on or before to - days + 1,
        // whatever weekday `to` is.
        var weeks = Math.Max(MinWeeks, (days - 1 + 6) / 7 + 1);
        var mondayOffset = ((int)to.DayOfWeek + 6) % 7;
        var from = to.AddDays(-mondayOffset - (weeks - 1) * 7);

        // Working strength sets. Sets before the window are only needed as the baseline
        // for PR detection, so only those with weight and reps are loaded.
        var sets = await db.WorkoutSets
            .AsNoTracking()
            .Where(s => s.WorkoutExercise.Workout.OwnerId == ownerId
                        && s.Warmup != true
                        && s.WorkoutExercise.Category == ExerciseCategory.Strength
                        && s.WorkoutExercise.Workout.Date != null
                        && s.WorkoutExercise.Workout.Date <= to
                        && (s.WorkoutExercise.Workout.Date >= from
                            || (s.Weight > 0 && s.Reps > 0)))
            .Select(s => new
            {
                Date = s.WorkoutExercise.Workout.Date!.Value,
                s.WorkoutExercise.WorkoutId,
                s.WorkoutExercise.ExerciseId,
                s.Weight,
                s.Reps
            })
            .ToListAsync(cancellationToken);

        var cardio = await db.CardioEntries
            .AsNoTracking()
            .Where(c => c.WorkoutExercise.Workout.OwnerId == ownerId
                        && c.WorkoutExercise.Workout.Date != null
                        && c.WorkoutExercise.Workout.Date >= from
                        && c.WorkoutExercise.Workout.Date <= to)
            .Select(c => new
            {
                Date = c.WorkoutExercise.Workout.Date!.Value,
                c.WorkoutExercise.WorkoutId,
                c.WorkoutExercise.ExerciseId,
                c.DurationSeconds
            })
            .ToListAsync(cancellationToken);

        var exerciseRows = await db.WorkoutExercises
            .AsNoTracking()
            .Where(e => e.Workout.OwnerId == ownerId && e.Workout.Date != null && e.Workout.Date >= from && e.Workout.Date <= to)
            .OrderByDescending(e => e.Workout.Date)
            .Select(e => new { e.ExerciseId, e.Name, e.MuscleGroup, e.Category })
            .ToListAsync(cancellationToken);

        var titles = await db.Workouts
            .AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.Date != null && w.Date >= from && w.Date <= to)
            .OrderBy(w => w.StartTime)
            .Select(w => new { Date = w.Date!.Value, w.Title })
            .ToListAsync(cancellationToken);

        // Newest name/muscle group wins if an exercise was logged differently over time.
        var exercises = exerciseRows
            .Where(e => e.MuscleGroup != WarmupMuscleGroup)
            .GroupBy(e => e.ExerciseId)
            .Select(g => g.First())
            .ToDictionary(e => e.ExerciseId, e => new DashboardExercise
            {
                ExerciseId = e.ExerciseId,
                Name = e.Name,
                MuscleGroup = e.MuscleGroup,
                Category = e.Category
            });

        var entries = new Dictionary<(DateOnly Date, string ExerciseId), DashboardDayEntry>();

        // Strength: walk each exercise's sessions in date order so PRs compare against
        // the best of every earlier session, including those before the window.
        foreach (var exercise in sets.GroupBy(s => s.ExerciseId))
        {
            var best = 0m;
            var hadSession = false;

            foreach (var day in exercise.GroupBy(s => s.Date).OrderBy(g => g.Key))
            {
                var weighted = day
                    .Where(s => s.Weight > 0 && s.Reps > 0)
                    .Select(s => (Weight: s.Weight!.Value, Reps: s.Reps!.Value))
                    .ToList();

                var e1Rm = weighted.Count > 0
                    ? weighted.Max(s => OneRepMaxCalculator.Epley(s.Weight, s.Reps))
                    : 0m;
                var isPr = hadSession && e1Rm > best;

                if (e1Rm > 0)
                {
                    best = Math.Max(best, e1Rm);
                    hadSession = true;
                }

                if (day.Key < from || !exercises.ContainsKey(exercise.Key)) { continue; }

                entries[(day.Key, exercise.Key)] = new DashboardDayEntry
                {
                    ExerciseId = exercise.Key,
                    WorkoutIds = day.Select(s => s.WorkoutId).Distinct().ToList(),
                    Sets = day.Count(),
                    TopSet = weighted.Count > 0 ? weighted.Max(s => s.Weight) : 0m,
                    EstimatedOneRepMax = Math.Round(e1Rm, 2),
                    Volume = weighted.Sum(s => s.Weight * s.Reps),
                    Pr = isPr
                };
            }
        }

        foreach (var group in cardio.GroupBy(c => (c.Date, c.ExerciseId)))
        {
            if (!exercises.ContainsKey(group.Key.ExerciseId)) { continue; }

            entries[group.Key] = new DashboardDayEntry
            {
                ExerciseId = group.Key.ExerciseId,
                WorkoutIds = group.Select(c => c.WorkoutId).Distinct().ToList(),
                Sets = group.Count(),
                Minutes = Math.Round(group.Sum(c => c.DurationSeconds ?? 0) / 60m, 1)
            };
        }

        var titlesByDate = titles
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.Date)
            .ToDictionary(g => g.Key, g => string.Join(" + ", g.Select(t => t.Title).Distinct()));

        var usedExerciseIds = entries.Keys.Select(k => k.ExerciseId).ToHashSet();

        return new DashboardData
        {
            From = from,
            To = to,
            Exercises = exercises.Values
                .Where(e => usedExerciseIds.Contains(e.ExerciseId))
                .OrderBy(e => e.Name)
                .ToList(),
            Days = entries
                .GroupBy(kv => kv.Key.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DashboardDay
                {
                    Date = g.Key,
                    Title = titlesByDate.GetValueOrDefault(g.Key),
                    Entries = g.Select(kv => kv.Value).OrderBy(e => e.ExerciseId).ToList()
                })
                .ToList()
        };
    }
}
