namespace Core.Models;

/// <summary>
/// Aggregated training data for the dashboard: one row per training day with the
/// working-set totals of each exercise done that day. The client derives every
/// dashboard section (tiles, heatmap, tables, charts) from it, so it never has to
/// load full workouts.
/// </summary>
public sealed class DashboardData
{
    /// <summary>
    /// First day covered: a Monday at least 52 weeks before the week of <see cref="To"/>,
    /// earlier when the requested range needs it.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>Last day covered (inclusive).</summary>
    public DateOnly To { get; set; }

    /// <summary>Every exercise referenced by <see cref="Days"/>.</summary>
    public List<DashboardExercise> Exercises { get; set; } = [];

    /// <summary>Days with at least one logged exercise, oldest first.</summary>
    public List<DashboardDay> Days { get; set; } = [];
}

public sealed class DashboardExercise
{
    public string ExerciseId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MuscleGroup { get; set; }
    public ExerciseCategory Category { get; set; }
}

public sealed class DashboardDay
{
    public DateOnly Date { get; set; }

    /// <summary>Workout title(s) for the day, joined with " + " when there were several.</summary>
    public string? Title { get; set; }

    public List<DashboardDayEntry> Entries { get; set; } = [];
}

/// <summary>
/// One exercise on one day. Warm-up sets are excluded everywhere. The weight metrics
/// only use sets with both weight and reps, like the exercise progress endpoint.
/// </summary>
public sealed class DashboardDayEntry
{
    public string ExerciseId { get; set; } = string.Empty;

    /// <summary>
    /// The workout(s) this exercise was logged in that day, so the client can open and
    /// edit them. Usually one; several when the exercise was in more than one workout.
    /// </summary>
    public List<Guid> WorkoutIds { get; set; } = [];

    /// <summary>Working sets (strength) or cardio entries.</summary>
    public int Sets { get; set; }

    /// <summary>Cardio duration in minutes (0 for strength).</summary>
    public decimal Minutes { get; set; }

    /// <summary>Heaviest working set weight, kg.</summary>
    public decimal TopSet { get; set; }

    /// <summary>Best Epley estimated one-rep max of the day's working sets, kg.</summary>
    public decimal EstimatedOneRepMax { get; set; }

    /// <summary>Sum of weight * reps over working sets, kg.</summary>
    public decimal Volume { get; set; }

    /// <summary>
    /// True when the day's estimated 1RM beats every earlier session of this exercise
    /// (all history, not only the dashboard window). A first-ever session is not a PR.
    /// </summary>
    public bool Pr { get; set; }
}
