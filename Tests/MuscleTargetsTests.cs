using Core.Models;
using GymLogsParser.Controllers;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>#45: every user has their own weekly target ranges.</summary>
public sealed class MuscleTargetsTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    [Fact]
    public async Task EachUserSeesOnlyTheirOwnRanges()
    {
        // The migrations seed default ranges as legacy rows; make the legacy Chest range
        // stand out so a leak would show.
        await using (var db = Database.CreateDbContext())
        {
            var legacyChest = await db.MuscleTargets.SingleAsync(t => t.OwnerId == "legacy" && t.MuscleGroup == "Chest", Ct);
            legacyChest.MinSets = 1;
            legacyChest.MaxSets = 2;
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = Database.CreateDbContext())
        {
            var result = await Controller(db, TestUsers.Alice)
                .Update([new MuscleTarget { MuscleGroup = "chest", MinSets = 5, MaxSets = 7 }], Ct);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        Assert.Equal((5, 7), await ChestAsync(TestUsers.Alice));
        Assert.Equal(MuscleTargetDefaults.For("Chest"), await ChestAsync(TestUsers.Bob));

        await using var check = Database.CreateDbContext();
        var legacy = await check.MuscleTargets.SingleAsync(t => t.OwnerId == "legacy" && t.MuscleGroup == "Chest", Ct);
        Assert.Equal((1, 2), (legacy.MinSets, legacy.MaxSets));
        Assert.False(await check.MuscleTargets.AnyAsync(t => t.OwnerId == TestUsers.Bob, Ct));
    }

    private async Task<(int Min, int Max)> ChestAsync(string user)
    {
        await using var db = Database.CreateDbContext();
        var result = await Controller(db, user).List(Ct);
        var targets = Assert.IsAssignableFrom<IReadOnlyList<MuscleTarget>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var chest = Assert.Single(targets, t => t.MuscleGroup == "Chest");
        return (chest.MinSets, chest.MaxSets);
    }

    private static MuscleTargetsController Controller(Infrastructure.AppDbContext db, string user) =>
        new MuscleTargetsController(new MuscleTargetService(db)).As(user);
}
