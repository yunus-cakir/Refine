using SQLite;

namespace Refine.App.Models;

public class Workout
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutProgramId { get; set; }

    public string Name { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;

    [Ignore]
    public List<WorkoutItem> Items { get; set; } = new();
}