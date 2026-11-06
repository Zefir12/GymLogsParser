using Core.Models;

namespace Infrastructure.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Per-day, per-exercise working-set aggregates for whole calendar weeks
    /// (Monday to Sunday) ending with the week that contains <paramref name="to"/>:
    /// at least 53 weeks, and enough to cover the last <paramref name="days"/> days.
    /// </summary>
    Task<DashboardData> GetAsync(DateOnly to, int days, CancellationToken cancellationToken = default);
}
