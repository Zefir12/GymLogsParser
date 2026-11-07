using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

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
}
