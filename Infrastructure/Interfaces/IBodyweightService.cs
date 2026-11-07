using Infrastructure.Helpers;

namespace Infrastructure.Interfaces;

/// <summary>Per-user bodyweight measurements, stored in <c>bodyweight_entries</c> (#45).</summary>
public interface IBodyweightService
{
    /// <summary>All measurements of <paramref name="ownerId"/>; empty when the user has none.</summary>
    Task<BodyweightHistory> GetHistoryAsync(string ownerId, CancellationToken cancellationToken = default);
}
