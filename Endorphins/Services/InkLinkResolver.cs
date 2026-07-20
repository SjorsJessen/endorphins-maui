using System.Text.RegularExpressions;

namespace Endorphins.Services;

/// <summary>Where an ink link points: a file, and a 1-based caret position within it.</summary>
public sealed record InkLinkTarget(string RelativePath, int Line, int Column);

/// <summary>
/// Resolves the targets of ink diverts and INCLUDEs to a file and line, so the editor can
/// jump to them.
///
/// This mirrors the compiler's view rather than inventing its own: INCLUDE paths resolve
/// against the project root exactly as <see cref="ProjectFileHandler.ResolveInkFilename"/>
/// does, so a link that navigates is a link the compiler can also follow.
/// </summary>
public sealed partial class InkLinkResolver(FileStorageService storage)
{
    // === knot ===, or === function name(args) ===
    [GeneratedRegex(@"^\s*={2,}\s*(?:function\s+)?([A-Za-z_]\w*)", RegexOptions.Multiline)]
    private static partial Regex KnotPattern();

    // = stitch  (a single leading =, not part of a knot header)
    [GeneratedRegex(@"^\s*=(?!=)\s*(?:function\s+)?([A-Za-z_]\w*)", RegexOptions.Multiline)]
    private static partial Regex StitchPattern();

    // - (label), * (label), + (label)
    [GeneratedRegex(@"^\s*[-*+]+\s*\(\s*([A-Za-z_]\w*)\s*\)", RegexOptions.Multiline)]
    private static partial Regex LabelPattern();

    /// <summary>
    /// Resolves an INCLUDE target. <paramref name="includeName"/> is the raw text after the
    /// INCLUDE keyword; it is project-root relative, matching the compiler's file handler.
    /// </summary>
    public InkLinkTarget? ResolveInclude(string includeName)
    {
        if (storage.Root is null) return null;

        var relative = includeName.Trim().Replace('\\', '/');
        if (relative.Length == 0) return null;

        var full = Path.GetFullPath(Path.Combine(storage.Root, relative));
        return File.Exists(full) ? new InkLinkTarget(relative, 1, 1) : null;
    }

    /// <summary>
    /// Resolves a divert target such as <c>knot</c>, <c>knot.stitch</c> or <c>stitch.label</c>.
    /// Searches <paramref name="activeScriptPath"/> first so a local definition wins over a
    /// same-named one elsewhere, then the rest of the project's .ink files.
    /// </summary>
    public InkLinkTarget? ResolveDivert(string target, string activeScriptPath, string activeContent)
    {
        var segments = target.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        if (FindPath(activeContent, segments) is { } local)
        {
            return new InkLinkTarget(activeScriptPath, local.Line, local.Column);
        }

        foreach (var path in InkFilesExcept(activeScriptPath))
        {
            string content;
            try
            {
                content = File.ReadAllText(Path.Combine(storage.Root!, path));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[InkLink] cannot read '{path}': {e.GetType().Name}");
                continue;
            }

            if (FindPath(content, segments) is { } found)
            {
                return new InkLinkTarget(path, found.Line, found.Column);
            }
        }
        return null;
    }

    private IEnumerable<string> InkFilesExcept(string activeScriptPath)
    {
        if (storage.Root is null) return [];
        var active = activeScriptPath.Replace('\\', '/');
        return storage.FilePaths
            .Where(p => p.EndsWith(".ink", StringComparison.OrdinalIgnoreCase))
            .Where(p => !string.Equals(p.Replace('\\', '/'), active, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Walks a dotted path. Each segment must be defined after the previous one — that is how
    /// ink scopes stitches and labels to their enclosing knot — so <c>cave.entrance</c> finds
    /// the <c>entrance</c> stitch belonging to <c>cave</c> rather than a same-named stitch in
    /// some other knot.
    /// </summary>
    private static (int Line, int Column)? FindPath(string content, string[] segments)
    {
        var offset = 0;
        (int Line, int Column)? result = null;

        foreach (var segment in segments)
        {
            var match = FindDefinition(content, segment, offset);
            if (match is null) return null;
            offset = match.Value.Index;
            result = ToLineColumn(content, match.Value.NameIndex);
        }
        return result;
    }

    /// <summary>Finds where <paramref name="name"/> is defined at or after <paramref name="from"/>.</summary>
    private static (int Index, int NameIndex)? FindDefinition(string content, string name, int from)
    {
        (int Index, int NameIndex)? best = null;

        foreach (var regex in new[] { KnotPattern(), StitchPattern(), LabelPattern() })
        {
            foreach (Match m in regex.Matches(content, from))
            {
                if (m.Groups[1].Value != name) continue;
                if (best is null || m.Index < best.Value.Index)
                {
                    best = (m.Index, m.Groups[1].Index);
                }
                break;   // matches come in order; the first hit per pattern is the earliest
            }
        }
        return best;
    }

    private static (int Line, int Column) ToLineColumn(string content, int index)
    {
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }
        return (line, index - lineStart + 1);
    }
}
