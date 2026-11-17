using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Infrastructure.Entities;

namespace Infrastructure.Helpers;

/// <summary>What <see cref="HevyCsvWriter.Write"/> produced.</summary>
/// <param name="Csv">The file, header row included, LF line ends, no BOM.</param>
/// <param name="Rows">Set rows (header not counted).</param>
/// <param name="Workouts">Workouts with at least one row.</param>
/// <param name="SkippedSets">Sets and cardio entries left out because they had no weight, reps, distance or duration Hevy accepts.</param>
public sealed record HevyCsvExport(string Csv, int Rows, int Workouts, int SkippedSets);

/// <summary>
/// Writes workouts as a Hevy workout CSV export (#80), the inverse of
/// <see cref="HevyCsv.Parse"/>: the 14 Hevy columns in Hevy's order, text quoted and
/// numbers bare as Hevy writes them, local times as "6 Nov 2026, 18:48", kg, km and
/// seconds. Every value is kept inside the bounds the parser checks (embr's Hevy
/// import uses the same parser), and a set with nothing in range is left out, so the
/// file imports with no bad rows.
/// <list type="bullet">
/// <item>A workout's rows share <c>title</c> + <c>start_time</c>; an empty title becomes
/// "Workout", a missing start time 00:00, and a title + start that is already taken gets
/// " (2)", " (3)", ... so two workouts never merge on import.</item>
/// <item><c>exercise_title</c> is the catalog name of the exercise ID (what the import
/// matches automatically), else the name stored on the workout.</item>
/// <item>Warm-ups are <c>warmup</c>; the "Drop set" / "To failure" / "60 s" / "1.5 km"
/// set notes our import writes go back into <c>set_type</c>, <c>duration_seconds</c>
/// and <c>distance_km</c>. Other set notes have no Hevy column and are dropped.</item>
/// </list>
/// </summary>
public static partial class HevyCsvWriter
{
    public static readonly string[] Columns =
    [
        "title", "start_time", "end_time", "description", "exercise_title", "superset_id",
        "exercise_notes", "set_index", "set_type", "weight_kg", "reps", "distance_km",
        "duration_seconds", "rpe"
    ];

    public const string DateFormat = "d MMM yyyy, HH:mm";
    public const string DefaultTitle = "Workout";

    /// <summary>
    /// Writes <paramref name="workouts"/> (with exercises, sets and cardio loaded) in the
    /// given order. <paramref name="exerciseTitle"/> names an exercise for the file.
    /// </summary>
    public static HevyCsvExport Write(IEnumerable<Workout> workouts, Func<Exercise, string> exerciseTitle)
    {
        var csv = new StringBuilder();
        csv.AppendJoin(',', Columns.Select(Quote)).Append('\n');

        var taken = new HashSet<(string, DateTime)>();
        int rows = 0, written = 0, skipped = 0;

        foreach (var workout in workouts)
        {
            if (workout.Date is not { } date) continue;

            var startTime = Minute(workout.StartTime ?? TimeOnly.MinValue);
            var start = date.ToDateTime(startTime);
            var title = UniqueTitle(Clean(workout.Title) ?? DefaultTitle, start, taken);

            // An end before the start is past midnight: Hevy writes the next day's date.
            DateTime? end = workout.EndTime is { } endTime
                ? date.ToDateTime(Minute(endTime)).AddDays(Minute(endTime) < startTime ? 1 : 0)
                : null;

            var prefix = string.Join(',',
                Quote(title),
                Quote(Format(start)),
                Quote(end is { } e ? Format(e) : ""),
                Quote(workout.Notes ?? ""));

            var workoutRows = 0;
            foreach (var exercise in workout.Exercises)
            {
                var name = Clip(Clean(exerciseTitle(exercise)) ?? Clean(exercise.ExerciseId) ?? "Exercise");
                var head = $"{prefix},{Quote(name)},,{Quote(exercise.Notes ?? "")}";
                var index = 0;

                foreach (var set in exercise.Sets.OrderBy(s => s.SetNumber))
                {
                    var (type, seconds, km) = FromNotes(set.Notes);
                    if (set.Warmup == true) type = "warmup";

                    var weight = InRange(set.Weight, 0m, 1000m);
                    decimal? reps = InRange(set.Reps, 0m, 1000m);
                    if (weight is null && reps is null && seconds is null && km is null)
                    {
                        skipped++;
                        continue;
                    }

                    AppendRow(csv, head, index++, type, weight, reps, km, seconds, InRange(set.Rpe, 1m, 10m));
                }

                foreach (var cardio in exercise.Cardio)
                {
                    decimal? seconds = InRange(cardio.DurationSeconds, 0m, 86_400m);
                    var km = InRange(cardio.DistanceKm, 0m, 1000m);
                    if (seconds is null && km is null)
                    {
                        skipped++;
                        continue;
                    }

                    AppendRow(csv, head, index++, "normal", null, null, km, seconds, null);
                }

                workoutRows += index;
            }

            rows += workoutRows;
            if (workoutRows > 0) written++;
        }

        return new HevyCsvExport(csv.ToString(), rows, written, skipped);
    }

