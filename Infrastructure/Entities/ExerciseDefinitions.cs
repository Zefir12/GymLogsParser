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
    public string MuscleGroup { get; set; } = string.Empty;
    public bool Barbell { get; set; }
    public List<string> Aliases { get; set; } = [];
}
