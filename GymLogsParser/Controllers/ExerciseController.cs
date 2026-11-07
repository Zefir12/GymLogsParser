using Core.Models;
using GymLogsParser.Extensions;
using Infrastructure;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymLogsParser.Controllers;

[ApiController]
[Authorize]
[Route("api/exercises")]
public sealed class ExercisesController(AppDbContext db, IBodyweightService bodyweights) : ControllerBase
{
    /// <summary>
    /// Distinct list of strength exercises the signed-in user has logged at least once,
    /// used to populate an exercise picker on the frontend.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExerciseSummary>>> ListExercises(CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        var rows = await db.WorkoutExercises
            .AsNoTracking()
            .Where(e => e.Workout.OwnerId == ownerId && e.Category == ExerciseCategory.Strength)
            .Select(e => new { e.ExerciseId, e.Name, e.MuscleGroup })
            .ToListAsync(cancellationToken);

        var summaries = rows
            .GroupBy(e => e.ExerciseId)
            .Select(g => new ExerciseSummary
            {
                ExerciseId = g.Key,
                Name = g.First().Name,
                MuscleGroup = g.First().MuscleGroup
            })
            .OrderBy(e => e.Name)
            .ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Returns one data point per workout for the given exercise: max weight lifted,
    /// total volume (sum of weight * reps across working sets), and the estimated
    /// one-rep-max for that session, plus bodyweight and DOTS score (of the e1RM). Warm-up sets and sets missing weight/reps are
    /// excluded from all three metrics. Only the signed-in user's workouts, and their
    /// bodyweight history (no history: bodyweight and DOTS are null).
    /// </summary>
    [HttpGet("{exerciseId}/progress")]
    public async Task<ActionResult<List<ExerciseProgressPoint>>> GetProgress(
        string exerciseId,
        CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        var exerciseOccurrences = await db.WorkoutExercises
            .AsNoTracking()
            .Where(e => e.Workout.OwnerId == ownerId && e.ExerciseId == exerciseId)
            .Include(e => e.Sets)
            .Include(e => e.Workout)
            .ToListAsync(cancellationToken);

        if (exerciseOccurrences.Count == 0) { return NotFound(); }

        var bodyweightHistory = await bodyweights.GetHistoryAsync(ownerId, cancellationToken);

        var points = exerciseOccurrences
            // group by workout in case the same exercise appears more than once
            // in one session (e.g. supersets logged as separate entries)
            .GroupBy(e => e.WorkoutId)
            .Select(group =>
            {
                var workingSets = group
                    .SelectMany(e => e.Sets)
                    .Where(s => s.Warmup != true
                                && s.Weight.HasValue && s.Weight > 0
                                && s.Reps.HasValue && s.Reps > 0)
                    .ToList();

                var workout = group.First().Workout;
                double? bodyweight = workout.Date.HasValue
                    && bodyweightHistory.GetInterpolated(workout.Date.Value) is { } interpolated
                    ? Math.Round(interpolated, 1)
                    : null;

                if (workingSets.Count == 0)
                {
                    return new ExerciseProgressPoint
                    {
                        WorkoutId = group.Key,
                        Date = workout.Date,
                        MaxWeight = 0,
                        TotalVolume = 0,
                        EstimatedOneRepMax = 0,
                        Bodyweight = bodyweight,
                        Dots = 0
                    };
                }

                var estimatedOneRepMax = workingSets
                    .Max(s => OneRepMaxCalculator.Epley(s.Weight!.Value, s.Reps!.Value));

                return new ExerciseProgressPoint
                {
                    WorkoutId = group.Key,
                    Date = workout.Date,
                    MaxWeight = workingSets.Max(s => s.Weight!.Value),
                    TotalVolume = workingSets.Sum(s => s.Weight!.Value * s.Reps!.Value),
                    EstimatedOneRepMax = estimatedOneRepMax,
                    Bodyweight = bodyweight,
                    // DOTS of the session's estimated 1RM at that day's bodyweight
                    Dots = bodyweight.HasValue
                        ? Math.Round(DotsCalculator.Calculate(estimatedOneRepMax, bodyweight.Value), 1)
                        : null
                };
            })
            .OrderBy(p => p.Date)
            .ToList();

        return Ok(points);
    }
}

public sealed class ExerciseSummary
{
    public string ExerciseId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MuscleGroup { get; set; }
}

/// <summary>
/// A single chartable data point representing one workout session's performance
/// for a specific exercise.
/// </summary>
public sealed class ExerciseProgressPoint
{
    public Guid WorkoutId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal MaxWeight { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal EstimatedOneRepMax { get; set; }

    /// <summary>Bodyweight interpolated from history for the workout date (null if no date).</summary>
    public double? Bodyweight { get; set; }

    /// <summary>DOTS score of the estimated 1RM at that bodyweight (null if no date).</summary>
    public decimal? Dots { get; set; }
}