using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;

namespace Infrastructure.Helpers;

/// <summary>One valid set row of a Hevy export.</summary>
public sealed record HevyRow(
    int Line,
    string Title,
    DateTime Start,
    DateTime? End,
    string? Description,
    string ExerciseTitle,
    string? ExerciseNotes,
    string SetType,
    decimal? WeightKg,
    int? Reps,
    decimal? DistanceKm,
    int? DurationSeconds,
    decimal? Rpe);

/// <summary>The rows of a Hevy export that parsed, and the ones that didn't.</summary>
public sealed record HevyCsvResult(IReadOnlyList<HevyRow> Rows, IReadOnlyList<HevyImportRowError> Errors);

/// <summary>
/// Parses a Hevy workout CSV export (#77): one row per set with the columns
/// <c>title, start_time, end_time, description, exercise_title, superset_id,
/// exercise_notes, set_index, set_type, weight_kg, reps, distance_km,
/// duration_seconds, rpe</c>. Columns are found by header name, so their order
/// doesn't matter; only <c>title</c>, <c>start_time</c> and <c>exercise_title</c> are
/// required. A row that doesn't parse becomes an error with its line number and is
/// left out; the other rows still import.
/// </summary>
public static partial class HevyCsv
{
    public const int MaxRows = 100_000;
    public const int MaxTitleLength = 200;

    /// <summary>Hevy writes "6 Nov 2026, 18:48" (no time zone: local time). The ISO forms are accepted too.</summary>
    private static readonly string[] DateFormats =
    [
        "d MMM yyyy, HH:mm",
        "d MMM yyyy, H:mm",
        "d MMM yyyy HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm"
    ];

    private static readonly string[] RequiredColumns = ["title", "start_time", "exercise_title"];

