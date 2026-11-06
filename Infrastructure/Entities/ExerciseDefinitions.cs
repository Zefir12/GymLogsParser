using Core.Models;

namespace Infrastructure.Entities;

/// <summary>
/// A catalog exercise (not a logged one: see <see cref="Exercise"/>).
/// </summary>
public class ExerciseDefinitionEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExerciseCategory Category { get; set; }

    /// <summary>The primary muscle group (share 1).</summary>
    public string MuscleGroup { get; set; } = string.Empty;

    public bool Barbell { get; set; }
    public List<string> Aliases { get; set; } = [];

    /// <summary>Secondary muscle groups with their share. Never contains <see cref="MuscleGroup"/>.</summary>
    public List<ExerciseDefinitionMuscleEntity> Muscles { get; set; } = [];
}

/// <summary>
/// A secondary muscle group of a catalog exercise: each set of the exercise counts
/// <see cref="Share"/> sets towards <see cref="MuscleGroup"/>.
/// </summary>
public class ExerciseDefinitionMuscleEntity
{
    public string ExerciseDefinitionId { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public decimal Share { get; set; }
}
