using System;

namespace Refine.App.Services;

public enum TopBarMode
{
    Home,
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

    public void SetHomeMode()
    {
        OnConfigChanged?.Invoke(new TopBarConfig { Mode = TopBarMode.Home });
    }

    public void SetDetailMode(string title)
    {
        OnConfigChanged?.Invoke(new TopBarConfig { Mode = TopBarMode.Detail, Title = title });
    }

    public void LeftTapped() => OnLeftActionTapped?.Invoke();
    public void RightTapped() => OnRightActionTapped?.Invoke();
}
