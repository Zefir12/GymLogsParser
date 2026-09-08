using Core.Models;

namespace Infrastructure.Entities;

public class Exercise
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }
    public Workout Workout { get; set; } = null!;

    public string ExerciseId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MuscleGroup { get; set; }
    public string? Notes { get; set; }
    public ExerciseCategory Category { get; set; }

    public List<Set> Sets { get; set; } = [];
    public List<Cardio> Cardio { get; set; } = [];
}