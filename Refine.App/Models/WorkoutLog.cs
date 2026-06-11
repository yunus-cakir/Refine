using SQLite;

namespace Refine.App.Models;

public class WorkoutLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutId { get; set; }

    [Indexed]
    public int ExerciseId { get; set; }

    public string ExerciseNameSnapshot { get; set; } = "";

    [Indexed]
    public DateTime Date { get; set; }

    public int SetNumber { get; set; }
    public double? Weight { get; set; }
    public int? Reps { get; set; }

    public int? RIR { get; set; }
    public int FormRating { get; set; }

    public string? Note { get; set; }

    public int Week { get; set; } = 1;
    public int Cycle { get; set; } = 0;

    public bool IsCompleted { get; set; } = false;
    public bool IsSaved { get; set; } = false;
}