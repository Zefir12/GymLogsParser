using Core.Models;

namespace Infrastructure.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Per-day, per-exercise working-set aggregates for the 53 calendar weeks
    /// (Monday to Sunday) ending with the week that contains <paramref name="to"/>.
    /// </summary>
    Task<DashboardData> GetAsync(DateOnly to, CancellationToken cancellationToken = default);
}
