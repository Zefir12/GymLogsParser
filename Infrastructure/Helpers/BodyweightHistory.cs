using System.Globalization;

namespace Infrastructure.Helpers;

/// <summary>
/// The user's historical bodyweight measurements. Used to estimate bodyweight
/// on any given workout date (for bodyweight exercises and DOTS scoring).
/// </summary>
public static class BodyweightHistory
{
    private static readonly BodyweightPoint[] Points =
        [
            new("2018-12-17", 66.0), new("2019-02-04", 66.4), new("2019-04-03", 67.0), new("2019-05-20", 65.1), new("2019-08-02", 65.3), new("2019-09-02", 66.1), new("2019-10-19", 67.0), new("2019-12-21", 65.0),
            new("2020-07-26", 67.0), new("2022-08-01", 70.05), new("2022-08-18", 69.6), new("2023-08-01", 70.4), new("2023-08-10", 71.6), new("2023-08-14", 72.2), new("2023-08-20", 72.0), new("2023-08-28", 73.8),
            new("2023-09-03", 74.5), new("2023-09-08", 75.4), new("2023-09-13", 75.9), new("2023-09-18", 76.0), new("2023-09-23", 76.4), new("2023-09-29", 76.9), new("2023-10-04", 75.0), new("2023-10-12", 74.6),
            new("2023-10-20", 74.5), new("2023-11-29", 73.0), new("2024-01-11", 72.4), new("2024-02-08", 72.9), new("2024-02-22", 75.5), new("2024-03-13", 73.3), new("2024-04-17", 75.7), new("2024-05-23", 78.2),
            new("2024-06-19", 77.1), new("2024-07-06", 78.8), new("2024-08-11", 78.5), new("2024-09-20", 81.2), new("2024-10-16", 82.7), new("2024-11-25", 82.3), new("2024-12-30", 84.4), new("2025-01-26", 85.3),
            new("2025-02-19", 86.1), new("2025-03-15", 84.9), new("2025-04-17", 85.7), new("2025-05-30", 87.7), new("2025-06-14", 87.0), new("2025-07-19", 86.7), new("2025-08-14", 84.6), new("2025-09-03", 83.7),
            new("2025-10-27", 80.7), new("2025-12-17", 84.4), new("2026-01-21", 87.4), new("2026-03-23", 89.1), new("2026-05-04", 90.1), new("2026-07-08", 91.9), new("2026-07-23", 91.7), new("2026-08-06", 91.0)
        ];

    /// <summary>
    /// Linearly interpolates bodyweight between the two surrounding measurements.
    /// Dates before the first / after the last measurement are clamped to that value.
    /// </summary>
    public static double GetInterpolated(DateOnly date)
    {
        if (date <= Points[0].Date) return Points[0].WeightKg;
        if (date >= Points[^1].Date) return Points[^1].WeightKg;

        for (var i = 1; i < Points.Length; i++)
        {
            var previous = Points[i - 1];
            var next = Points[i];

            if (date > next.Date) continue;
            if (date == next.Date) return next.WeightKg;

            var totalDays = next.Date.DayNumber - previous.Date.DayNumber;
            var elapsedDays = date.DayNumber - previous.Date.DayNumber;
            var fraction = (double)elapsedDays / totalDays;

            return previous.WeightKg + (next.WeightKg - previous.WeightKg) * fraction;
        }

        return Points[^1].WeightKg;
    }

    private readonly record struct BodyweightPoint(string DateString, double WeightKg)
    {
        public DateOnly Date { get; } = DateOnly.ParseExact(DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}