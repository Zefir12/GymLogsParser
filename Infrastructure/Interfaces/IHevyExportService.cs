using Infrastructure.Helpers;

namespace Infrastructure.Interfaces;

/// <summary>Exports one user's workouts as a Hevy CSV (#80), importable by our Hevy import and embr's.</summary>
public interface IHevyExportService
{
    /// <summary>
    /// The owner's workouts dated <paramref name="from"/> to <paramref name="to"/>
    /// (inclusive, either open), oldest first, as a Hevy CSV. Workouts without a date
    /// are left out.
    /// </summary>
    Task<HevyCsvExport> ExportAsync(string ownerId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
