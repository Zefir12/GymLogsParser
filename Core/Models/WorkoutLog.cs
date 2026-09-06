namespace GymLog.Api.Models;

public sealed class WorkoutLog
{
    public DateOnly? Date { get; set; }

    public string? Title { get; set; }

    public string? Notes { get; set; }

    public List<WorkoutExercise> Exercises { get; set; } = [];
}

public sealed class WorkoutExercise
{
    /// <summary>
    /// Stable ID from the application's exercise catalog.
    /// </summary>
    public string ExerciseId { get; set; } = string.Empty;

    /// <summary>
    /// Canonical display name from the catalog.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }

    /// <summary>
    /// Comments specifically belonging to this exercise.
    /// </summary>
    public string? Notes { get; set; }

    public ExerciseCategory Category { get; set; }

    public List<WorkoutSet> Sets { get; set; } = [];

    public List<CardioEntry> Cardio { get; set; } = [];
}

public enum ExerciseCategory
{
    Strength = 0,
    Cardio = 1
}

public enum WeightEntryMode
{
    Total = 0,
    PerSide = 1
}

public sealed class WorkoutSet
{
    public int? SetNumber { get; set; }

    /// <summary>
    /// Final normalized total weight on the bar/machine/dumbbell.
    /// Always kilograms.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// How the user wrote the weight.
    /// This is important for auditing AI interpretation.
    /// </summary>
    public WeightEntryMode WeightEntryMode { get; set; }

    public int? Reps { get; set; }

    public decimal? Rir { get; set; }

    public decimal? Rpe { get; set; }

    public bool? Warmup { get; set; }

    public string? Notes { get; set; }
}

public sealed class CardioEntry
{
    public string Activity { get; set; } = string.Empty;

    public int? DurationSeconds { get; set; }

    public decimal? DistanceKm { get; set; }

    public decimal? SpeedKmh { get; set; }

    public decimal? InclinePercent { get; set; }

    public string? Notes { get; set; }
}