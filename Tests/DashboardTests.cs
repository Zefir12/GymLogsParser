using Core.Models;
using GymLogsParser.Controllers;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Tests;

/// <summary>#45: the dashboard counts only the caller's workouts.</summary>
public sealed class DashboardTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    [Fact]
    public async Task Dashboard_CountsOnlyTheCallersWorkouts()
    {
        Guid alices, bobs;
        await using (var db = Database.CreateDbContext())
        {
            alices = await TestData.AddWorkoutAsync(db, TestUsers.Alice, Day, "bench-press", "Push", Ct);
            bobs = await TestData.AddWorkoutAsync(db, TestUsers.Bob, Day, "squat", "Legs", Ct);
            await TestData.AddWorkoutAsync(db, "legacy", Day, "deadlift", "Pull", Ct);
        }

        var alice = await GetAsync(TestUsers.Alice);
        var aliceDay = Assert.Single(alice.Days);
        Assert.Equal("Push", aliceDay.Title);
        var aliceEntry = Assert.Single(aliceDay.Entries);
        Assert.Equal("bench-press", aliceEntry.ExerciseId);
        Assert.Equal(alices, Assert.Single(aliceEntry.WorkoutIds));
        Assert.Equal("bench-press", Assert.Single(alice.Exercises).ExerciseId);

        var bob = await GetAsync(TestUsers.Bob);
        var bobEntry = Assert.Single(Assert.Single(bob.Days).Entries);
        Assert.Equal("squat", bobEntry.ExerciseId);
        Assert.Equal(bobs, Assert.Single(bobEntry.WorkoutIds));

        Assert.Empty((await GetAsync("sub-carol")).Days);
    }

    private async Task<DashboardData> GetAsync(string user)
    {
        await using var db = Database.CreateDbContext();
        var controller = new DashboardController(new DashboardService(db)).As(user);
        var result = await controller.Get(new DateOnly(2026, 11, 7), null, Ct);
        return Assert.IsType<DashboardData>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
