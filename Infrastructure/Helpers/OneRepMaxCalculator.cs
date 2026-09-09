namespace Infrastructure.Helpers;

/// <summary>
/// Estimates one-rep-max equivalent from a submaximal weight/reps pair.
/// </summary>
public static class OneRepMaxCalculator
{
    /// <summary>
    /// Epley formula: 1RM = weight * (1 + reps / 30).
    /// Simple, widely used, tends to slightly overestimate at high rep counts.
    /// </summary>
    public static decimal Epley(decimal weight, int reps)
    {
        if (reps <= 0 || weight <= 0) return 0m;
        if (reps == 1) return weight;

        return weight * (1 + reps / 30m);
    }

    /// <summary>
    /// Brzycki formula: 1RM = weight / (1.0278 - 0.0278 * reps).
    /// Tends to be more conservative than Epley, especially beyond ~10 reps.
    /// Becomes unstable/negative past ~36 reps, so it's clamped defensively.
    /// </summary>
    public static decimal Brzycki(decimal weight, int reps)
    {
        if (reps <= 0 || weight <= 0) return 0m;
        if (reps == 1) return weight;

        var denominator = 1.0278m - 0.0278m * reps;
        if (denominator <= 0) return weight * (1 + reps / 30m); // fall back to Epley-ish

        return weight / denominator;
    }
}