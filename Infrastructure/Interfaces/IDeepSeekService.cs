using Core.Models;

namespace Infrastructure.Interfaces;

public interface IDeepSeekService
{
    /// <summary>
    /// Parses a free-text log for <paramref name="ownerId"/>: the bodyweight hint in the
    /// prompt comes from that user's history, and the parse cache is per user.
    /// </summary>
    Task<ParseWorkoutResult> ParseWorkoutAsync(string ownerId, string rawText, bool barbellWeightsArePerSide, CancellationToken cancellationToken = default);
}

public sealed record ParseWorkoutResult(WorkoutLog Workout, bool CacheHit, int CacheHitTokens, int CacheMissTokens, int InputTokens, int OutputTokens);