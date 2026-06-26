using Refine.App.Models.Entities;
using Refine.App.Models.UI;
using Refine.App.Models.Enums;

namespace Refine.App.Services;

public class WorkoutEditorState
{
    public Workout? DraftWorkout { get; set; }
    public bool IsActive { get; set; }

    public void Clear()
    {
        DraftWorkout = null;
        IsActive = false;
    }
}

