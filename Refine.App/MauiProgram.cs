using Microsoft.Extensions.Logging;
using Refine.App.Services; // Servislerin olduğu namespace
using DotNet.Meteor.HotReload.Plugin;

namespace Refine.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Variable.ttf", "Inter");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialFilled");
                fonts.AddFont("MaterialSymbolsOutlined.ttf", "MaterialOutlined");
            });

        // --- KRİTİK BÖLÜM: SERVİS KAYITLARI ---
        // Bu satırlar olmazsa uygulama "Inject" aşamasında çöker.

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.EnableHotReload();
#endif

        // Veritabanı Servisi
        builder.Services.AddSingleton<LocalDbService>();

        builder.Services.AddSingleton<NavigationBridge>();
        builder.Services.AddSingleton<TopBarBridge>();
        builder.Services.AddSingleton<MainPage>();

        builder.Services.AddSingleton<AnalyticsService>();
        builder.Services.AddSingleton<RecoveryService>();
        builder.Services.AddSingleton<HomeStateService>();

        // Kullanıcı Servisi (Home.razor'ın çökme sebebi muhtemelen buydu)
        //builder.Services.AddSingleton<UserService>();

        builder.Services.AddSingleton<WorkoutEditorState>();
        builder.Services.AddSingleton<SelectionStateService>();
        builder.Services.AddScoped<IPopupService, PopupService>();

        // Eğer kullanıyorsan diğer servisler:
        // builder.Services.AddSingleton<ExerciseService>();
        // builder.Services.AddSingleton<WorkoutProgramService>();

        return builder.Build();
    }
}