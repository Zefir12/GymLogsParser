namespace Infrastructure.Entities;

/// <summary>The weekly target range (working sets per week) of one muscle group.</summary>
public class MuscleTargetEntity
{
    public string MuscleGroup { get; set; } = string.Empty;
    public int MinSets { get; set; }
    public int MaxSets { get; set; }
}
