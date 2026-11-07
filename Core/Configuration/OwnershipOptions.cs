namespace Core.Configuration;

/// <summary>Who owns the data that existed before per-user data (#45). Section <c>Ownership</c>.</summary>
public sealed class OwnershipOptions
{
    /// <summary>
    /// OIDC <c>sub</c> or email of the user who gets every row the per-user migration
    /// marked as legacy. The first time that user is seen signed in, those rows move to
    /// their <c>sub</c>. Empty: legacy rows stay unclaimed (and invisible).
    /// </summary>
    public string LegacyOwner { get; set; } = string.Empty;
}
