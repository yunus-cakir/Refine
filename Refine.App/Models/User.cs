using SQLite;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;

namespace Refine.App.Models;

public class AppSettings
{
    public string Language { get; set; } = "en";
    public string UnitSystem { get; set; } = "metric";
    public string Theme { get; set; } = "system";
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Monday;
}
public class WorkoutSettings
{
    public int PreferredRestTime { get; set; } = 90;
    public int AverageSetDuration { get; set; } = 45;
    public int PreferredSetCount { get; set; } = 3;
    public int PreferredMinReps { get; set; } = 8;
    public int PreferredMaxReps { get; set; } = 12;
    public string PreferredRepRange { get; set; } = "8-12";
    public decimal PreferredRIR { get; set; } = 2.0m;
    public bool AutoCopyWeight { get; set; } = true;
    public bool AutoCopyReps { get; set; } = true;
    public bool AutoCopyRIR { get; set; } = true;
    public bool AutoCopyFormRating { get; set; } = true;
    public decimal CnsThreshold { get; set; } = 150m;
    public string CycleLength { get; set; } = "Weekly";
}

public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Gender { get; set; } = "";
    
    // Flat cached biometric data
    public double Height { get; set; }
    public double Weight { get; set; }

    // Flat nutrition settings
    public int TargetDailyCalories { get; set; } = 2500;
    public string MetabolismType { get; set; } = "normal";

    [ForeignKey(typeof(WorkoutProgram))]
    public int? SelectedWorkoutProgramId { get; set; }

    [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
    public WorkoutProgram? SelectedWorkoutProgram { get; set; }

    [Ignore]
    public List<WorkoutProgram> WorkoutPrograms { get; set; } = new();

    // Relational biometrics
    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<BiometricLog> BiometricLogs { get; set; } = new();

    // Blob settings
    [TextBlob("AppSettingsBlob"), Ignore]
    public AppSettings AppSettings { get; set; } = new();
    public string AppSettingsBlob { get; set; } = "";

    [TextBlob("WorkoutSettingsBlob"), Ignore]
    public WorkoutSettings WorkoutSettings { get; set; } = new();
    public string WorkoutSettingsBlob { get; set; } = "";
}
