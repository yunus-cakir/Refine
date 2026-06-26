using SQLite;

namespace Refine.App.Models.Entities;

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

    [Ignore]
    public bool IsUiModified { get; set; } = false;

    private double? _uiWeight;
    [Ignore]
    public double? UIWeight
    {
        get => _uiWeight;
        set { _uiWeight = value; IsUiModified = true; }
    }

    private int? _uiReps;
    [Ignore]
    public int? UIReps
    {
        get => _uiReps;
        set { _uiReps = value; IsUiModified = true; }
    }

    private int? _uiRIR;
    [Ignore]
    public int? UIRIR
    {
        get => _uiRIR;
        set { _uiRIR = value; IsUiModified = true; }
    }

    private int _uiFormRating = 3;
    [Ignore]
    public int UIFormRating
    {
        get => _uiFormRating;
        set { _uiFormRating = value; IsUiModified = true; }
    }

    public void SetAutoCopiedValues(double? weight, int? reps, int? rir, int formRating)
    {
        _uiWeight = weight;
        _uiReps = reps;
        _uiRIR = rir;
        _uiFormRating = formRating;
        IsUiModified = false;
    }
}
