namespace Infrastructure.Helpers;

/// <summary>
/// One user's bodyweight measurements (from <c>bodyweight_entries</c>). Used to estimate
/// bodyweight on any given workout date (for bodyweight exercises and DOTS scoring).
/// A user without measurements has no bodyweight: <see cref="GetInterpolated"/> returns null.
/// </summary>
public sealed class BodyweightHistory
{
    private readonly (DateOnly Date, double WeightKg)[] _points;

    /// <param name="points">Measurements in any order; a date appears at most once.</param>
    public BodyweightHistory(IEnumerable<(DateOnly Date, double WeightKg)> points)
    {
        _points = points.OrderBy(p => p.Date).ToArray();
    }

    /// <summary>A history without measurements.</summary>
    public static BodyweightHistory Empty { get; } = new([]);

    /// <summary>
    /// Linearly interpolates bodyweight between the two surrounding measurements.
    /// Dates before the first / after the last measurement are clamped to that value.
    /// Null when there are no measurements.
    /// </summary>
    public double? GetInterpolated(DateOnly date)
    {
        if (_points.Length == 0) return null;
        if (date <= _points[0].Date) return _points[0].WeightKg;
        if (date >= _points[^1].Date) return _points[^1].WeightKg;

        for (var i = 1; i < _points.Length; i++)
        {
            var previous = _points[i - 1];
            var next = _points[i];

            if (date > next.Date) continue;
            if (date == next.Date) return next.WeightKg;

            var totalDays = next.Date.DayNumber - previous.Date.DayNumber;
            var elapsedDays = date.DayNumber - previous.Date.DayNumber;
            var fraction = (double)elapsedDays / totalDays;

            return previous.WeightKg + (next.WeightKg - previous.WeightKg) * fraction;
        }

        return _points[^1].WeightKg;
    }
}
