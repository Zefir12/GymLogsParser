using System.Globalization;
using Core.Models;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Turns a Hevy CSV export into workouts (#77).
/// <list type="bullet">
/// <item>A workout is the rows sharing <c>title</c> + <c>start_time</c>; its date and
/// start/end time are the export's local times.</item>
/// <item>An exercise is a run of consecutive rows with one <c>exercise_title</c> (Hevy
/// writes an exercise's sets together; the same title twice in a workout stays two
/// exercises). Sets are numbered 1.. in file order.</item>
/// <item>Titles map to existing catalog exercises: the request's mapping wins, else an
/// automatic match on name or alias (<see cref="HevyCsv.MatchKeys"/>). The import never
/// creates catalog entries (catalog writes are admin-only and IDs are permanent);
/// sets of unmapped titles are skipped and counted.</item>
/// <item>A workout the user already has (same date, start time and title) is skipped,
/// so importing the same file twice adds nothing the second time.</item>
/// </list>
/// </summary>
public sealed class HevyImportService(AppDbContext db, IExerciseCatalogService catalog) : IHevyImportService
{
    public const int MaxReportedErrors = 200;

    public async Task<HevyImportOutcome> ImportAsync(
        string ownerId,
        string csv,
        IReadOnlyDictionary<string, string?> mappings,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        HevyCsvResult parsed;
        try
        {
            parsed = HevyCsv.Parse(csv);
        }
        catch (FormatException ex)
        {
            // Our own messages (HevyCsv/CsvReader), never framework text.
            return HevyImportOutcome.Fail(ex.Message);
        }

        var snapshot = await catalog.GetAsync(cancellationToken);
        var resolved = ResolveExercises(parsed.Rows, mappings, snapshot, out var mappingError);
        if (mappingError is not null) return HevyImportOutcome.Fail(mappingError);

        var summary = new HevyImportSummary
        {
            Imported = !dryRun,
            Exercises = resolved.Values.OrderByDescending(e => e.Sets).ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            Errors = parsed.Errors.Take(MaxReportedErrors).ToList(),
            ErrorCount = parsed.Errors.Count
        };

        var workouts = parsed.Rows
            .GroupBy(r => (r.Title, r.Start))
            .ToList();
        summary.WorkoutsInFile = workouts.Count;

        var existing = await ExistingKeysAsync(ownerId, workouts.Select(g => DateOnly.FromDateTime(g.Key.Start)), cancellationToken);

        var entities = new List<Workout>();
        foreach (var group in workouts)
        {
            var (title, start) = group.Key;
            var date = DateOnly.FromDateTime(start);
            var startTime = ToMinute(TimeOnly.FromDateTime(start));
            if (existing.Contains((date, startTime, title)))
            {
                summary.DuplicateWorkouts++;
                continue;
            }

            var workout = BuildWorkout(ownerId, group.ToList(), resolved, snapshot, summary);
            if (workout.Exercises.Count == 0) continue; // every set was unmapped

            entities.Add(workout);
            summary.Workouts++;
        }

        if (!dryRun && entities.Count > 0)
        {
            db.Workouts.AddRange(entities);
            await db.SaveChangesAsync(cancellationToken);
        }

        return HevyImportOutcome.Ok(summary);
    }

    /// <summary>Every distinct title with its set count and catalog ID, keyed by title (ordinal).</summary>
    private static Dictionary<string, HevyImportExercise> ResolveExercises(
        IReadOnlyList<HevyRow> rows,
        IReadOnlyDictionary<string, string?> mappings,
        ExerciseCatalogSnapshot snapshot,
        out string? error)
    {
        error = null;
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in snapshot.All)
        {
            // Names win over aliases when both give the same key.
            byKey.TryAdd(HevyCsv.NameKey(definition.Name), definition.Id);
        }

        foreach (var definition in snapshot.All)
        {
            foreach (var alias in definition.Aliases)
            {
                byKey.TryAdd(HevyCsv.NameKey(alias), definition.Id);
            }
        }

        var result = new Dictionary<string, HevyImportExercise>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (result.TryGetValue(row.ExerciseTitle, out var known))
            {
                known.Sets++;
                continue;
            }

            var exercise = new HevyImportExercise { Title = row.ExerciseTitle, Sets = 1 };
            if (mappings.TryGetValue(row.ExerciseTitle, out var chosen))
            {
                if (string.IsNullOrWhiteSpace(chosen))
                {
                    exercise.ExerciseId = null;
                }
                else if (snapshot.ById.TryGetValue(chosen, out var definition))
                {
                    exercise.ExerciseId = definition.Id;
                }
                else
                {
                    error = $"The mapping for \"{row.ExerciseTitle}\" names the catalog ID \"{chosen}\", which doesn't exist. Pick an exercise from the catalog or leave it unmapped.";
                    return result;
                }
            }
            else
            {
                exercise.ExerciseId = HevyCsv.MatchKeys(row.ExerciseTitle)
                    .Select(key => byKey.GetValueOrDefault(key))
                    .FirstOrDefault(id => id is not null);
                exercise.AutoMatched = exercise.ExerciseId is not null;
            }

            result.Add(row.ExerciseTitle, exercise);
        }

        return result;
    }

    /// <summary>(date, start time, title) of the owner's workouts on the given days.</summary>
    private async Task<HashSet<(DateOnly, TimeOnly, string)>> ExistingKeysAsync(
        string ownerId, IEnumerable<DateOnly> dates, CancellationToken cancellationToken)
    {
        var days = dates.Distinct().ToList();
        var rows = await db.Workouts
            .AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.Date != null && days.Contains(w.Date.Value)
                        && w.StartTime != null && w.Title != null)
            .Select(w => new { w.Date, w.StartTime, w.Title })
            .ToListAsync(cancellationToken);

        return rows
            .Select(w => (w.Date!.Value, ToMinute(w.StartTime!.Value), w.Title!))
            .ToHashSet();
    }

    private static Workout BuildWorkout(
        string ownerId,
        List<HevyRow> rows,
        Dictionary<string, HevyImportExercise> resolved,
        ExerciseCatalogSnapshot snapshot,
        HevyImportSummary summary)
    {
        var first = rows[0];
        var workout = new Workout
        {
            OwnerId = ownerId,
            Date = DateOnly.FromDateTime(first.Start),
            Title = first.Title,
            Notes = rows.Select(r => r.Description).FirstOrDefault(d => d is not null),
            StartTime = ToMinute(TimeOnly.FromDateTime(first.Start)),
            EndTime = first.End is { } end ? ToMinute(TimeOnly.FromDateTime(end)) : null
        };

        for (var i = 0; i < rows.Count;)
        {
            var title = rows[i].ExerciseTitle;
            var run = rows.Skip(i).TakeWhile(r => r.ExerciseTitle == title).ToList();
            i += run.Count;

            if (resolved[title].ExerciseId is not { } id || !snapshot.ById.TryGetValue(id, out var definition))
            {
                summary.UnmappedSets += run.Count;
                continue;
            }

            var exercise = new Exercise
            {
                ExerciseId = definition.Id,
                Name = definition.Name,
                MuscleGroup = definition.MuscleGroup,
                Notes = run.Select(r => r.ExerciseNotes).FirstOrDefault(n => n is not null),
                Category = definition.Category
            };

            if (definition.Category == ExerciseCategory.Cardio)
            {
                exercise.Cardio = run.Select(r => new Cardio
                {
                    Activity = definition.Name,
                    DurationSeconds = r.DurationSeconds,
                    DistanceKm = r.DistanceKm,
                    Notes = SetNotes(r, includeCardioFields: false)
                }).ToList();
            }
            else
            {
                exercise.Sets = run.Select((r, n) => new Set
                {
                    SetNumber = n + 1,
                    Weight = r.WeightKg,
                    Reps = r.Reps,
                    Rpe = r.Rpe,
                    Warmup = r.SetType == "warmup",
                    Notes = SetNotes(r, includeCardioFields: true)
                }).ToList();
            }

            summary.Sets += run.Count;
            workout.Exercises.Add(exercise);
        }

        return workout;
    }

    /// <summary>Hevy times have minutes only; stored times may carry seconds.</summary>
    private static TimeOnly ToMinute(TimeOnly time) => new(time.Hour, time.Minute);

    /// <summary>
    /// What a set row carries that has no column of its own: drop set / to failure, and on
    /// strength sets a duration or distance (planks, carries).
    /// </summary>
    private static string? SetNotes(HevyRow row, bool includeCardioFields)
    {
        var parts = new List<string>();
        if (row.SetType == "dropset") parts.Add("Drop set");
        if (row.SetType == "failure") parts.Add("To failure");
        if (includeCardioFields && row.DurationSeconds is { } seconds) parts.Add($"{seconds} s");
        if (includeCardioFields && row.DistanceKm is { } km) parts.Add($"{km.ToString(CultureInfo.InvariantCulture)} km");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
