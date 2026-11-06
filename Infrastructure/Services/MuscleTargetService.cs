using Core.Models;
using Infrastructure.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class MuscleTargetService(AppDbContext db) : IMuscleTargetService
{
    public async Task<IReadOnlyList<MuscleTarget>> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.MuscleTargets.AsNoTracking().ToListAsync(cancellationToken);
        return Merge(rows);
    }

    public async Task<IReadOnlyList<MuscleTarget>> UpdateAsync(IReadOnlyList<MuscleTarget> targets, CancellationToken cancellationToken = default)
    {
        var rows = await db.MuscleTargets.ToListAsync(cancellationToken);
        var byGroup = rows.ToDictionary(r => r.MuscleGroup, StringComparer.Ordinal);

        foreach (var target in targets)
        {
            if (!byGroup.TryGetValue(target.MuscleGroup, out var row))
            {
                row = new MuscleTargetEntity { MuscleGroup = target.MuscleGroup };
                db.MuscleTargets.Add(row);
                byGroup[row.MuscleGroup] = row;
                rows.Add(row);
            }

            row.MinSets = target.MinSets;
            row.MaxSets = target.MaxSets;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Merge(rows);
    }

    /// <summary>One target per muscle group in display order; rows of unknown groups are ignored.</summary>
    private static List<MuscleTarget> Merge(IEnumerable<MuscleTargetEntity> rows)
    {
        var byGroup = rows.ToDictionary(r => r.MuscleGroup, StringComparer.Ordinal);

        return MuscleGroups.All
            .Where(MuscleGroups.IsMuscle)
            .Select(group =>
            {
                var (defaultMin, defaultMax) = MuscleTargetDefaults.For(group);
                byGroup.TryGetValue(group, out var row);
                return new MuscleTarget
                {
                    MuscleGroup = group,
                    MinSets = row?.MinSets ?? defaultMin,
                    MaxSets = row?.MaxSets ?? defaultMax,
                    DefaultMinSets = defaultMin,
                    DefaultMaxSets = defaultMax
                };
            })
            .ToList();
    }
}
