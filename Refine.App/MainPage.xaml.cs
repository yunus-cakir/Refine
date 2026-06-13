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
        _topBarBridge.OnRightActionStateChanged += HandleRightActionStateChanged;
        _navBridge.OnLocationChanged += HandleLocationChanged;

        // Varsayılan sekmeyi ayarla (Home)
        UpdateActiveTab("");

        SetInitialShadows();

        // Trigger top bar render immediately
        HandleTopBarConfigChanged(_topBarBridge.CurrentConfig);
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

                HomeHeaderSwitchBorder.IsVisible = true;
                HomeHeaderProfileBorder.IsVisible = true;

                if (!string.IsNullOrWhiteSpace(config.Title))
                {
                    HomeHeaderLogoText.Text = config.Title.ToUpper();
                }
                else
                {
                    HomeHeaderLogoText.Text = "REFINE STUDIO";
                }
            }
            else if (config.Mode == TopBarMode.Standard)
            {
                HomeHeaderGrid.IsVisible = true;
                DetailHeaderGrid.IsVisible = false;

                HomeHeaderSwitchBorder.IsVisible = false;
                HomeHeaderProfileBorder.IsVisible = false;

                HomeHeaderLogoText.Text = config.Title?.ToUpper() ?? string.Empty;
            }
            else if (config.Mode == TopBarMode.Detail)
            {
                HomeHeaderGrid.IsVisible = false;
                DetailHeaderGrid.IsVisible = true;
                DetailHeaderTitle.Text = config.Title?.ToUpper() ?? "DETAIL";
            }

            // Sync right action visibility on config change
            DetailHeaderRightButton.Opacity = _topBarBridge.HasRightAction ? 1.0 : 0.0;
            DetailHeaderRightButton.InputTransparent = !_topBarBridge.HasRightAction;
            
            LegacyDetailHeaderRightButton.Opacity = _topBarBridge.HasRightAction ? 1.0 : 0.0;
            LegacyDetailHeaderRightButton.InputTransparent = !_topBarBridge.HasRightAction;
        });
    }

    private void HandleRightActionStateChanged(bool hasAction)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DetailHeaderRightButton.Opacity = hasAction ? 1.0 : 0.0;
            DetailHeaderRightButton.InputTransparent = !hasAction;
            
            LegacyDetailHeaderRightButton.Opacity = hasAction ? 1.0 : 0.0;
            LegacyDetailHeaderRightButton.InputTransparent = !hasAction;
        });
    }

    private void HandleLocationChanged(string activeUrl)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateActiveTab(activeUrl);
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
                    _navBridge.NavigateTo("more");
                }
                else
                {
                    _navBridge.NavigateTo(targetUrl);
                }
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

        IconMore.FontFamily = fontOutlined;
        IconMore.TextColor = inactiveColor;
        TextMore.TextColor = inactiveColor;

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
            case "more":
                IconMore.FontFamily = fontFilled;
                IconMore.TextColor = activeColor;
                TextMore.TextColor = activeColor;
                break;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Cancel the native back navigation immediately to prevent closing the app
        // while we check asynchronously with Blazor.
        _ = HandleBackAsync();
        return true; 
    }

    private async Task HandleBackAsync()
    {
        bool handled = await _navBridge.RequestHardwareBackAsync();
        
        if (!handled)
        {
            // If Blazor didn't handle it, we are at the root. Quit the app natively.
            Application.Current?.Quit();
        }
    }
}