using Core.Models;
using GymLogsParser.Extensions;
using Infrastructure;
using Infrastructure.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymLogsParser.Controllers;

/// <summary>
/// The signed-in user's workouts. Every query is filtered by the owner (OIDC <c>sub</c>),
/// so another user's workout IDs answer 404 (#45).
/// </summary>
[ApiController]
[Authorize]
[Route("api/workouts")]
public sealed class WorkoutsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Lightweight list for the workouts screen: one row per workout, newest first,
    /// with just enough aggregate info to render without loading every set.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkoutSummary>>> List(CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }

        var workouts = await db.Workouts
            .AsNoTracking()
            .Where(w => w.OwnerId == ownerId)
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Sets)
            .OrderByDescending(w => w.Date)
            .ToListAsync(cancellationToken);

        var summaries = workouts
            .Select(w => new WorkoutSummary
            {
                Id = w.Id,
                Date = w.Date,
                Title = w.Title,
                ExerciseCount = w.Exercises.Count,
                TotalSets = w.Exercises.Sum(e => e.Sets.Count),
                HasCardio = w.Exercises.Any(e => e.Category == ExerciseCategory.Cardio),
                MuscleGroups = w.Exercises
                    .Select(e => e.MuscleGroup)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToList()!
            })
            .ToList();

        return Ok(summaries);
    }

    /// <summary>Saves a new workout owned by the signed-in user.</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] WorkoutLog? workout, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (workout is null) {  return BadRequest(new { error = "Workout is required." }); }

        var entity = new Workout
        {
            OwnerId = ownerId,
            Date = workout.Date,
            Title = workout.Title,
            Notes = workout.Notes,
            StartTime = workout.StartTime,
            EndTime = workout.EndTime,

            Persons = workout.Persons
                .Select(name => new People
                {
                    Name = name
                })
                .ToList(),

            Exercises = workout.Exercises
                .Select(exercise => new Exercise()
                {
                    ExerciseId = exercise.ExerciseId,
                    Name = exercise.Name,
                    MuscleGroup = exercise.MuscleGroup,
                    Notes = exercise.Notes,
                    Category = exercise.Category,

                    Sets = exercise.Sets
                        .Select(set => new Set()
                        {
                            SetNumber = set.SetNumber,
                            Weight = set.Weight,
                            Reps = set.Reps,
                            Rir = set.Rir,
                            Rpe = set.Rpe,
                            Warmup = set.Warmup,
                            Notes = set.Notes
                        })
                        .ToList(),

                    Cardio = exercise.Cardio
                        .Select(cardio => new Cardio()
                        {
                            Activity = cardio.Activity,
                            DurationSeconds = cardio.DurationSeconds,
                            DistanceKm = cardio.DistanceKm,
                            SpeedKmh = cardio.SpeedKmh,
                            InclinePercent = cardio.InclinePercent,
                            Notes = cardio.Notes
                        })
                        .ToList()
                })
                .ToList()
        };

        db.Workouts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById),  new { id = entity.Id }, entity.Id);
    }

    /// <summary>One of the signed-in user's workouts; 404 for anyone else's.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkoutLog>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        var workout = await db.Workouts
            .AsNoTracking()
            .Include(x => x.Persons)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Cardio)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);

        if (workout is null) { return NotFound(); }

        return Ok(new WorkoutLog
        {
            Id = workout.Id,
            Date = workout.Date,
            Title = workout.Title,
            Notes = workout.Notes,
            StartTime = workout.StartTime,
            EndTime = workout.EndTime,

            Persons = workout.Persons
                .Select(x => x.Name)
                .ToList(),

            Exercises = workout.Exercises
                .Select(x => new WorkoutExercise
                {
                    ExerciseId = x.ExerciseId,
                    Name = x.Name,
                    MuscleGroup = x.MuscleGroup,
                    Notes = x.Notes,
                    Category = x.Category,

                    Sets = x.Sets
                        .OrderBy(s => s.SetNumber)
                        .Select(s => new WorkoutSet
                        {
                            SetNumber = s.SetNumber,
                            Weight = s.Weight,
                            Reps = s.Reps,
                            Rir = s.Rir,
                            Rpe = s.Rpe,
                            Warmup = s.Warmup,
                            Notes = s.Notes
                        })
                        .ToList(),

                    Cardio = x.Cardio
                        .Select(c => new CardioEntry
                        {
                            Activity = c.Activity,
                            DurationSeconds = c.DurationSeconds,
                            DistanceKm = c.DistanceKm,
                            SpeedKmh = c.SpeedKmh,
                            InclinePercent = c.InclinePercent,
                            Notes = c.Notes
                        })
                        .ToList()
                })
                .ToList()
        });
    }

    /// <summary>
    /// Full replace of a workout's editable content (metadata, persons, and every
    /// exercise/set/cardio row). The edit screen always sends the complete workout
    /// back, so we drop the old children and rebuild them rather than diffing.
    /// Only the owner can update it; 404 for anyone else.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] WorkoutLog? workout, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        if (workout is null) { return BadRequest(new { error = "Workout is required." }); }

        var entity = await db.Workouts
            .Include(x => x.Persons)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Cardio)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);

        if (entity is null) { return NotFound(); }

        entity.Date = workout.Date;
        entity.Title = workout.Title;
        entity.Notes = workout.Notes;
        entity.StartTime = workout.StartTime;
        entity.EndTime = workout.EndTime;

        // Clearing a fully-loaded, tracked collection and re-adding lets EF Core's
        // default orphan-delete behaviour remove the old rows on SaveChanges,
        // as long as the Person/Exercise/Set/Cardio FKs are required (non-nullable).
        // If they're configured as optional, swap this for explicit db.RemoveRange calls.
        entity.Persons.Clear();
        foreach (var name in workout.Persons)
        {
            entity.Persons.Add(new People { Name = name });
        }

        entity.Exercises.Clear();
        foreach (var exercise in workout.Exercises)
        {
            var exerciseEntity = new Exercise
            {
                ExerciseId = exercise.ExerciseId,
                Name = exercise.Name,
                MuscleGroup = exercise.MuscleGroup,
                Notes = exercise.Notes,
                Category = exercise.Category
            };

            foreach (var set in exercise.Sets)
            {
                exerciseEntity.Sets.Add(new Set
                {
                    SetNumber = set.SetNumber,
                    Weight = set.Weight,
                    Reps = set.Reps,
                    Rir = set.Rir,
                    Rpe = set.Rpe,
                    Warmup = set.Warmup,
                    Notes = set.Notes
                });
            }

            foreach (var cardio in exercise.Cardio)
            {
                exerciseEntity.Cardio.Add(new Cardio
                {
                    Activity = cardio.Activity,
                    DurationSeconds = cardio.DurationSeconds,
                    DistanceKm = cardio.DistanceKm,
                    SpeedKmh = cardio.SpeedKmh,
                    InclinePercent = cardio.InclinePercent,
                    Notes = cardio.Notes
                });
            }

            entity.Exercises.Add(exerciseEntity);
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes one of the signed-in user's workouts; 404 for anyone else's.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (User.OwnerId() is not { } ownerId) { return Unauthorized(); }
        var entity = await db.Workouts.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);
        if (entity is null) { return NotFound(); }

        db.Workouts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// One row for the workouts list screen. Deliberately light — no sets/exercise
/// detail — so the list loads fast even with a long training history.
/// </summary>
public sealed class WorkoutSummary
{
    public Guid Id { get; set; }
    public DateOnly? Date { get; set; }
    public string? Title { get; set; }
    public int ExerciseCount { get; set; }
    public int TotalSets { get; set; }
    public bool HasCardio { get; set; }
    public List<string> MuscleGroups { get; set; } = [];
}