using Microsoft.AspNetCore.Components.WebView;

namespace Endorphins;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        blazorWebView.UrlLoading += OnUrlLoading;
    }

    // Hosts embedded as iframe tools in the workspace (Photopea, HeavyPaint).
    private static readonly string[] EmbeddedToolHosts = ["photopea.com", "heavypaint.com"];

    private static void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
    {
        // BlazorWebView opens every external URL in the system browser by
        // default — which kicks embedded tool iframes out to Safari (and leaves
        // a black pane behind, since the in-app load gets canceled).
        // Keep the embedded tools' hosts inside the WebView so they render in place.
        if (EmbeddedToolHosts.Any(h => e.Url.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase)))
        {
            e.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
        }
    }
}
