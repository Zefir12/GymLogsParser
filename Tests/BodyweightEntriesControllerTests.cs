using Core.Models;
using GymLogsParser.Controllers;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>#46: each user lists, adds, edits and deletes only their own bodyweight measurements.</summary>
public sealed class BodyweightEntriesControllerTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);
    private static readonly DateOnly Jan5 = new(2026, 1, 5);
    private static readonly DateOnly Jan9 = new(2026, 1, 9);

    [Fact]
    public async Task AddEditMoveAndDelete_OwnMeasurements()
    {
        Assert.IsType<CreatedResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan1, 80.4m), Ct))).Result);
        Assert.IsType<CreatedResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan5, 81m), Ct))).Result);
        Assert.Equal(new[] { (Jan5, 81m), (Jan1, 80.4m) }, await ListAsync(TestUsers.Alice));

        // Change the weight in place.
        Assert.IsType<OkObjectResult>((await Act(TestUsers.Alice, c => c.Update("2026-01-05", Entry(Jan5, 81.25m), Ct))).Result);
        // Move a measurement to another free date.
        Assert.IsType<OkObjectResult>((await Act(TestUsers.Alice, c => c.Update("2026-01-01", Entry(Jan9, 79.9m), Ct))).Result);
        Assert.Equal(new[] { (Jan9, 79.9m), (Jan5, 81.25m) }, await ListAsync(TestUsers.Alice));

        Assert.IsType<NoContentResult>(await Act(TestUsers.Alice, c => c.Delete("2026-01-09", Ct)));
        Assert.Equal(new[] { (Jan5, 81.25m) }, await ListAsync(TestUsers.Alice));
    }

    [Fact]
    public async Task OtherUser_CannotSeeEditOrDeleteTheMeasurements()
    {
        await SeedAsync(TestUsers.Alice, Jan5, 80m);

        Assert.Empty(await ListAsync(TestUsers.Bob));
        Assert.IsType<NotFoundObjectResult>((await Act(TestUsers.Bob, c => c.Update("2026-01-05", Entry(Jan5, 60m), Ct))).Result);
        Assert.IsType<NotFoundObjectResult>((await Act(TestUsers.Bob, c => c.Update("2026-01-05", Entry(Jan9, 60m), Ct))).Result);
        Assert.IsType<NotFoundObjectResult>(await Act(TestUsers.Bob, c => c.Delete("2026-01-05", Ct)));

        // Bob's own measurement on the same day is his, not a conflict with Alice's.
        Assert.IsType<CreatedResult>((await Act(TestUsers.Bob, c => c.Create(Entry(Jan5, 60m), Ct))).Result);
        Assert.IsType<NoContentResult>(await Act(TestUsers.Bob, c => c.Delete("2026-01-05", Ct)));

        Assert.Equal(new[] { (Jan5, 80m) }, await ListAsync(TestUsers.Alice));
        Assert.Empty(await ListAsync(TestUsers.Bob));

        await using var db = Database.CreateDbContext();
        var alice = await db.BodyweightEntries.SingleAsync(b => b.OwnerId == TestUsers.Alice, Ct);
        Assert.Equal((Jan5, 80m), (alice.Date, alice.WeightKg));
    }

    [Fact]
    public async Task SecondMeasurementOnADate_IsAConflict()
    {
        await SeedAsync(TestUsers.Alice, Jan1, 80m);
        await SeedAsync(TestUsers.Alice, Jan5, 81m);

        Assert.IsType<ConflictObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan1, 82m), Ct))).Result);
        Assert.IsType<ConflictObjectResult>((await Act(TestUsers.Alice, c => c.Update("2026-01-01", Entry(Jan5, 82m), Ct))).Result);

        Assert.Equal(new[] { (Jan5, 81m), (Jan1, 80m) }, await ListAsync(TestUsers.Alice));
    }

    [Fact]
    public async Task InvalidInput_IsRejected()
    {
        var tooLate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(null, Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan1, 19.99m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan1, 400.01m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(Jan1, 80.123m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(tooLate, 80m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Create(Entry(default, 80m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>((await Act(TestUsers.Alice, c => c.Update("05.01.2026", Entry(Jan5, 80m), Ct))).Result);
        Assert.IsType<BadRequestObjectResult>(await Act(TestUsers.Alice, c => c.Delete("not-a-date", Ct)));

        Assert.Empty(await ListAsync(TestUsers.Alice));
    }

    [Fact]
    public async Task UnauthenticatedCaller_GetsUnauthorized()
    {
        await using var db = Database.CreateDbContext();
        var controller = new BodyweightEntriesController(new BodyweightService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        Assert.IsType<UnauthorizedResult>((await controller.List(Ct)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.Create(Entry(Jan1, 80m), Ct)).Result);
    }

    private static BodyweightEntry Entry(DateOnly date, decimal weightKg) => new() { Date = date, WeightKg = weightKg };

    /// <summary>Runs one action as <paramref name="user"/> on a fresh context, as one request would.</summary>
    private async Task<T> Act<T>(string user, Func<BodyweightEntriesController, Task<T>> action)
    {
        await using var db = Database.CreateDbContext();
        return await action(new BodyweightEntriesController(new BodyweightService(db)).As(user));
    }

    private async Task<(DateOnly, decimal)[]> ListAsync(string user)
    {
        var result = await Act(user, c => c.List(Ct));
        var entries = Assert.IsAssignableFrom<IReadOnlyList<BodyweightEntry>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        return entries.Select(e => (e.Date, e.WeightKg)).ToArray();
    }

    private async Task SeedAsync(string owner, DateOnly date, decimal weightKg)
    {
        await using var db = Database.CreateDbContext();
        db.BodyweightEntries.Add(new BodyweightEntryEntity { OwnerId = owner, Date = date, WeightKg = weightKg });
        await db.SaveChangesAsync(Ct);
    }
}
