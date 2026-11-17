using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Writes a user's workouts as a Hevy CSV export (#80) with <see cref="HevyCsvWriter"/>.
/// Exercise titles are the current catalog names (falling back to the name stored on
/// the workout), so a Hevy import matches them to the catalog automatically.
/// </summary>
public sealed class HevyExportService(AppDbContext db, IExerciseCatalogService catalog) : IHevyExportService
{
    public async Task<HevyCsvExport> ExportAsync(string ownerId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = db.Workouts
            .AsNoTracking()
            .Where(w => w.OwnerId == ownerId && w.Date != null);
        if (from is { } first) query = query.Where(w => w.Date >= first);
        if (to is { } last) query = query.Where(w => w.Date <= last);

        var workouts = await query
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Include(w => w.Exercises).ThenInclude(e => e.Cardio)
            .OrderBy(w => w.Date).ThenBy(w => w.StartTime).ThenBy(w => w.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var snapshot = await catalog.GetAsync(cancellationToken);
        string Title(Exercise exercise) =>
            snapshot.ById.TryGetValue(exercise.ExerciseId, out var definition) && !string.IsNullOrWhiteSpace(definition.Name)
                ? definition.Name
                : exercise.Name;

        return HevyCsvWriter.Write(workouts, Title);
    }
}
