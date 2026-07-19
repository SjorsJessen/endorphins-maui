using CommunityToolkit.Maui;
using Endorphins.Services;
using Endorphins.Shared;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Endorphins;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => { 
                fonts.AddFont("RobotoMono.ttf", "RobotoMono"); 
                fonts.AddFont("Horizon.otf", "Horizon"); 
                fonts.AddFont("Inter.ttf", "Inter"); 
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<AssetsService>();
        builder.Services.AddSingleton<PdfService>();
        builder.Services.AddSingleton<InkStoryService>();
        builder.Services.AddSingleton<EditorService>();
        builder.Services.AddSingleton<FileStorageService>();
        builder.Services.AddSingleton<LocalMediaServer>();
        builder.Services.AddSingleton<DiagramService>();
        builder.Services.AddSingleton<MoodboardService>();
        builder.Services.AddMudServices();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
#if DEBUG && (IOS || MACCATALYST)
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping(
            "Inspectable", (handler, view) =>
            {
                handler.PlatformView.Inspectable = true;
            });
#endif
        return builder.Build();
    }
}