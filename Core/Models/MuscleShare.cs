namespace Core.Models;

/// <summary>
/// How much one muscle group counts when an exercise is done: a set of the exercise
/// adds <see cref="Share"/> sets to the group's weekly volume (e.g. bench press:
/// Chest 1, Triceps 0.5, Front Delts 0.5).
/// </summary>
public sealed class MuscleShare
{
    /// <summary>One of <see cref="MuscleGroups.All"/>.</summary>
    public string MuscleGroup { get; set; } = string.Empty;

    /// <summary>Between 0.05 and 1, in steps of 0.05. The primary group is always 1.</summary>
    public decimal Share { get; set; }
}
