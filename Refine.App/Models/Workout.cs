using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models;

public class Workout
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [ForeignKey(typeof(WorkoutProgram))]
    public int WorkoutProgramId { get; set; }

    public string Name { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;

    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<WorkoutItem> Items { get; set; } = new();
}