using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models;

public class WorkoutItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // FOREIGN KEY: Hangi antrenman gününe ait?
    [ForeignKey(typeof(Workout))]
    public int WorkoutId { get; set; }

    // FOREIGN KEY: Hangi egzersiz?
    [ForeignKey(typeof(Exercise))]
    public int ExerciseId { get; set; }

    // Egzersiz detaylarını (Adı, Resmi) çekmek için kullanacağız
    // ama veritabanına tüm nesneyi kaydetmiyoruz.
    [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
    public Exercise Exercise { get; set; } = new Exercise();

    public int Sets { get; set; }
    public string RepsRange { get; set; } = "";
    public int Order { get; set; }
}