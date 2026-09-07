using Core.Models;

namespace Infrastructure.Interfaces;

public interface IDeepSeekService
{
    Task<ParseWorkoutResult> ParseWorkoutAsync(string rawText, bool barbellWeightsArePerSide, CancellationToken cancellationToken = default);
}

public sealed record ParseWorkoutResult(WorkoutLog Workout, bool CacheHit, int CacheHitTokens, int CacheMissTokens, int InputTokens, int OutputTokens);