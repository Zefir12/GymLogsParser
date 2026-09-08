using System.Text.Json.Serialization;

namespace Core.Models;

public sealed class WorkoutLog
{
    public DateOnly? Date { get; set; }

    public string? Title { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Parsed from "start 17:30" style entries in the raw log.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Parsed from "koniec 18:46" style entries in the raw log.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Training partners mentioned in the log (normalized to nominative form).
    /// Empty if none were mentioned.
    /// </summary>
    public List<string> Persons { get; set; } = [];

    public List<WorkoutExercise> Exercises { get; set; } = [];
}

public sealed class WorkoutExercise
{
    /// <summary>
    /// Stable ID from the application's exercise catalog. The only exercise
    /// identity field requested from the model.
    /// </summary>
    public string ExerciseId { get; set; } = string.Empty;

    /// <summary>
    /// Canonical display name from the catalog. Hydrated server-side
    /// after parsing — never requested from the model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hydrated server-side from the catalog — never requested from the model.
    /// </summary>
    public string? MuscleGroup { get; set; }

    /// <summary>
    /// Comments specifically belonging to this exercise.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Derived server-side from whether Cardio or Sets is populated —
    /// never requested from the model.
    /// </summary>
    public ExerciseCategory Category { get; set; }

    public List<WorkoutSet> Sets { get; set; } = [];

    public List<CardioEntry> Cardio { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExerciseCategory
{
    Strength = 0,
    Cardio = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WeightEntryMode
{
    Total = 0,
    PerSide = 1
}

public sealed class WorkoutSet
{
    /// <summary>
    /// Assigned server-side from array position — never requested from the model.
    /// </summary>
    public int SetNumber { get; set; }

    /// <summary>
    /// Final normalized total weight on the bar/machine/dumbbell.
    /// Always kilograms.
    /// </summary>
    public decimal? Weight { get; set; }

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