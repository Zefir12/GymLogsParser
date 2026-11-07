using Core.Models;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Services;

public sealed class BodyweightService(AppDbContext db) : IBodyweightService
{
    /// <inheritdoc />
    public async Task<BodyweightHistory> GetHistoryAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var rows = await db.BodyweightEntries
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .Select(b => new { b.Date, b.WeightKg })
            .ToListAsync(cancellationToken);

        return rows.Count == 0
            ? BodyweightHistory.Empty
            : new BodyweightHistory(rows.Select(r => (r.Date, (double)r.WeightKg)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BodyweightEntry>> ListAsync(string ownerId, CancellationToken cancellationToken = default) =>
        await db.BodyweightEntries
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.Date)
            .Select(b => new BodyweightEntry { Date = b.Date, WeightKg = b.WeightKg })
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<BodyweightWriteResult> AddAsync(string ownerId, BodyweightEntry entry, CancellationToken cancellationToken = default)
    {
        if (await db.BodyweightEntries.AnyAsync(b => b.OwnerId == ownerId && b.Date == entry.Date, cancellationToken))
        {
            return BodyweightWriteResult.DateTaken;
        }

        db.BodyweightEntries.Add(new BodyweightEntryEntity { OwnerId = ownerId, Date = entry.Date, WeightKg = entry.WeightKg });
        return await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BodyweightWriteResult> UpdateAsync(string ownerId, DateOnly date, BodyweightEntry entry, CancellationToken cancellationToken = default)
    {
        var existing = await db.BodyweightEntries
            .SingleOrDefaultAsync(b => b.OwnerId == ownerId && b.Date == date, cancellationToken);
        if (existing is null) return BodyweightWriteResult.NotFound;

        if (entry.Date == date)
        {
            existing.WeightKg = entry.WeightKg;
            return await SaveAsync(cancellationToken);
        }

        if (await db.BodyweightEntries.AnyAsync(b => b.OwnerId == ownerId && b.Date == entry.Date, cancellationToken))
        {
            return BodyweightWriteResult.DateTaken;
        }

        // The date is part of the key, so a move is a delete plus an insert (one transaction).
        db.BodyweightEntries.Remove(existing);
        db.BodyweightEntries.Add(new BodyweightEntryEntity { OwnerId = ownerId, Date = entry.Date, WeightKg = entry.WeightKg });
        return await SaveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string ownerId, DateOnly date, CancellationToken cancellationToken = default) =>
        await db.BodyweightEntries
            .Where(b => b.OwnerId == ownerId && b.Date == date)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Saves; a concurrent insert on the same date (unique key) is <see cref="BodyweightWriteResult.DateTaken"/>.</summary>
    private async Task<BodyweightWriteResult> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return BodyweightWriteResult.Saved;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return BodyweightWriteResult.DateTaken;
        }
    }
}