    private static void AppendRow(
        StringBuilder csv, string head, int index, string type,
        decimal? weight, decimal? reps, decimal? km, decimal? seconds, decimal? rpe)
    {
        csv.Append(head).Append(',')
            .Append(index.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(Quote(type)).Append(',')
            .Append(Number(weight)).Append(',')
            .Append(Number(reps)).Append(',')
            .Append(Number(km)).Append(',')
            .Append(Number(seconds is { } s ? decimal.Round(s) : null)).Append(',')
            .Append(Number(rpe)).Append('\n');
    }

    /// <summary>The title, or "title (2)", "title (3)", ... when the title and start time are already in the file.</summary>
    private static string UniqueTitle(string title, DateTime start, HashSet<(string, DateTime)> taken)
    {
        var candidate = Clip(title);
        for (var n = 2; !taken.Add((candidate, start)); n++)
        {
            var suffix = $" ({n})";
            candidate = Clip(title, HevyCsv.MaxTitleLength - suffix.Length) + suffix;
        }

        return candidate;
    }

    /// <summary>Set type, duration and distance from the notes <c>HevyImportService</c> writes ("Drop set, 60 s").</summary>
    private static (string Type, decimal? Seconds, decimal? Km) FromNotes(string? notes)
    {
        string type = "normal";
        decimal? seconds = null, km = null;
        if (string.IsNullOrWhiteSpace(notes)) return (type, seconds, km);

        foreach (var part in notes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("Drop set", StringComparison.OrdinalIgnoreCase)) type = "dropset";
            else if (part.Equals("To failure", StringComparison.OrdinalIgnoreCase)) type = "failure";
            else if (SecondsPattern().Match(part) is { Success: true } s) seconds = InRange(ParseNumber(s.Groups[1].Value), 0m, 86_400m);
            else if (KmPattern().Match(part) is { Success: true } k) km = InRange(ParseNumber(k.Groups[1].Value), 0m, 1000m);
        }

        return (type, seconds, km);
    }

    private static decimal? ParseNumber(string text) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static decimal? InRange(decimal? value, decimal min, decimal max) =>
        value is { } v && v >= min && v <= max ? v : null;

    private static TimeOnly Minute(TimeOnly time) => new(time.Hour, time.Minute);

    private static string Format(DateTime time) => time.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Trimmed, or null when empty (the parser trims every field).</summary>
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Clip(string text, int max = HevyCsv.MaxTitleLength) =>
        text.Length <= max ? text : text[..max].TrimEnd();

    /// <summary>Up to 5 decimals, no trailing zeros, invariant ("82.5").</summary>
    private static string Number(decimal? value) =>
        value is { } v ? v.ToString("0.#####", CultureInfo.InvariantCulture) : "";

    private static string Quote(string text) => $"\"{text.Replace("\"", "\"\"")}\"";

    [GeneratedRegex(@"^(\d+(?:\.\d+)?) s$")]
    private static partial Regex SecondsPattern();

    [GeneratedRegex(@"^(\d+(?:\.\d+)?) km$")]
    private static partial Regex KmPattern();
}
