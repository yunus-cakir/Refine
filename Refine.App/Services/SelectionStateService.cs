namespace Refine.App.Services;

public class SelectionStateService
{
    public List<int> SelectedIds { get; set; } = new();
    public string ReturnUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public void Clear()
    {
        SelectedIds.Clear();
        ReturnUrl = string.Empty;
        IsActive = false;
    }
}
