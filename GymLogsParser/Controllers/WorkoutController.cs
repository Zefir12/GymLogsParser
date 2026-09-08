using Core.Models;
using Infrastructure;
using Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymLogsParser.Controllers;

[ApiController]
[Route("api/workouts")]
public sealed class WorkoutsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] WorkoutLog? workout, CancellationToken cancellationToken)
    {
        if (workout is null) {  return BadRequest(new { error = "Workout is required." }); }

        var entity = new Workout
        {
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkoutLog>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var workout = await db.Workouts
            .AsNoTracking()
            .Include(x => x.Persons)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Cardio)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (workout is null) { return NotFound(); }

        return Ok(new WorkoutLog
        {
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
}