namespace Core.Models;

/// <summary>
/// The one flat list of body parts an exercise can train (no parent categories
/// such as "Legs" or "Shoulders"). Stored as plain strings on catalog entries
/// and logged exercises. Keep in sync with MUSCLE_GROUPS in the UI
/// (UI/gymlogsparser/src/utils/muscleGroups.ts).
/// </summary>
public static class MuscleGroups
{
    public const string Cardio = "Cardio";

    /// <summary>Warm-up and mobility drills: kept in the catalog, left out of training volume.</summary>
    public const string WarmUp = "Warm-up";

    /// <summary>Display order: upper body, arms, core, lower body, neck, then Cardio and Warm-up.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "Chest",
        "Lats",
        "Upper Back",
        "Traps",
        "Lower Back",
        "Front Delts",
        "Side Delts",
        "Rear Delts",
        "Biceps",
        "Triceps",
        "Forearms",
        "Abs",
        "Quadriceps",
        "Hamstrings",
        "Glutes",
        "Calves",
        "Adductors",
        "Abductors",
        "Neck",
        Cardio,
        WarmUp
    ];

    /// <summary>
    /// Groups an exercise can count towards as a secondary muscle: every real muscle
    /// group, so not Cardio or Warm-up.
    /// </summary>
    public static bool IsMuscle(string group) => group != Cardio && group != WarmUp && All.Contains(group);

    /// <summary>The canonical spelling of <paramref name="value"/> (case-insensitive), or null if it is not a muscle group.</summary>
    public static string? Canonical(string? value) =>
        All.FirstOrDefault(g => string.Equals(g, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
