namespace Infrastructure.Entities;

public class People
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }
    public Workout Workout { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
}