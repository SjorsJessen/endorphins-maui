using CommunityToolkit.Maui;
using Endorphins.Services;
using Endorphins.Shared;
using Microsoft.AspNetCore.Components.WebView.Maui;
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
        builder.Services.AddSingleton<InkLinkResolver>();
        builder.Services.AddSingleton<EditorService>();
        builder.Services.AddSingleton<ProjectBookmarkStore>();
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
#if IOS || MACCATALYST
        // Saving from an embedded tool navigates to a data:/blob: URL far longer than System.Uri
        // accepts, which crashes BlazorWebView's navigation delegate. See WebViewDownloadInterceptor.
        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping(
            "InterceptDownloads", (handler, view) =>
            {
                WebViewDownloadInterceptor.InstallOn(handler.PlatformView);
            });
#endif
        return builder.Build();
    }
}