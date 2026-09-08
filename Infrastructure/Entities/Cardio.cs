namespace Infrastructure.Entities;

public sealed class Cardio
{
    public Guid Id { get; set; }

    public Guid WorkoutExerciseId { get; set; }
    public Exercise WorkoutExercise { get; set; } = null!;

    public string Activity { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? InclinePercent { get; set; }
    public string? Notes { get; set; }
}