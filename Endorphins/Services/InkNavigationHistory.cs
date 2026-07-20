namespace Endorphins.Services;

/// <summary>
/// Back/forward history over the ink scripts opened in the editor, with browser semantics:
/// visiting a script while stepped back discards whatever was ahead.
///
/// Entries are project-relative paths rather than content, so a script edited since it was
/// last visited comes back in its current state. Paths can also go stale (renamed, deleted),
/// which is why stepping is driven through <see cref="Back"/>/<see cref="Forward"/> taking a
/// predicate: unreachable entries are dropped as they are encountered instead of dead-ending
/// the buttons.
/// </summary>
public sealed class InkNavigationHistory
{
    private readonly List<string> _entries = [];
    private int _index = -1;

    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;

    public string? Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

    /// <summary>
    /// Records a script the user opened directly. Re-opening the current script is ignored so
    /// repeated clicks on the same file don't pad the history with duplicates.
    /// </summary>
    public void Visit(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        relativePath = Normalize(relativePath);
        if (Same(Current, relativePath)) return;

        // Drop the forward branch — the user has chosen a different path from here.
        if (_index < _entries.Count - 1)
        {
            _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        }
        _entries.Add(relativePath);
        _index = _entries.Count - 1;
    }

    /// <summary>Steps back to the nearest entry satisfying <paramref name="isReachable"/>.</summary>
    public string? Back(Func<string, bool> isReachable) => Step(-1, isReachable);

    /// <summary>Steps forward to the nearest entry satisfying <paramref name="isReachable"/>.</summary>
    public string? Forward(Func<string, bool> isReachable) => Step(+1, isReachable);

    private string? Step(int direction, Func<string, bool> isReachable)
    {
        var cursor = _index;
        while (true)
        {
            cursor += direction;
            if (cursor < 0 || cursor >= _entries.Count) return null;

            if (isReachable(_entries[cursor]))
            {
                _index = cursor;
                return _entries[cursor];
            }

            // Gone for good: forget it and keep looking in the same direction. Removing an
            // entry before the cursor shifts everything down, so compensate.
            _entries.RemoveAt(cursor);
            if (cursor <= _index) _index--;
            if (direction > 0) cursor--;
        }
    }

    /// <summary>Keeps history pointing at a script that was renamed in place.</summary>
    public void Rename(string oldPath, string newPath)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (Same(_entries[i], oldPath)) _entries[i] = newPath;
        }
    }

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Canonical form of a project-relative path. Entries reach us from three places — the
    /// asset tree, the main-script constant and INCLUDE text — which can spell the same file
    /// differently; without this, one file could occupy several history slots.
    /// </summary>
    private static string Normalize(string relativePath)
    {
        var segments = new List<string>();
        foreach (var segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    break;
                case ".." when segments.Count > 0 && segments[^1] != "..":
                    segments.RemoveAt(segments.Count - 1);
                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }
        return string.Join('/', segments);
    }
}
