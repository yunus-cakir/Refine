using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models;

public class Exercise
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public EquipmentType Equipment { get; set; } = EquipmentType.None;
    public string ImageUrl { get; set; } = "";

    public enum EquipmentType
    {
        None,
        Barbell,
        Dumbbell,
        Machine,
        Cable,
        Bodyweight,
        Kettlebell
    }

    public enum LateralityType
    {
        Bilateral,
        UnilateralAlternating,
        UnilateralIsolated
    }

    public LateralityType Laterality { get; set; } = LateralityType.Bilateral;


    public decimal CnsFatigueScore { get; set; } // Örn: 8.5

    [TextBlob("VariationTagsBlob")]
    public List<string> VariationTags { get; set; } = new();
    public string VariationTagsBlob { get; set; } = "";

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