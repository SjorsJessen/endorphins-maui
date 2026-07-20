#if IOS || MACCATALYST
using Foundation;
using ObjCRuntime;
using UIKit;
using WebKit;

namespace Endorphins.Services;

/// <summary>
/// BlazorWebView's own navigation delegate builds a <see cref="Uri"/> for every navigation so it
/// can raise UrlLoading. The embedded tools (HeavyPaint, Photopea) save by navigating to a
/// data:/blob: URL that carries the whole image inline, which blows past Uri's 65519-character
/// limit — and the UriFormatException escapes through the Objective-C callback, killing the app
/// at Program.Main rather than at anything resembling the call site.
///
/// So this proxy sits in front of MAUI's delegate: download-shaped navigations go to WebKit's
/// downloader (which never constructs a Uri) and everything else is forwarded on untouched.
/// </summary>
internal sealed class WebViewDownloadInterceptor : NSObject, IWKNavigationDelegate, IWKDownloadDelegate
{
    // Both WKWebView.NavigationDelegate and WKDownload.Delegate are weak references, so nothing
    // else keeps an interceptor alive once the handler has finished building the view.
    private static readonly List<WebViewDownloadInterceptor> Installed = [];

    private readonly NSObject _inner;
    private readonly Dictionary<IntPtr, string> _destinations = [];

    private WebViewDownloadInterceptor(NSObject inner) => _inner = inner;

    /// <summary>Wraps <paramref name="webView"/>'s existing navigation delegate. Idempotent.</summary>
    public static void InstallOn(WKWebView webView)
    {
        if (webView.NavigationDelegate is not NSObject inner || inner is WebViewDownloadInterceptor)
        {
            return;
        }

        var interceptor = new WebViewDownloadInterceptor(inner);
        Installed.Add(interceptor);
        webView.NavigationDelegate = interceptor;
    }

    [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
    public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
    {
        // Read only the scheme — touching AbsoluteString here would cost a multi-megabyte string
        // copy for exactly the navigations we are trying to keep out of managed code.
        if (navigationAction.Request.Url?.Scheme is "data" or "blob")
        {
            decisionHandler(WKNavigationActionPolicy.Download);
            return;
        }

        ((IWKNavigationDelegate)_inner).DecidePolicy(webView, navigationAction, decisionHandler);
    }

    [Export("webView:navigationAction:didBecomeDownload:")]
    public void NavigationActionDidBecomeDownload(WKWebView webView, WKNavigationAction navigationAction, WKDownload download)
        => download.Delegate = this;

    [Export("webView:navigationResponse:didBecomeDownload:")]
    public void NavigationResponseDidBecomeDownload(WKWebView webView, WKNavigationResponse navigationResponse, WKDownload download)
        => download.Delegate = this;

    [Export("download:decideDestinationUsingResponse:suggestedFilename:completionHandler:")]
    public void DecideDestination(WKDownload download, NSUrlResponse response, string suggestedFilename, Action<NSUrl> completionHandler)
    {
        // WKDownload refuses a destination that already exists, so stage each one in its own
        // scratch directory. The app sandbox always allows writes here; the user's chosen
        // location is reached afterwards through the document picker.
        var name = string.IsNullOrWhiteSpace(suggestedFilename) ? "download" : Path.GetFileName(suggestedFilename);
        var staging = Path.Combine(Path.GetTempPath(), "downloads", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(staging);

        var destination = Path.Combine(staging, name);
        _destinations[(IntPtr)download.Handle] = destination;
        completionHandler(NSUrl.FromFilename(destination));
    }

    [Export("downloadDidFinish:")]
    public void DidFinish(WKDownload download)
    {
        if (!_destinations.Remove((IntPtr)download.Handle, out var destination))
        {
            return;
        }

        // Export-mode picker: the one file-writing path open to us under App Sandbox with only
        // com.apple.security.files.user-selected.read-write (see Entitlements.plist).
        var picker = new UIDocumentPickerViewController([NSUrl.FromFilename(destination)], asCopy: true);
        var scene = UIApplication.SharedApplication.ConnectedScenes.ToArray().OfType<UIWindowScene>().FirstOrDefault();
        var root = scene?.Windows.FirstOrDefault(w => w.IsKeyWindow)?.RootViewController;
        root?.PresentViewController(picker, animated: true, completionHandler: null);
    }

    [Export("download:didFailWithError:resumeData:")]
    public void DidFailWithError(WKDownload download, NSError error, NSData? resumeData)
        => _destinations.Remove((IntPtr)download.Handle);

    // Everything we do not implement above — DidFinishNavigation, DidCommitNavigation, the
    // auth-challenge callbacks — must still reach MAUI's delegate, or BlazorWebView never learns
    // its page loaded. Claiming the selector and then forwarding is the standard proxy pattern:
    // RespondsToSelector gets WebKit to make the call, and dispatch failure routes it to _inner.
    public override bool RespondsToSelector(Selector? sel)
        => base.RespondsToSelector(sel) || _inner.RespondsToSelector(sel);

    [Export("forwardingTargetForSelector:")]
    public NSObject ForwardingTargetForSelector(Selector sel) => _inner;
}
#endif
