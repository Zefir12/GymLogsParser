namespace Infrastructure.Entities;

public class Workout
{
    public Guid Id { get; set; }

    public DateOnly? Date { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    public List<People> Persons { get; set; } = [];
    public List<Exercise> Exercises { get; set; } = [];
}