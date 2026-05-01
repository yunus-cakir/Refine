using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models;

public class ExerciseMuscleMap
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [ForeignKey(typeof(Exercise))]
    public int ExerciseId { get; set; }

    [ForeignKey(typeof(MuscleGroup))]
    public int MuscleGroupId { get; set; }

    // Etki Çarpanı (Örn: 1.0 = Ana Kas, 0.5 = İkincil Kas)
    public double ImpactMultiplier { get; set; }

    [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
    public MuscleGroup MuscleGroup { get; set; }
}
