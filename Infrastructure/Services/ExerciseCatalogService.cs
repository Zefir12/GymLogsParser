using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Models;
using Infrastructure.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public sealed class ExerciseCatalogService(AppDbContext db, IMemoryCache cache) : IExerciseCatalogService
{
    private const string CacheKey = "exercise-catalog";

    public async Task<ExerciseCatalogSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<ExerciseCatalogSnapshot>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var rows = await db.ExerciseDefinitions
            .AsNoTracking()
            .Include(e => e.Muscles)
            .ToListAsync(cancellationToken);

        // Sort in memory so the order (and so the prompt text and Version) doesn't
        // depend on the database collation.
        var all = rows
            .Select(ToModel)
            .OrderBy(e => e.MuscleGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();

        var snapshot = new ExerciseCatalogSnapshot(all, ComputeVersion(all));
        cache.Set(CacheKey, snapshot);
        return snapshot;
    }

    public async Task<ExerciseDefinition> CreateAsync(ExerciseDefinition definition, CancellationToken cancellationToken = default)
    {
        var entity = new ExerciseDefinitionEntity { Id = definition.Id };
        Apply(entity, definition);

        db.ExerciseDefinitions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);

        return ToModel(entity);
    }

    public async Task<ExerciseDefinition?> UpdateAsync(string id, ExerciseDefinition definition, CancellationToken cancellationToken = default)
    {
        var entity = await db.ExerciseDefinitions
            .Include(e => e.Muscles)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        Apply(entity, definition);
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);

        return ToModel(entity);
    }

    private static void Apply(ExerciseDefinitionEntity entity, ExerciseDefinition definition)
    {
        entity.Name = definition.Name;
        entity.Category = definition.Category;
        entity.MuscleGroup = definition.MuscleGroup;
        entity.Barbell = definition.Barbell;
        // A new list, so EF sees the primitive collection as changed.
        entity.Aliases = [.. definition.Aliases];
        ApplyMuscles(entity, definition.Muscles);
    }

    /// <summary>
    /// Makes the stored secondary muscles match <paramref name="muscles"/> (minus the
    /// primary, which is implied), updating rows in place so no key is deleted and
    /// re-added in the same save.
    /// </summary>
    private static void ApplyMuscles(ExerciseDefinitionEntity entity, IEnumerable<MuscleShare> muscles)
    {
        var wanted = muscles
            .Where(m => !string.Equals(m.MuscleGroup, entity.MuscleGroup, StringComparison.Ordinal))
            .DistinctBy(m => m.MuscleGroup, StringComparer.Ordinal)
            .ToDictionary(m => m.MuscleGroup, m => m.Share, StringComparer.Ordinal);

        entity.Muscles.RemoveAll(m => !wanted.ContainsKey(m.MuscleGroup));

        foreach (var (group, share) in wanted)
        {
            var existing = entity.Muscles.FirstOrDefault(m => m.MuscleGroup == group);
            if (existing is not null)
            {
                existing.Share = share;
            }
            else
            {
                entity.Muscles.Add(new ExerciseDefinitionMuscleEntity
                {
                    ExerciseDefinitionId = entity.Id,
                    MuscleGroup = group,
                    Share = share
                });
            }
        }
    }

    private static ExerciseDefinition ToModel(ExerciseDefinitionEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Category = entity.Category,
        MuscleGroup = entity.MuscleGroup,
        Barbell = entity.Barbell,
        Aliases = [.. entity.Aliases],
        Muscles = ToMuscles(entity)
    };

    /// <summary>Primary at share 1 first, then the secondary groups by share (highest first) and list order.</summary>
    private static List<MuscleShare> ToMuscles(ExerciseDefinitionEntity entity) =>
    [
        new MuscleShare { MuscleGroup = entity.MuscleGroup, Share = 1m },
        .. entity.Muscles
            .Where(m => m.MuscleGroup != entity.MuscleGroup)
            .OrderByDescending(m => m.Share)
            .ThenBy(m => GroupRank(m.MuscleGroup))
            .ThenBy(m => m.MuscleGroup, StringComparer.Ordinal)
            .Select(m => new MuscleShare { MuscleGroup = m.MuscleGroup, Share = m.Share })
    ];

    private static int GroupRank(string group)
    {
        for (var i = 0; i < MuscleGroups.All.Count; i++)
        {
            if (MuscleGroups.All[i] == group) return i;
        }

        return int.MaxValue;
    }

    private static string ComputeVersion(IReadOnlyList<ExerciseDefinition> all)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all)));
        return Convert.ToHexString(bytes, 0, 8);
    }
}
