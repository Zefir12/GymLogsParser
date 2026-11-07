namespace Core.Models;

/// <summary>
/// One bodyweight measurement of the signed-in user (<c>/api/bodyweight-entries</c>, #46).
/// The date is the key: a user has at most one measurement per day.
/// </summary>
public sealed class BodyweightEntry
{
    public DateOnly Date { get; set; }

    /// <summary>Kilograms, at most two decimals, within <see cref="MinKg"/>..<see cref="MaxKg"/>.</summary>
    public decimal WeightKg { get; set; }

    public const decimal MinKg = 20m;
    public const decimal MaxKg = 400m;

    /// <summary>The earliest date a measurement may have.</summary>
    public static readonly DateOnly MinDate = new(1900, 1, 1);
}

/// <summary>Outcome of adding, moving or changing a measurement.</summary>
public enum BodyweightWriteResult
{
    Saved,
    /// <summary>The user has no measurement on the date being edited.</summary>
    NotFound,
    /// <summary>The user already has a measurement on the target date.</summary>
    DateTaken
}
