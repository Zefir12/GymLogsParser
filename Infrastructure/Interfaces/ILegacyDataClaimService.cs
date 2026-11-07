namespace Infrastructure.Interfaces;

/// <summary>
/// Hands the rows that existed before per-user data (#45, owner <c>legacy</c>) to the
/// configured legacy owner (<c>Ownership:LegacyOwner</c>) the first time that user is
/// seen signed in.
/// </summary>
public interface ILegacyDataClaimService
{
    /// <summary>
    /// If the signed-in user matches the legacy owner (by <paramref name="ownerId"/>, or by
    /// <paramref name="email"/> unless it is known to be unverified), moves every legacy
    /// workout, muscle target and bodyweight entry to <paramref name="ownerId"/>. Cheap
    /// for everyone else and after the first successful claim: no database access.
    /// </summary>
    Task ClaimIfLegacyOwnerAsync(string ownerId, string? email, bool? emailVerified, CancellationToken cancellationToken = default);
}
