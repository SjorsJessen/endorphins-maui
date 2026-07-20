using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Endorphins.Services;

/// <summary>
/// A tiny loopback HTTP/1.1 file server that streams project files straight to
/// the WebView by URL, so &lt;video&gt;, pdf.js and &lt;img&gt; load them natively —
/// with HTTP Range support (seeking + progressive/lazy loading) — instead of us
/// shovelling whole files across the JS-interop bridge (and base64-inflating
/// them into data: URLs).
///
/// Binds to 127.0.0.1 on an OS-assigned port. Loopback is exempt from iOS's
/// Local Network privacy prompt; the paired NSAllowsLocalNetworking entry in
/// Info.plist lets ATS permit the cleartext-to-localhost loads.
///
/// Every URL is prefixed with a per-launch random token: other local processes
/// share the loopback interface, so the token stops them enumerating project
/// files through the port.
/// </summary>
public sealed class LocalMediaServer : IDisposable
{
    private readonly FileStorageService _storage;
    private readonly TcpListener _listener;
    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly int _port;

    public LocalMediaServer(FileStorageService storage)
    {
        _storage = storage;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync();
    }

    /// <summary>Absolute URL the WebView can use to stream the given project-relative file.</summary>
    public string Url(string relativePath)
    {
        // Escape each segment but keep the slashes so the path structure survives.
        var escaped = string.Join('/', relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        var url = $"http://127.0.0.1:{_port}/{_token}/{escaped}";

        // Stamp the URL with the file's last-write time. Responses are marked immutable
        // so repeat selections are served from WebKit's cache with no round trip at all,
        // and editing the file on disk still yields a new URL rather than a stale hit.
        // (ResolvePath drops the query string, so the stamp never affects lookup.)
        var root = _storage.Root;
        if (root is not null)
        {
            var stamp = File.GetLastWriteTimeUtc(Path.Combine(root, relativePath)).Ticks;
            url += $"?v={stamp}";
        }
        return url;
    }

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                return;   // listener stopped — app shutting down
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"[LocalMediaServer] accept failed: {e.Message}");
                continue;
            }
            _ = HandleClientAsync(client);   // fire-and-forget; one connection per request
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                var (method, target, headers) = await ReadRequestAsync(stream);
                if (method is null || target is null)
                {
                    return;   // malformed / closed
                }

                if (method == "OPTIONS")
                {
                    // CORS preflight — pdf.js fetches cross-origin (app:// → http://127.0.0.1)
                    // with a Range header, which some WebKit builds preflight.
                    await WritePreflightAsync(stream);
                    return;
                }

                if (method != "GET" && method != "HEAD")
                {
                    await WriteStatusAsync(stream, 405, "Method Not Allowed");
                    return;
                }

                var path = ResolvePath(target);
                if (path is null || !File.Exists(path))
                {
                    await WriteStatusAsync(stream, 404, "Not Found");
                    return;
                }

                await ServeFileAsync(stream, path, headers, includeBody: method == "GET");
            }
            catch (IOException) { /* client hung up mid-transfer — normal for media seeking */ }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"[LocalMediaServer] request failed: {e.Message}");
            }
        }
    }

    /// <summary>Reads the request line + headers (up to the blank line).</summary>
    private static async Task<(string? Method, string? Target, Dictionary<string, string> Headers)>
        ReadRequestAsync(NetworkStream stream)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        var read = 0;

        // Read until we see the end-of-headers marker; requests are tiny so one
        // read almost always suffices, but loop to be safe.
        while (!sb.ToString().Contains("\r\n\r\n"))
        {
            var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (n == 0) return (null, null, headers);
            sb.Append(Encoding.ASCII.GetString(buffer, 0, n));
            if ((read += n) > 64 * 1024) return (null, null, headers);   // header flood guard
        }

        var lines = sb.ToString().Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2) return (null, null, headers);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0) break;
            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }
        return (requestLine[0], requestLine[1], headers);
    }

    /// <summary>Validates the token and maps the URL to an on-disk path inside the project root.</summary>
    private string? ResolvePath(string target)
    {
        var root = _storage.Root;
        if (root is null) return null;

        var pathOnly = target.Split('?', 2)[0].TrimStart('/');
        var slash = pathOnly.IndexOf('/');
        if (slash < 0) return null;

        var token = pathOnly[..slash];
        if (token != _token) return null;   // wrong/absent token → refuse

        var rel = Uri.UnescapeDataString(pathOnly[(slash + 1)..]);
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, rel));

        // Guard against ../ traversal escaping the project root.
        var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
    }

    private static async Task ServeFileAsync(NetworkStream stream, string path,
        Dictionary<string, string> headers, bool includeBody)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 16, useAsync: true);
        var total = file.Length;
        var contentType = ContentType(Path.GetExtension(path));

        long start = 0, end = total - 1;
        var partial = false;
        if (headers.TryGetValue("Range", out var range) && TryParseRange(range, total, out start, out end))
        {
            partial = true;
        }

        var length = end - start + 1;
        var head = new StringBuilder();
        head.Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n");
        head.Append($"Content-Type: {contentType}\r\n");
        head.Append($"Content-Length: {length}\r\n");
        head.Append("Accept-Ranges: bytes\r\n");
        // Safe because Url() version-stamps every URL with the file's mtime.
        head.Append("Cache-Control: public, max-age=31536000, immutable\r\n");
        // pdf.js/media fetch cross-origin from the app:// (WebView) origin.
        head.Append("Access-Control-Allow-Origin: *\r\n");
        head.Append("Access-Control-Expose-Headers: Content-Range, Content-Length, Accept-Ranges\r\n");
        if (partial)
        {
            head.Append($"Content-Range: bytes {start}-{end}/{total}\r\n");
        }
        head.Append("Connection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()));

        if (!includeBody || length <= 0) return;

        file.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[1 << 16];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var n = await file.ReadAsync(buffer.AsMemory(0, toRead));
            if (n == 0) break;
            await stream.WriteAsync(buffer.AsMemory(0, n));
            remaining -= n;
        }
    }

    /// <summary>Parses a single-range "bytes=start-end" header (the only form browsers send here).</summary>
    private static bool TryParseRange(string header, long total, out long start, out long end)
    {
        start = 0;
        end = total - 1;
        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;

        var spec = header["bytes=".Length..].Split(',')[0].Trim();
        var dash = spec.IndexOf('-');
        if (dash < 0) return false;

        var startText = spec[..dash];
        var endText = spec[(dash + 1)..];

        if (startText.Length == 0)
        {
            // Suffix range: bytes=-N → the last N bytes.
            if (!long.TryParse(endText, out var suffix) || suffix <= 0) return false;
            start = Math.Max(0, total - suffix);
            end = total - 1;
        }
        else
        {
            if (!long.TryParse(startText, out start)) return false;
            end = endText.Length == 0
                ? total - 1
                : long.TryParse(endText, out var e) ? Math.Min(e, total - 1) : total - 1;
        }
        return start <= end && start < total;
    }

    private static async Task WriteStatusAsync(NetworkStream stream, int code, string reason)
    {
        var msg = $"HTTP/1.1 {code} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(msg));
    }

    private static async Task WritePreflightAsync(NetworkStream stream)
    {
        var msg = "HTTP/1.1 204 No Content\r\n" +
                  "Access-Control-Allow-Origin: *\r\n" +
                  "Access-Control-Allow-Methods: GET, HEAD, OPTIONS\r\n" +
                  "Access-Control-Allow-Headers: Range\r\n" +
                  "Access-Control-Max-Age: 86400\r\n" +
                  "Content-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(msg));
    }

    private static string ContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".m4a" => "audio/mp4",
        ".flac" => "audio/flac",
        _ => "application/octet-stream"
    };

    public void Dispose() => _listener.Stop();
}
