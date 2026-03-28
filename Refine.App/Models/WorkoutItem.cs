using SQLite;

namespace Refine.App.Models;

public class WorkoutItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // FOREIGN KEY: Hangi antrenman gününe ait?
    [Indexed]
    public int WorkoutId { get; set; }

    // FOREIGN KEY: Hangi egzersiz?
    public int ExerciseId { get; set; }

    // Egzersiz detaylarını (Adı, Resmi) çekmek için kullanacağız
    // ama veritabanına tüm nesneyi kaydetmiyoruz.
    [Ignore]
    public Exercise Exercise { get; set; } = new Exercise();

    public int Sets { get; set; }
    public string RepsRange { get; set; } = "";
    public int Order { get; set; }
}