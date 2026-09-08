namespace Infrastructure.Entities;

public class Set
{
    public Guid Id { get; set; }

    public Guid WorkoutExerciseId { get; set; }
    public Exercise WorkoutExercise { get; set; } = null!;

    public int SetNumber { get; set; }
    public decimal? Weight { get; set; }
    public int? Reps { get; set; }
    public decimal? Rir { get; set; }
    public decimal? Rpe { get; set; }
    public bool? Warmup { get; set; }
    public string? Notes { get; set; }
}