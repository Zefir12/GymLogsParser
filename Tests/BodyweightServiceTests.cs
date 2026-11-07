using Infrastructure.Entities;
using Infrastructure.Services;

namespace Tests;

/// <summary>#45: bodyweight history is per user; a user without measurements has none.</summary>
public sealed class BodyweightServiceTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    [Fact]
    public async Task History_ContainsOnlyTheUsersOwnMeasurements()
    {
        await using (var db = Database.CreateDbContext())
        {
            db.BodyweightEntries.AddRange(
                new BodyweightEntryEntity { OwnerId = TestUsers.Alice, Date = new DateOnly(2026, 1, 1), WeightKg = 80m },
                new BodyweightEntryEntity { OwnerId = TestUsers.Alice, Date = new DateOnly(2026, 1, 11), WeightKg = 90m },
                new BodyweightEntryEntity { OwnerId = TestUsers.Bob, Date = new DateOnly(2026, 1, 6), WeightKg = 60m });
            await db.SaveChangesAsync(Ct);
        }

        await using var context = Database.CreateDbContext();
        var service = new BodyweightService(context);
        var day = new DateOnly(2026, 1, 6);

        Assert.Equal(85d, (await service.GetHistoryAsync(TestUsers.Alice, Ct)).GetInterpolated(day));
        Assert.Equal(60d, (await service.GetHistoryAsync(TestUsers.Bob, Ct)).GetInterpolated(day));
        // The migration seeds zefir's history as legacy rows; nobody else sees them.
        Assert.Null((await service.GetHistoryAsync("sub-carol", Ct)).GetInterpolated(day));
    }
}
