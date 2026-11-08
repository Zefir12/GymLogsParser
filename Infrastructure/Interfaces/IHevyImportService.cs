using Core.Models;

namespace Infrastructure.Interfaces;

/// <summary>Imports workouts from a Hevy CSV export into one user's log (#77).</summary>
public interface IHevyImportService
{
    /// <summary>
    /// Parses <paramref name="csv"/>, maps exercise titles to the catalog and, unless
    /// <paramref name="dryRun"/>, saves the new workouts in one transaction. Returns the
    /// summary, or an English error for the whole request (not a Hevy file, a mapping
    /// to an unknown catalog ID) and saves nothing.
    /// </summary>
    Task<HevyImportOutcome> ImportAsync(
        string ownerId,
        string csv,
        IReadOnlyDictionary<string, string?> mappings,
        bool dryRun,
        CancellationToken cancellationToken = default);
}

/// <summary>Either a summary or a request error (for a 400).</summary>
public sealed record HevyImportOutcome(HevyImportSummary? Summary, string? Error)
{
    public static HevyImportOutcome Ok(HevyImportSummary summary) => new(summary, null);
    public static HevyImportOutcome Fail(string error) => new(null, error);
}
