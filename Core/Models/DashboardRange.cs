namespace Core.Models;

/// <summary>
/// The dashboard's date ranges, as the client sends them in <c>?range=</c>.
/// Day counts match the client's <c>RANGE_DAYS</c>.
/// </summary>
public static class DashboardRange
{
    /// <summary>Used when no range is given: the 53 weeks the heatmap always shows cover it.</summary>
    public const string Default = "1Y";

    private static readonly Dictionary<string, int> DaysByRange = new(StringComparer.OrdinalIgnoreCase)
    {
        ["3M"] = 91,
        ["6M"] = 182,
        ["1Y"] = 365,
        ["5Y"] = 1826,
        ["10Y"] = 3653
    };

    public static IReadOnlyCollection<string> All => DaysByRange.Keys;

    /// <summary>Days covered by <paramref name="range"/>, ending with (and including) today.</summary>
    public static bool TryGetDays(string range, out int days) => DaysByRange.TryGetValue(range, out days);
}