    private static readonly HashSet<string> SetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "normal", "warmup", "dropset", "failure"
    };

    /// <summary>
    /// Parses <paramref name="csv"/>. Throws <see cref="FormatException"/> with an
    /// English message for problems with the whole file (no header, a required column
    /// missing, an unclosed quote, too many rows).
    /// </summary>
    public static HevyCsvResult Parse(string csv)
    {
        using var records = CsvReader.Read(csv).GetEnumerator();
        if (!records.MoveNext())
        {
            throw new FormatException("The file is empty. Export your workouts from Hevy (Settings > Export & import data > Export workouts) and upload that CSV.");
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var header = records.Current.Fields;
        for (var i = 0; i < header.Count; i++)
        {
            columns.TryAdd(header[i].Trim(), i);
        }

        var missing = RequiredColumns.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new FormatException(
                $"This doesn't look like a Hevy workout export: the header has no {string.Join(", ", missing)} column. Upload the CSV from Hevy's \"Export workouts\".");
        }

        var rows = new List<HevyRow>();
        var errors = new List<HevyImportRowError>();
        var count = 0;

        while (records.MoveNext())
        {
            if (++count > MaxRows)
            {
                throw new FormatException($"The file has more than {MaxRows} rows. Split the export into smaller files.");
            }

            var record = records.Current;
            if (ParseRow(record, columns, header.Count, out var row) is { } error)
            {
                errors.Add(new HevyImportRowError { Line = record.Line, Error = error });
            }
            else
            {
                rows.Add(row!);
            }
        }

        return new HevyCsvResult(rows, errors);
    }

    private static string? ParseRow(CsvRecord record, Dictionary<string, int> columns, int headerCount, out HevyRow? row)
    {
        row = null;

        string? Get(string column) =>
            columns.TryGetValue(column, out var index) && index < record.Fields.Count && record.Fields[index].Trim() is { Length: > 0 } value
                ? value
                : null;

        if (record.Fields.Count != headerCount)
        {
            return $"The row has {record.Fields.Count} fields but the header has {headerCount}.";
        }

        var title = Get("title");
        if (title is null) return "The workout title is empty.";
        if (title.Length > MaxTitleLength) return $"The workout title is longer than {MaxTitleLength} characters.";

        var startText = Get("start_time");
        if (startText is null) return "The start time is empty.";
        if (ParseDate(startText) is not { } start) return $"The start time \"{Clip(startText)}\" is not a date like \"6 Nov 2026, 18:48\".";

        // The end time is only metadata: a broken one is dropped rather than failing the set.
        var end = Get("end_time") is { } endText ? ParseDate(endText) : null;

        var exerciseTitle = Get("exercise_title");
        if (exerciseTitle is null) return "The exercise title is empty.";
        if (exerciseTitle.Length > MaxTitleLength) return $"The exercise title is longer than {MaxTitleLength} characters.";

        var setType = Get("set_type") ?? "normal";
        if (!SetTypes.Contains(setType)) return $"The set type \"{Clip(setType)}\" is not normal, warmup, dropset or failure.";

        if (ParseDecimal(Get("weight_kg"), "weight_kg", 0m, 1000m, out var weight) is { } weightError) return weightError;
        if (ParseDecimal(Get("reps"), "reps", 0m, 1000m, out var reps) is { } repsError) return repsError;
        if (reps is { } r && r != decimal.Truncate(r)) return $"reps must be a whole number, not {r.ToString(CultureInfo.InvariantCulture)}.";
        if (ParseDecimal(Get("distance_km"), "distance_km", 0m, 1000m, out var distance) is { } distanceError) return distanceError;
        if (ParseDecimal(Get("duration_seconds"), "duration_seconds", 0m, 86_400m, out var duration) is { } durationError) return durationError;
        if (ParseDecimal(Get("rpe"), "rpe", 1m, 10m, out var rpe) is { } rpeError) return rpeError;

        if (weight is null && reps is null && distance is null && duration is null)
        {
            return "The set has no weight, reps, distance or duration.";
        }

        row = new HevyRow(
            record.Line,
            title,
            start,
            end,
            Get("description"),
            exerciseTitle,
            Get("exercise_notes"),
            setType.ToLowerInvariant(),
            weight,
            reps is { } wholeReps ? (int)wholeReps : null,
            distance,
            duration is { } seconds ? (int)decimal.Round(seconds) : null,
            rpe);
        return null;
    }

    private static string? ParseDecimal(string? text, string column, decimal min, decimal max, out decimal? value)
    {
        value = null;
        if (text is null) return null;

        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return $"{column} \"{Clip(text)}\" is not a number.";
        }

        if (parsed < min || parsed > max)
        {
            return $"{column} {parsed.ToString(CultureInfo.InvariantCulture)} is outside {min.ToString(CultureInfo.InvariantCulture)}–{max.ToString(CultureInfo.InvariantCulture)}.";
        }

        value = parsed;
        return null;
    }

    private static DateTime? ParseDate(string text) =>
        DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            ? date
            : null;

    /// <summary>Keeps echoed file content short in error messages.</summary>
    private static string Clip(string text) => text.Length <= 40 ? text : text[..40] + "…";

    /// <summary>
    /// The words of an exercise name, lower case, sorted, with a trailing plural "s"
    /// dropped, so "Bench Press (Barbell)" and "Barbell Bench Press" (or "Pull Up" and
    /// "Pull-Up") give the same key.
    /// </summary>
    public static string NameKey(string name) =>
        string.Join(' ', WordPattern().Matches(name.ToLowerInvariant())
            .Select(m => m.Value.Length > 3 && m.Value.EndsWith('s') ? m.Value[..^1] : m.Value)
            .Order(StringComparer.Ordinal));

    /// <summary>
    /// The keys to try for a Hevy title, best first: the whole title (equipment in the
    /// brackets counts as words); then without a " - Bar Grip" style suffix; then without
    /// a generic equipment bracket ("(Machine)", "(Cable)"), which the catalog often
    /// leaves out. Barbell and dumbbell are never dropped: they change the exercise.
    /// </summary>
    public static IEnumerable<string> MatchKeys(string hevyTitle)
    {
        var title = hevyTitle.Trim();
        yield return NameKey(title);

        var dash = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            title = title[..dash].Trim();
            yield return NameKey(title);
        }

        if (GenericEquipmentPattern().Match(title) is { Success: true } generic)
        {
            yield return NameKey(title[..generic.Index]);
        }
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s*\((machine|cable|machine plates|plate loaded|band|bodyweight)\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GenericEquipmentPattern();
}
