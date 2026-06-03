using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models;

public class Exercise
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Equipment { get; set; } = "";
    public string ImageUrl { get; set; } = "";

    public decimal CnsFatigueScore { get; set; } // Örn: 8.5

    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<ExerciseMuscleMap> MuscleMaps { get; set; } = new();

    [Ignore]
    public string PrimaryMuscleCategory
    {
        get
        {
            var primaryMap = MuscleMaps?.OrderByDescending(m => m.ImpactMultiplier).FirstOrDefault();
            return primaryMap?.MuscleGroup?.Category ?? "General";
        }
    }

    public bool IsCustom { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
}