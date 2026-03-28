using SQLite;

namespace Refine.App.Models;

public class Exercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Equipment { get; set; } = "";
    public string MuscleGroup { get; set; } = "";
    public string ImageUrl { get; set; } = "";

    public bool IsCustom { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
}