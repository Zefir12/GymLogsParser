namespace Core.Models;

/// <summary>
/// A Hevy CSV export to preview or import (#77). The UI reads the file and sends its
/// text; <see cref="Mappings"/> maps Hevy exercise titles to catalog IDs.
/// </summary>
public sealed class HevyImportRequest
{
    /// <summary>The whole CSV file, header row included.</summary>
    public string? Csv { get; set; }

    /// <summary>
    /// Hevy <c>exercise_title</c> → catalog ID chosen by the user. A title that is
    /// missing here uses the automatic match; a title mapped to null or "" is skipped.
    /// </summary>
    public Dictionary<string, string?>? Mappings { get; set; }
}

/// <summary>
/// What an import did (or, for a preview, would do). Counts only cover rows that
/// parsed; rows with errors are listed in <see cref="Errors"/> and skipped.
/// </summary>
public sealed class HevyImportSummary
{
    /// <summary>False for a preview: nothing was saved.</summary>
    public bool Imported { get; set; }

    /// <summary>Workouts in the file (one per <c>title</c> + <c>start_time</c>).</summary>
    public int WorkoutsInFile { get; set; }

    /// <summary>New workouts saved (or to be saved).</summary>
    public int Workouts { get; set; }

    /// <summary>Sets (strength sets plus cardio entries) in those new workouts.</summary>
    public int Sets { get; set; }

    /// <summary>Workouts skipped because the user already has one with the same date, start time and title.</summary>
    public int DuplicateWorkouts { get; set; }

    /// <summary>Sets of new workouts skipped because their exercise has no catalog mapping.</summary>
    public int UnmappedSets { get; set; }

    /// <summary>Every distinct Hevy exercise title in the file, with its resolved catalog ID (null = unmapped).</summary>
    public List<HevyImportExercise> Exercises { get; set; } = [];

    /// <summary>The first <c>MaxReportedErrors</c> bad rows; <see cref="ErrorCount"/> has the total.</summary>
    public List<HevyImportRowError> Errors { get; set; } = [];

    public int ErrorCount { get; set; }
}

public sealed class HevyImportExercise
{
    /// <summary>The Hevy <c>exercise_title</c>, e.g. "Bench Press (Barbell)".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Valid set rows with this title in the whole file.</summary>
    public int Sets { get; set; }

    /// <summary>The catalog ID the sets go to, or null when they are skipped.</summary>
    public string? ExerciseId { get; set; }

    /// <summary>True when <see cref="ExerciseId"/> came from the automatic name match, not from the request.</summary>
    public bool AutoMatched { get; set; }
}

public sealed class HevyImportRowError
{
    /// <summary>1-based line in the file where the row starts (the header is line 1).</summary>
    public int Line { get; set; }

    public string Error { get; set; } = string.Empty;
}
