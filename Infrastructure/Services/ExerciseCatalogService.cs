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

        var rows = await db.ExerciseDefinitions.AsNoTracking().ToListAsync(cancellationToken);

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
        var entity = await db.ExerciseDefinitions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
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
    }

    private static ExerciseDefinition ToModel(ExerciseDefinitionEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Category = entity.Category,
        MuscleGroup = entity.MuscleGroup,
        Barbell = entity.Barbell,
        Aliases = [.. entity.Aliases]
    };

    private static string ComputeVersion(IReadOnlyList<ExerciseDefinition> all)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all)));
        return Convert.ToHexString(bytes, 0, 8);
    }
}
