namespace Core.Models;

/// <summary>
/// One entry of the exercise catalog: the IDs the parser may map a log line to,
/// plus the display data hydrated onto parsed exercises. Stored in the
/// <c>exercise_definitions</c> table and edited from the UI.
/// </summary>
public sealed class ExerciseDefinition
{
    /// <summary>Stable kebab-case ID, e.g. "barbell-bench-press".</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ExerciseCategory Category { get; set; }

    public string MuscleGroup { get; set; } = string.Empty;

    /// <summary>True when the per-side barbell conversion applies.</summary>
    public bool Barbell { get; set; }

    /// <summary>
    /// Other names the user writes for this exercise (e.g. Polish shorthand).
    /// Listed in the parsing prompt next to the ID.
    /// </summary>
    public List<string> Aliases { get; set; } = [];
}
