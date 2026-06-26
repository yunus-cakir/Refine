using SQLite;
using SQLiteNetExtensions.Attributes;

using Refine.App.Models.Entities;
namespace Refine.App.Models.Enums;

public class MuscleGroup
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Name { get; set; } = ""; // e.g., "Front Delt", "Upper Chest"
    public string Category { get; set; } = ""; // e.g., "Shoulders", "Chest"
    
    public string SvgPathData { get; set; } = "";

    [ManyToMany(typeof(ExerciseMuscleMap), CascadeOperations = CascadeOperation.All)]
    public List<Exercise> Exercises { get; set; } = new();
}

