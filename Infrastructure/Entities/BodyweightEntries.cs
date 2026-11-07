namespace Infrastructure.Entities;

/// <summary>One bodyweight measurement of one user (#45).</summary>
public class BodyweightEntryEntity
{
    /// <summary>OIDC <c>sub</c> of the user who was weighed.</summary>
    public string OwnerId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal WeightKg { get; set; }
}
