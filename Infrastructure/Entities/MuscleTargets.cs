namespace Infrastructure.Entities;

/// <summary>One user's weekly target range (working sets per week) of one muscle group.</summary>
public class MuscleTargetEntity
{
    /// <summary>OIDC <c>sub</c> of the user these ranges belong to (#45).</summary>
    public string OwnerId { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public int MinSets { get; set; }
    public int MaxSets { get; set; }
}
