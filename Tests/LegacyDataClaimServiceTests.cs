using Core.Configuration;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests;

/// <summary>
/// #45: the rows the PerUserData migration marked <c>legacy</c> (workouts, muscle targets,
/// bodyweight) move to the configured legacy owner, matched by sub or email, exactly once.
/// The migrations seed legacy muscle targets and bodyweight; each test adds a legacy workout.
/// </summary>
public sealed class LegacyDataClaimServiceTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private const string Legacy = LegacyDataClaimService.LegacyOwnerId;
    private const string OwnerEmail = "zefir@example.com";
    private static readonly DateOnly Day = new(2026, 11, 2);

    [Fact]
    public async Task ConfiguredSub_GetsEveryLegacyRow()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();

        await Service(TestUsers.Alice).ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct);

        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(Counts.None, await CountsAsync(Legacy));
    }

    [Fact]
    public async Task ConfiguredEmail_MatchesIgnoringCaseAndSpaces()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();

        await Service(OwnerEmail).ClaimIfLegacyOwnerAsync(TestUsers.Alice, " Zefir@Example.COM ", true, Ct);

        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(Counts.None, await CountsAsync(Legacy));
    }

    [Fact]
    public async Task UnverifiedEmail_DoesNotMatch()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();

        await Service(OwnerEmail).ClaimIfLegacyOwnerAsync(TestUsers.Alice, OwnerEmail, false, Ct);

        Assert.Equal(Counts.None, await CountsAsync(TestUsers.Alice));
        Assert.Equal(legacy, await CountsAsync(Legacy));
    }

    [Fact]
    public async Task OtherUsers_GetNothing()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();
        Guid bobs;
        await using (var db = Database.CreateDbContext())
        {
            bobs = await TestData.AddWorkoutAsync(db, TestUsers.Bob, Day, "squat", "Bob", Ct);
        }

        var service = Service(OwnerEmail);
        await service.ClaimIfLegacyOwnerAsync(TestUsers.Bob, "bob@example.com", true, Ct);
        await service.ClaimIfLegacyOwnerAsync(TestUsers.Bob, null, null, Ct);
        // The placeholder owner itself never claims.
        await service.ClaimIfLegacyOwnerAsync(Legacy, OwnerEmail, true, Ct);

        Assert.Equal(legacy, await CountsAsync(Legacy));
        Assert.Equal(new Counts(1, 0, 0), await CountsAsync(TestUsers.Bob));
        await using var check = Database.CreateDbContext();
        Assert.Equal(TestUsers.Bob, (await check.Workouts.SingleAsync(w => w.Id == bobs, Ct)).OwnerId);
    }

    [Fact]
    public async Task EmptySetting_ClaimsNothing()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();

        await Service("").ClaimIfLegacyOwnerAsync(TestUsers.Alice, OwnerEmail, true, Ct);

        Assert.Equal(legacy, await CountsAsync(Legacy));
    }

    [Fact]
    public async Task ClaimRunsOnlyOnce()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();
        var service = Service(TestUsers.Alice);
        await service.ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct);

        // A legacy row that shows up later is left alone: after the first claim the
        // service does no database work.
        await using (var db = Database.CreateDbContext())
        {
            await TestData.AddWorkoutAsync(db, Legacy, Day, "deadlift", "Late", Ct);
        }
        await service.ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct);

        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(new Counts(1, 0, 0), await CountsAsync(Legacy));
    }

    [Fact]
    public async Task UsersOwnTargetsAndBodyweight_WinOverLegacyRows()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();
        DateOnly weighed;
        await using (var db = Database.CreateDbContext())
        {
            weighed = await db.BodyweightEntries.Where(b => b.OwnerId == Legacy).MinAsync(b => b.Date, Ct);
            db.MuscleTargets.Add(new MuscleTargetEntity { OwnerId = TestUsers.Alice, MuscleGroup = "Chest", MinSets = 1, MaxSets = 2 });
            db.BodyweightEntries.Add(new BodyweightEntryEntity { OwnerId = TestUsers.Alice, Date = weighed, WeightKg = 99.5m });
            await db.SaveChangesAsync(Ct);
        }

        await Service(TestUsers.Alice).ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct);

        // Same totals: the legacy Chest target and the legacy entry for that date were dropped.
        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(Counts.None, await CountsAsync(Legacy));
        await using var check = Database.CreateDbContext();
        var chest = await check.MuscleTargets.SingleAsync(t => t.OwnerId == TestUsers.Alice && t.MuscleGroup == "Chest", Ct);
        Assert.Equal((1, 2), (chest.MinSets, chest.MaxSets));
        var entry = await check.BodyweightEntries.SingleAsync(b => b.OwnerId == TestUsers.Alice && b.Date == weighed, Ct);
        Assert.Equal(99.5m, entry.WeightKg);
    }

    [Fact]
    public async Task TwoClaimsAtOnce_MoveEverythingOnce()
    {
        var legacy = await SeedLegacyWorkoutAndCountAsync();
        var service = Service(TestUsers.Alice);

        await Task.WhenAll(
            service.ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct),
            service.ClaimIfLegacyOwnerAsync(TestUsers.Alice, OwnerEmail, true, Ct));

        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(Counts.None, await CountsAsync(Legacy));
    }

    [Fact]
    public async Task TwoInstancesClaimingAtOnce_MoveEverythingOnce()
    {
        // Like two API replicas: no shared lock, only the database transactions.
        var legacy = await SeedLegacyWorkoutAndCountAsync();

        await Task.WhenAll(
            Service(TestUsers.Alice).ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct),
            Service(TestUsers.Alice).ClaimIfLegacyOwnerAsync(TestUsers.Alice, null, null, Ct));

        Assert.Equal(legacy, await CountsAsync(TestUsers.Alice));
        Assert.Equal(Counts.None, await CountsAsync(Legacy));
    }

    private LegacyDataClaimService Service(string legacyOwner) =>
        new(Database, Options.Create(new OwnershipOptions { LegacyOwner = legacyOwner }), NullLogger<LegacyDataClaimService>.Instance);

    /// <summary>Adds one legacy workout and returns what the legacy owner has to claim.</summary>
    private async Task<Counts> SeedLegacyWorkoutAndCountAsync()
    {
        await using (var db = Database.CreateDbContext())
        {
            await TestData.AddWorkoutAsync(db, Legacy, Day, "bench-press", "Legacy", Ct);
        }

        var legacy = await CountsAsync(Legacy);
        Assert.Equal(1, legacy.Workouts);
        Assert.True(legacy.Targets > 0, "the migrations should seed legacy muscle targets");
        Assert.True(legacy.Bodyweight > 0, "the migrations should seed legacy bodyweight entries");
        return legacy;
    }

    private async Task<Counts> CountsAsync(string ownerId)
    {
        await using var db = Database.CreateDbContext();
        return new Counts(
            await db.Workouts.CountAsync(w => w.OwnerId == ownerId, Ct),
            await db.MuscleTargets.CountAsync(t => t.OwnerId == ownerId, Ct),
            await db.BodyweightEntries.CountAsync(b => b.OwnerId == ownerId, Ct));
    }

    private sealed record Counts(int Workouts, int Targets, int Bodyweight)
    {
        public static Counts None { get; } = new(0, 0, 0);
    }
}
