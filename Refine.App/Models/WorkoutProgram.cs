using SQLite;

namespace Refine.App.Models;

public class WorkoutProgram
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public string Goal { get; set; } = "";
    public string TargetMuscles { get; set; } = "";
    public string Environment { get; set; } = "";

    [Ignore]
    public List<Workout> Workouts { get; set; } = new();
}