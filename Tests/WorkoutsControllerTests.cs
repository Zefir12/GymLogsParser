using System.Security.Claims;
using Core.Models;
using GymLogsParser.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>#45: workouts belong to one user; anyone else's workout ID answers 404.</summary>
public sealed class WorkoutsControllerTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    [Fact]
    public async Task List_ReturnsOnlyTheCallersWorkouts()
    {
        Guid alices;
        await using (var db = Database.CreateDbContext())
        {
            alices = await TestData.AddWorkoutAsync(db, TestUsers.Alice, Day, "bench-press", "Alice", Ct);
            await TestData.AddWorkoutAsync(db, TestUsers.Bob, Day, "squat", "Bob", Ct);
            await TestData.AddWorkoutAsync(db, "legacy", Day, "deadlift", "Legacy", Ct);
        }

        await using var context = Database.CreateDbContext();
        var result = await new WorkoutsController(context).As(TestUsers.Alice).List(Ct);

        var list = Assert.IsType<List<WorkoutSummary>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(alices, Assert.Single(list).Id);
    }

    [Fact]
    public async Task GetById_OwnWorkout_IsReturned_OtherUsersWorkout_IsNotFound()
    {
        Guid alices;
        await using (var db = Database.CreateDbContext())
        {
            alices = await TestData.AddWorkoutAsync(db, TestUsers.Alice, Day, "bench-press", "Alice", Ct);
        }

        await using var context = Database.CreateDbContext();

        var own = await new WorkoutsController(context).As(TestUsers.Alice).GetById(alices, Ct);
        var workout = Assert.IsType<WorkoutLog>(Assert.IsType<OkObjectResult>(own.Result).Value);
        Assert.Equal("Alice", workout.Title);

        var other = await new WorkoutsController(context).As(TestUsers.Bob).GetById(alices, Ct);
        Assert.IsType<NotFoundResult>(other.Result);
    }

    [Fact]
    public async Task Update_OtherUsersWorkout_IsNotFound_AndUnchanged()
    {
        Guid alices;
        await using (var db = Database.CreateDbContext())
        {
            alices = await TestData.AddWorkoutAsync(db, TestUsers.Alice, Day, "bench-press", "Alice", Ct);
        }

        await using (var context = Database.CreateDbContext())
        {
            var result = await new WorkoutsController(context).As(TestUsers.Bob)
                .Update(alices, new WorkoutLog { Date = Day, Title = "Taken over" }, Ct);
            Assert.IsType<NotFoundResult>(result);
        }

        await using var check = Database.CreateDbContext();
        var stored = await check.Workouts.Include(w => w.Exercises).SingleAsync(w => w.Id == alices, Ct);
        Assert.Equal(TestUsers.Alice, stored.OwnerId);
        Assert.Equal("Alice", stored.Title);
        Assert.Single(stored.Exercises);
    }

    [Fact]
    public async Task Delete_OtherUsersWorkout_IsNotFound_AndKept()
    {
        Guid alices;
        await using (var db = Database.CreateDbContext())
        {
            alices = await TestData.AddWorkoutAsync(db, TestUsers.Alice, Day, "bench-press", "Alice", Ct);
        }

        await using (var context = Database.CreateDbContext())
        {
            var result = await new WorkoutsController(context).As(TestUsers.Bob).Delete(alices, Ct);
            Assert.IsType<NotFoundResult>(result);
        }

        await using var check = Database.CreateDbContext();
        Assert.True(await check.Workouts.AnyAsync(w => w.Id == alices, Ct));
    }

    [Fact]
    public async Task Create_StoresTheCallerAsOwner()
    {
        Guid id;
        await using (var context = Database.CreateDbContext())
        {
            var result = await new WorkoutsController(context).As(TestUsers.Bob)
                .Create(new WorkoutLog { Date = Day, Title = "Legs" }, Ct);
            id = Assert.IsType<Guid>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        }

        await using var check = Database.CreateDbContext();
        var stored = await check.Workouts.SingleAsync(w => w.Id == id, Ct);
        Assert.Equal(TestUsers.Bob, stored.OwnerId);
    }

    [Fact]
    public async Task List_WithoutSub_IsUnauthorized()
    {
        await using var context = Database.CreateDbContext();
        var controller = new WorkoutsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "test")) }
            }
        };

        var result = await controller.List(Ct);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
