using Core.Models;
using Infrastructure.Helpers;

namespace Infrastructure.Interfaces;

/// <summary>
/// Per-user bodyweight measurements, stored in <c>bodyweight_entries</c> (#45, #46).
/// Every method is scoped to <c>ownerId</c> (the caller's OIDC <c>sub</c>): one user
/// never reads or changes another user's rows.
/// </summary>
public interface IBodyweightService
{
    /// <summary>All measurements of <paramref name="ownerId"/>; empty when the user has none.</summary>
    Task<BodyweightHistory> GetHistoryAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>The measurements of <paramref name="ownerId"/>, newest first.</summary>
    Task<IReadOnlyList<BodyweightEntry>> ListAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a measurement the caller has validated. <see cref="BodyweightWriteResult.DateTaken"/>
    /// when the user already has one on that date.
    /// </summary>
    Task<BodyweightWriteResult> AddAsync(string ownerId, BodyweightEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user's measurement on <paramref name="date"/> with <paramref name="entry"/>,
    /// which may have another date (the measurement moves). <see cref="BodyweightWriteResult.NotFound"/>
    /// when the user has nothing on <paramref name="date"/>; <see cref="BodyweightWriteResult.DateTaken"/>
    /// when moving onto a date the user already has.
    /// </summary>
    Task<BodyweightWriteResult> UpdateAsync(string ownerId, DateOnly date, BodyweightEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's measurement on <paramref name="date"/>; false when there is none.</summary>
    Task<bool> DeleteAsync(string ownerId, DateOnly date, CancellationToken cancellationToken = default);
}
