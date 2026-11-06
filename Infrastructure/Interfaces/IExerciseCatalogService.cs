using Core.Models;

namespace Infrastructure.Interfaces;

/// <summary>
/// The exercise catalog stored in <c>exercise_definitions</c>. Reads are served
/// from memory; every write drops the cached copy so the next read (and the next
/// parsing prompt) sees the change.
/// </summary>
public interface IExerciseCatalogService
{
    Task<ExerciseCatalogSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new definition. The caller has validated it and checked the ID is free.</summary>
    Task<ExerciseDefinition> CreateAsync(ExerciseDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Replaces name, category, muscle group, barbell and aliases. Null if the ID doesn't exist.</summary>
    Task<ExerciseDefinition?> UpdateAsync(string id, ExerciseDefinition definition, CancellationToken cancellationToken = default);
}

/// <summary>
/// An immutable copy of the whole catalog. <see cref="Version"/> changes whenever
/// any definition changes, so it can be part of cache keys that depend on the catalog.
/// </summary>
public sealed class ExerciseCatalogSnapshot(IReadOnlyList<ExerciseDefinition> all, string version)
{
    /// <summary>Sorted by muscle group, then name.</summary>
    public IReadOnlyList<ExerciseDefinition> All { get; } = all;

    public IReadOnlyDictionary<string, ExerciseDefinition> ById { get; } =
        all.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

    public string Version { get; } = version;
}
