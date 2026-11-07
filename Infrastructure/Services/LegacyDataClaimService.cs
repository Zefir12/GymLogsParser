using Core.Configuration;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
/// Singleton: remembers that the claim ran, so after that it costs nothing per request.
/// Claims are serialized, and each one is a single transaction; running it again only
/// moves rows that are still legacy.
/// </summary>
public sealed class LegacyDataClaimService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<OwnershipOptions> options,
    ILogger<LegacyDataClaimService> logger) : ILegacyDataClaimService
{
    /// <summary>Owner of the rows that existed before #45; set by the PerUserData migration.</summary>
    public const string LegacyOwnerId = "legacy";

    private readonly string _legacyOwner = (options.Value.LegacyOwner ?? string.Empty).Trim();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _claimed;

    /// <inheritdoc />
    public async Task ClaimIfLegacyOwnerAsync(string ownerId, string? email, bool? emailVerified, CancellationToken cancellationToken = default)
    {
        if (_claimed || _legacyOwner.Length == 0 || ownerId == LegacyOwnerId) return;

        var matches = string.Equals(ownerId, _legacyOwner, StringComparison.Ordinal)
                      || (emailVerified != false
                          && !string.IsNullOrWhiteSpace(email)
                          && string.Equals(email.Trim(), _legacyOwner, StringComparison.OrdinalIgnoreCase));
        if (!matches) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_claimed) return;

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var workouts = await db.Database.ExecuteSqlAsync(
                $"""UPDATE workouts SET "OwnerId" = {ownerId} WHERE "OwnerId" = {LegacyOwnerId}""",
                cancellationToken);

            // Rows the user already has win; the legacy duplicates are dropped.
            var targets = await db.Database.ExecuteSqlAsync(
                $"""
                 UPDATE muscle_targets t SET "OwnerId" = {ownerId}
                 WHERE t."OwnerId" = {LegacyOwnerId}
                   AND NOT EXISTS (SELECT 1 FROM muscle_targets o WHERE o."OwnerId" = {ownerId} AND o."MuscleGroup" = t."MuscleGroup")
                 """,
                cancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"""DELETE FROM muscle_targets WHERE "OwnerId" = {LegacyOwnerId}""",
                cancellationToken);

            var bodyweight = await db.Database.ExecuteSqlAsync(
                $"""
                 UPDATE bodyweight_entries b SET "OwnerId" = {ownerId}
                 WHERE b."OwnerId" = {LegacyOwnerId}
                   AND NOT EXISTS (SELECT 1 FROM bodyweight_entries o WHERE o."OwnerId" = {ownerId} AND o."Date" = b."Date")
                 """,
                cancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"""DELETE FROM bodyweight_entries WHERE "OwnerId" = {LegacyOwnerId}""",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _claimed = true;

            if (workouts + targets + bodyweight > 0)
            {
                logger.LogInformation(
                    "Legacy data claimed by the configured legacy owner: {Workouts} workouts, {Targets} muscle targets, {Bodyweight} bodyweight entries.",
                    workouts, targets, bodyweight);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
