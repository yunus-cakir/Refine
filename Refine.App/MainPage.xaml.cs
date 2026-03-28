using Refine.App.Services;
using Microsoft.Maui.Controls.Shapes; // Path Geometry için gerekli

namespace Refine.App;

public partial class MainPage : ContentPage
{
    private readonly NavigationBridge _navBridge;
    private readonly PathGeometryConverter _converter = new PathGeometryConverter();

    public MainPage(NavigationBridge navBridge)
    {
        InitializeComponent();
        _navBridge = navBridge;

        // --- İLK AÇILIŞ AYARLARI ---

        // 1. Home İkonunu Yükle (Sabit olduğu için buraya ekledik)
        PathHome.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Home_Filled)!;

        // 2. Diğer sekmeleri varsayılan (Home seçili) hale getir
        UpdateActiveTab("");
    }

    private void OnNavTapped(object? sender, EventArgs e)
    {
        if (sender is View view && view.GestureRecognizers is not null && view.GestureRecognizers.Count > 0)
        {
            if (view.GestureRecognizers[0] is TapGestureRecognizer tap)
            {
                string targetUrl = tap.CommandParameter?.ToString() ?? "";

                // Url boş gelirse Home demektir
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

        // 1. Önce hepsini SIFIRLA (Outline İkon + Soluk Renk)

        PathWorkouts.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Workout_Outline)!;
        PathWorkouts.Fill = inactiveColor;
        TextWorkouts.TextColor = inactiveColor;

        PathPrograms.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Program_Outline)!;
        PathPrograms.Fill = inactiveColor;
        TextPrograms.TextColor = inactiveColor;

        PathExercises.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Exercise_Outline)!;
        PathExercises.Fill = inactiveColor;
        TextExercises.TextColor = inactiveColor;

        PathProfile.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Profile_Outline)!;
        PathProfile.Fill = inactiveColor;
        TextProfile.TextColor = inactiveColor;

        // 2. Seçili olanı AKTİF YAP (Filled İkon + Neon Renk)
        switch (activeUrl)
        {
            case "workouts":
                PathWorkouts.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Workout_Filled)!;
                PathWorkouts.Fill = activeColor;
                TextWorkouts.TextColor = activeColor;
                break;

            case "programs":
                PathPrograms.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Program_Filled)!;
                PathPrograms.Fill = activeColor;
                TextPrograms.TextColor = activeColor;
                break;

            case "exercises":
                PathExercises.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Exercise_Filled)!;
                PathExercises.Fill = activeColor;
                TextExercises.TextColor = activeColor;
                break;

            case "profile":
                PathProfile.Data = (Geometry)_converter.ConvertFromInvariantString(IconPaths.Profile_Filled)!;
                PathProfile.Fill = activeColor;
                TextProfile.TextColor = activeColor;
                break;

            default:
                // Home (Orta Buton) zaten sabittir, değişmez.
                break;
        }
    }
}