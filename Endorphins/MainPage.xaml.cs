using Microsoft.AspNetCore.Components.WebView;

namespace Endorphins;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        blazorWebView.UrlLoading += OnUrlLoading;
    }

    private static void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
    {
        // BlazorWebView opens every external URL in the system browser by
        // default — which kicks the embedded Photopea iframe out to Safari.
        // Keep photopea.com inside the WebView so it renders embedded.
        if (e.Url.Host.EndsWith("photopea.com", StringComparison.OrdinalIgnoreCase))
        {
            e.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
        }
    }
}
