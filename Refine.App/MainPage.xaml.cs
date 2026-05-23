using Refine.App.Services;

namespace Refine.App;

public partial class MainPage : ContentPage
{
    private readonly NavigationBridge _navBridge;
    private readonly TopBarBridge _topBarBridge;
    private bool _isSwitchLeftActive = true;

    public MainPage(NavigationBridge navBridge, TopBarBridge topBarBridge)
    {
        InitializeComponent();
        _navBridge = navBridge;
        _topBarBridge = topBarBridge;

        _topBarBridge.OnConfigChanged += HandleTopBarConfigChanged;

        // Varsayılan sekmeyi ayarla (Home)
        UpdateActiveTab("");

        SetInitialShadows();
    }

    private void SetInitialShadows()
    {
        SwitchLeftCircle.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#3b82f6")),
            Offset = new Point(0, 0),
            Opacity = 0.5f,
            Radius = 12
        };
        SwitchRightCircle.Shadow = null;
    }

    private void HandleTopBarConfigChanged(TopBarConfig config)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (config.Mode == TopBarMode.Home)
            {
                HomeHeaderGrid.IsVisible = true;
                DetailHeaderGrid.IsVisible = false;
            }
            else if (config.Mode == TopBarMode.Detail)
            {
                HomeHeaderGrid.IsVisible = false;
                DetailHeaderGrid.IsVisible = true;
                DetailHeaderTitle.Text = config.Title?.ToUpper() ?? "DETAIL";
            }
        });
    }

    private void OnTopBarLeftTapped(object? sender, EventArgs e)
    {
        _topBarBridge.LeftTapped();
    }

    private void OnTopBarRightTapped(object? sender, EventArgs e)
    {
        _topBarBridge.RightTapped();
    }

    private void OnSwitchTapped(object? sender, EventArgs e)
    {
        _isSwitchLeftActive = !_isSwitchLeftActive;
        
        if (_isSwitchLeftActive)
        {
            // Left is Blue, Right is Gray
            SwitchLeftCircle.BackgroundColor = Color.FromArgb("#3b82f6"); 
            SwitchRightCircle.BackgroundColor = Color.FromArgb("#3f3f46"); 

            SwitchLeftCircle.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#3b82f6")),
                Offset = new Point(0, 0),
                Opacity = 0.5f,
                Radius = 12
            };
            SwitchRightCircle.Shadow = null;
        }
        else
        {
            // Left is Gray, Right is Green
            SwitchLeftCircle.BackgroundColor = Color.FromArgb("#3f3f46"); 
            SwitchRightCircle.BackgroundColor = Color.FromArgb("#22c55e"); 

            SwitchLeftCircle.Shadow = null;
            SwitchRightCircle.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#22c55e")),
                Offset = new Point(0, 0),
                Opacity = 0.5f,
                Radius = 12
            };
        }
    }

    private void OnProfileTapped(object? sender, EventArgs e)
    {
        _navBridge.NavigateTo("profile");
        UpdateActiveTab("profile");
    }

    private void OnNavTapped(object? sender, EventArgs e)
    {
        if (sender is View view && view.GestureRecognizers is not null && view.GestureRecognizers.Count > 0)
        {
            if (view.GestureRecognizers[0] is TapGestureRecognizer tap)
            {
                string targetUrl = tap.CommandParameter?.ToString() ?? "";
                if (targetUrl == "preferences")
                {
                    _navBridge.NavigateTo("profile");
                }
                else
                {
                    _navBridge.NavigateTo(targetUrl);
                }
                UpdateActiveTab(targetUrl);
            }
        }
    }

    private void UpdateActiveTab(string activeUrl)
    {
        // RENKLER
        var inactiveColor = Color.FromArgb("#66ffffff"); // Soluk Beyaz
        var activeColor = Color.FromArgb("#ccff00");     // NEON LIME
        
        string fontOutlined = "MaterialOutlined";
        string fontFilled = "MaterialFilled";

        // 1. Önce hepsini SIFIRLA (Outlined Font + Soluk Renk)
        IconHome.FontFamily = fontOutlined;
        IconHome.TextColor = inactiveColor;
        TextHome.TextColor = inactiveColor;

        IconWorkouts.FontFamily = fontOutlined;
        IconWorkouts.TextColor = inactiveColor;
        TextWorkouts.TextColor = inactiveColor;

        IconAnalytics.FontFamily = fontOutlined;
        IconAnalytics.TextColor = inactiveColor;
        TextAnalytics.TextColor = inactiveColor;

        IconPreferences.FontFamily = fontOutlined;
        IconPreferences.TextColor = inactiveColor;
        TextPreferences.TextColor = inactiveColor;

        // 2. Seçili olanı AKTİF YAP (Filled Font + Neon Renk)
        switch (activeUrl)
        {
            case "":
                IconHome.FontFamily = fontFilled;
                IconHome.TextColor = activeColor;
                TextHome.TextColor = activeColor;
                break;

            case "workouts":
                IconWorkouts.FontFamily = fontFilled;
                IconWorkouts.TextColor = activeColor;
                TextWorkouts.TextColor = activeColor;
                break;

            case "analytics":
                IconAnalytics.FontFamily = fontFilled;
                IconAnalytics.TextColor = activeColor;
                TextAnalytics.TextColor = activeColor;
                break;

            case "preferences":
            case "profile":
                IconPreferences.FontFamily = fontFilled;
                IconPreferences.TextColor = activeColor;
                TextPreferences.TextColor = activeColor;
                break;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Try to handle back natively in Blazor first
        var task = _navBridge.RequestHardwareBackAsync();
        
        // Since OnBackButtonPressed is synchronous, we block briefly or run async void.
        // It's safer to run synchronously if possible, or return true and evaluate asynchronously.
        // For MAUI Blazor, returning true prevents app close. If we want to close, we can use Application.Current.Quit().
        var handled = task.GetAwaiter().GetResult();
        
        if (handled)
        {
            return true; // We handled it
        }

        return base.OnBackButtonPressed(); // Exit app
    }
}