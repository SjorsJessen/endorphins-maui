namespace Endorphins.Services;

/// <summary>
/// Turns an asset reference written by hand in an ink script — <c>"assets/images/characters/aiden.png"</c>,
/// or just <c>"aiden.png"</c> — into a URL the WebView can load from the project folder.
///
/// Results are cached because the underlying lookup is a linear scan of the project's file
/// list plus a stat for the media server's cache-busting stamp, and callers resolve the same
/// handful of names repeatedly (every character portrait, every line). The cache is dropped
/// whenever the project's files are re-enumerated, which is when a rename or a new asset
/// could change the answer.
/// </summary>
public sealed class AssetUrlResolver
{
    private readonly FileStorageService _storage;
    private readonly LocalMediaServer _media;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AssetUrlResolver(FileStorageService storage, LocalMediaServer media)
    {
        _storage = storage;
        _media = media;
        _storage.FilesLoaded += _ => Invalidate();
    }

    /// <summary>
    /// The loopback URL for <paramref name="nameOrPath"/>, or null when the project holds no
    /// such file. Null is a normal outcome — scripts outlive the assets they name — so callers
    /// should hide the image rather than render a broken one.
    /// </summary>
    public string? Url(string? nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath)) return null;

        var key = nameOrPath.Trim();
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var path = _storage.FindFile(key);
        var url = path is null ? null : _media.Url(path);
        if (url is null)
        {
            Console.Error.WriteLine($"[AssetUrl] '{key}' — no such file in the project.");
        }
        return _cache[key] = url;
    }

    public void Invalidate() => _cache.Clear();
}
