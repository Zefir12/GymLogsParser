using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>#70: the Neck muscle group is gone from the list, the catalog and the targets.</summary>
public sealed class NeckRemovalTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    [Fact]
    public void NeckIsNoLongerAMuscleGroup()
    {
        Assert.DoesNotContain("Neck", MuscleGroups.All);
        Assert.Null(MuscleGroups.Canonical("neck"));
        Assert.False(MuscleGroups.IsMuscle("Neck"));
    }

    [Fact]
    public async Task MigrationsLeaveNoNeckData()
    {
        await using var db = Database.CreateDbContext();

        Assert.False(await db.MuscleTargets.AnyAsync(t => t.MuscleGroup == "Neck", Ct));
        Assert.False(await db.ExerciseDefinitions.AnyAsync(d => d.MuscleGroup == "Neck", Ct));
        Assert.False(await db.ExerciseDefinitions.AnyAsync(d => d.Muscles.Any(m => m.MuscleGroup == "Neck"), Ct));

        // The neck exercises seeded by #35 keep their IDs and now train Traps.
        var neck = await db.ExerciseDefinitions
            .Where(d => d.Id == "neck-curl" || d.Id == "neck-extension")
            .Select(d => d.MuscleGroup)
            .ToListAsync(Ct);
        Assert.Equal(2, neck.Count);
        Assert.All(neck, group => Assert.Equal("Traps", group));
    }
}
