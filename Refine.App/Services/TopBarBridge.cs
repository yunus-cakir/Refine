namespace Refine.App.Services;

public enum TopBarMode
{
    Home,
    Standard,
    Detail
}

public class TopBarConfig
{
    public TopBarMode Mode { get; set; } = TopBarMode.Home;
    public string Title { get; set; } = string.Empty;
}

public class TopBarBridge
{
    public event Action<TopBarConfig>? OnConfigChanged;
    public event Action? OnLeftActionTapped;
    public event Action? OnRightActionTapped;
    public event Action? OnDefaultBackRequested;

    public TopBarConfig CurrentConfig { get; private set; } = new();

    public void SetHomeMode(string title = "")
    {
        CurrentConfig = new TopBarConfig { Mode = TopBarMode.Home, Title = title };
        OnConfigChanged?.Invoke(CurrentConfig);
    }

    public void SetStandardMode(string title)
    {
        CurrentConfig = new TopBarConfig { Mode = TopBarMode.Standard, Title = title };
        OnConfigChanged?.Invoke(CurrentConfig);
    }

    public void SetDetailMode(string title)
    {
        CurrentConfig = new TopBarConfig { Mode = TopBarMode.Detail, Title = title };
        OnConfigChanged?.Invoke(CurrentConfig);
    }

    public void LeftTapped()
    {
        if (OnLeftActionTapped != null)
        {
            OnLeftActionTapped.Invoke();
        }
        else
        {
            OnDefaultBackRequested?.Invoke();
        }
    }

    public void RightTapped() => OnRightActionTapped?.Invoke();
}
