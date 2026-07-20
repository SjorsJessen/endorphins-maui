#if IOS || MACCATALYST
using Foundation;
#endif

namespace Endorphins.Services;

/// <summary>
/// Remembers the last opened project folder across launches.
///
/// Storing the path alone is not enough on Apple platforms: App Sandbox grants access to
/// a user-picked folder for the lifetime of the process only, so replaying that path on
/// the next launch reads back as an empty or permission-denied directory. The durable
/// form is a security-scoped bookmark, which re-grants access when resolved.
///
/// Debug builds use sandbox-free entitlements, so a path-only implementation would look
/// correct all through development and fail only in Release — the same shape of bug as
/// the missing network.server entitlement.
/// </summary>
public sealed class ProjectBookmarkStore
{
    private const string PathKey = "project.lastPath";

#if IOS || MACCATALYST
    private const string BookmarkKey = "project.lastBookmark";

    // Catalyst runs under the macOS sandbox and needs explicit security scope (and the
    // com.apple.security.files.bookmarks.app-scope entitlement). iOS has no such option;
    // its bookmarks carry scope implicitly.
#if MACCATALYST
    private const NSUrlBookmarkCreationOptions CreateOptions = NSUrlBookmarkCreationOptions.WithSecurityScope;
    private const NSUrlBookmarkResolutionOptions ResolveOptions = NSUrlBookmarkResolutionOptions.WithSecurityScope;
#else
    private const NSUrlBookmarkCreationOptions CreateOptions = NSUrlBookmarkCreationOptions.MinimalBookmark;
    private const NSUrlBookmarkResolutionOptions ResolveOptions = NSUrlBookmarkResolutionOptions.WithoutUI;
#endif

    // Held open for the life of the process: access ends when this is released, and the
    // project stays readable for as long as the app is running.
    private NSUrl? _scoped;
#endif

    /// <summary>Records <paramref name="path"/> as the project to reopen next launch.</summary>
    public void Remember(string path)
    {
        Preferences.Default.Set(PathKey, path);

#if IOS || MACCATALYST
        try
        {
            using var url = NSUrl.FromFilename(path);
            var data = url.CreateBookmarkData(CreateOptions, null, null, out var error);
            if (error is null && data is not null)
            {
                Preferences.Default.Set(BookmarkKey, data.GetBase64EncodedString(NSDataBase64EncodingOptions.None));
                return;
            }
            Console.Error.WriteLine($"[ProjectBookmark] create failed: {error?.LocalizedDescription}");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[ProjectBookmark] create threw: {e.Message}");
        }
        // Fall back to the bare path — correct when unsandboxed, and harmless otherwise
        // since Restore() verifies the directory is actually readable before returning it.
        Preferences.Default.Remove(BookmarkKey);
#endif
    }

    /// <summary>
    /// Returns the last project's path, having re-acquired access to it, or null if there
    /// is none or it is no longer reachable (moved, deleted, permission withdrawn).
    /// </summary>
    public string? Restore()
    {
#if IOS || MACCATALYST
        var encoded = Preferences.Default.Get<string?>(BookmarkKey, null);
        if (!string.IsNullOrEmpty(encoded))
        {
            try
            {
                var data = new NSData(encoded, NSDataBase64DecodingOptions.None);
                var url = NSUrl.FromBookmarkData(data, ResolveOptions, null, out var stale, out var error);
                if (error is null && url is not null && url.StartAccessingSecurityScopedResource())
                {
                    _scoped = url;
                    // A stale bookmark still resolves; refresh it so it doesn't decay further.
                    if (stale) Remember(url.Path!);
                    return Readable(url.Path);
                }
                Console.Error.WriteLine($"[ProjectBookmark] resolve failed: {error?.LocalizedDescription}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[ProjectBookmark] resolve threw: {e.Message}");
            }
        }
#endif
        return Readable(Preferences.Default.Get<string?>(PathKey, null));
    }

    /// <summary>Forgets the stored project and drops any security-scoped access.</summary>
    public void Forget()
    {
        Preferences.Default.Remove(PathKey);
#if IOS || MACCATALYST
        Preferences.Default.Remove(BookmarkKey);
        _scoped?.StopAccessingSecurityScopedResource();
        _scoped = null;
#endif
    }

    /// <summary>
    /// A path we can enumerate. Existence alone is not proof: under the sandbox an
    /// un-granted directory can report as existing and then throw or come back empty.
    /// </summary>
    private static string? Readable(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
            return path;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[ProjectBookmark] '{path}' not readable: {e.GetType().Name}");
            return null;
        }
    }
}
