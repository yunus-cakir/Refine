using SQLite;
using SQLiteNetExtensions.Attributes;

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
    public int Cycle { get; set; } = 1;
    public DateTime LastCycleUpdateDate { get; set; } = DateTime.Now;

    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<Workout> Workouts { get; set; } = new();
}