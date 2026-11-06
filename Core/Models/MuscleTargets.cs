namespace Core.Models;

/// <summary>
/// The weekly target range of one muscle group, in working sets per week (sets of
/// secondary muscles count by share, see <see cref="MuscleShare"/>).
/// </summary>
public sealed class MuscleTarget
{
    /// <summary>One of <see cref="MuscleGroups.All"/> (never Cardio or Warm-up).</summary>
    public string MuscleGroup { get; set; } = string.Empty;

    public int MinSets { get; set; }

    public int MaxSets { get; set; }

    /// <summary>The range "reset to default" restores. Ignored on writes.</summary>
    public int DefaultMinSets { get; set; }

    /// <summary>The range "reset to default" restores. Ignored on writes.</summary>
    public int DefaultMaxSets { get; set; }
}

/// <summary>
/// Default weekly target ranges, from zefir's table on #35. Every muscle group in
/// <see cref="MuscleGroups.All"/> has one; Cardio and Warm-up have none. The
/// MuscleTargets migration seeds the same values (it keeps its own copy).
/// </summary>
public static class MuscleTargetDefaults
{
    /// <summary>Used for a muscle group without an entry below (none today).</summary>
    public static readonly (int Min, int Max) Fallback = (10, 20);

    public const int MaxSetsLimit = 100;

    private static readonly IReadOnlyDictionary<string, (int Min, int Max)> Ranges =
        new Dictionary<string, (int Min, int Max)>
        {
            ["Chest"] = (10, 20),
            ["Lats"] = (10, 20),
            ["Upper Back"] = (10, 20),
            ["Traps"] = (4, 12),
            ["Lower Back"] = (4, 10),
            ["Front Delts"] = (6, 12),
            ["Side Delts"] = (10, 20),
            ["Rear Delts"] = (8, 16),
            ["Biceps"] = (8, 16),
            ["Triceps"] = (8, 16),
            ["Forearms"] = (4, 10),
            ["Abs"] = (6, 12),
            ["Quadriceps"] = (10, 20),
            ["Hamstrings"] = (8, 16),
            ["Glutes"] = (8, 16),
            ["Calves"] = (8, 16),
            ["Adductors"] = (4, 10),
            ["Abductors"] = (4, 10),
            // Optional: 0 sets is still on target.
            ["Neck"] = (0, 8)
        };

    public static (int Min, int Max) For(string muscleGroup) =>
        Ranges.TryGetValue(muscleGroup, out var range) ? range : Fallback;
}
