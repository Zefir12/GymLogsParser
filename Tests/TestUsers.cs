using System.Security.Claims;
using Core.Models;
using Infrastructure;
using Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Tests;

/// <summary>Signed-in users as the controllers see them: an authenticated principal with an OIDC <c>sub</c>.</summary>
internal static class TestUsers
{
    public const string Alice = "sub-alice";
    public const string Bob = "sub-bob";

    public static ClaimsPrincipal Principal(string sub) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", sub) }, "test"));

    /// <summary>Runs the controller's actions as <paramref name="sub"/>.</summary>
    public static T As<T>(this T controller, string sub) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(sub) } };
        return controller;
    }
}

/// <summary>Rows written straight to the database, to set up a test.</summary>
internal static class TestData
{
    /// <summary>A strength workout with one exercise and one working set of 100 kg × 5.</summary>
    public static async Task<Guid> AddWorkoutAsync(
        AppDbContext db, string ownerId, DateOnly date, string exerciseId, string title, CancellationToken cancellationToken)
    {
        var workout = new Workout
        {
            OwnerId = ownerId,
            Date = date,
            Title = title,
            Exercises =
            [
                new Exercise
                {
                    ExerciseId = exerciseId,
                    Name = exerciseId,
                    MuscleGroup = "Chest",
                    Category = ExerciseCategory.Strength,
                    Sets = [new Set { SetNumber = 1, Weight = 100m, Reps = 5 }]
                }
            ]
        };

        db.Workouts.Add(workout);
        await db.SaveChangesAsync(cancellationToken);
        return workout.Id;
    }
}
