using Refine.App.Services;

namespace Refine.App;

public partial class MainPage : ContentPage
{
    private readonly NavigationBridge _navBridge;
    private bool _isSwitchLeftActive = true;

    public MainPage(NavigationBridge navBridge)
    {
        InitializeComponent();
        _navBridge = navBridge;

        // Varsayılan sekmeyi ayarla (Home)
        UpdateActiveTab("");
    }

    private void OnSwitchTapped(object? sender, EventArgs e)
    {
        _isSwitchLeftActive = !_isSwitchLeftActive;
        
        if (_isSwitchLeftActive)
        {
            // Left is Blue, Right is Gray
            SwitchLeftCircle.BackgroundColor = Color.FromArgb("#3b82f6"); 
            SwitchRightCircle.BackgroundColor = Color.FromArgb("#3f3f46"); 

            if (SwitchLeftCircle.Shadow is Shadow leftShadow)
            {
                leftShadow.Brush = new SolidColorBrush(Color.FromArgb("#3b82f6"));
                leftShadow.Opacity = 0.5f;
            }

            if (SwitchRightCircle.Shadow is Shadow rightShadow)
            {
                rightShadow.Opacity = 0f;
            }
        }
        else
        {
            // Left is Gray, Right is Green
            SwitchLeftCircle.BackgroundColor = Color.FromArgb("#3f3f46"); 
            SwitchRightCircle.BackgroundColor = Color.FromArgb("#22c55e"); 

            if (SwitchLeftCircle.Shadow is Shadow leftShadow)
            {
                leftShadow.Opacity = 0f;
            }

            if (SwitchRightCircle.Shadow is Shadow rightShadow)
            {
                rightShadow.Brush = new SolidColorBrush(Color.FromArgb("#22c55e"));
                rightShadow.Opacity = 0.5f;
            }
        }
    }

    private void OnNavTapped(object? sender, EventArgs e)
    {
        if (sender is View view && view.GestureRecognizers is not null && view.GestureRecognizers.Count > 0)
        {
            if (view.GestureRecognizers[0] is TapGestureRecognizer tap)
            {
                string targetUrl = tap.CommandParameter?.ToString() ?? "";
                _navBridge.NavigateTo(targetUrl);
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
                IconPreferences.FontFamily = fontFilled;
                IconPreferences.TextColor = activeColor;
                TextPreferences.TextColor = activeColor;
                break;
        }
    }
}