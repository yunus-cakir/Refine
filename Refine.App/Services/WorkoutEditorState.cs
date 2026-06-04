using Refine.App.Models;

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
