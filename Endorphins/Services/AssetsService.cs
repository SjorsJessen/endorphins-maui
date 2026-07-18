using Microsoft.AspNetCore.Components.Forms;

namespace Endorphins.Services;

public sealed class AssetsService
{
    public Action<string, string>? MarkdownFileSelected { get; set; }

    /// <summary>Raised as the markdown editor content changes, for live preview.</summary>
    public Action<string>? MarkdownContentChanged { get; set; }

    // The currently-selected markdown note. Persisted here (singleton) so the
    // editor/preview components, which are created lazily when the Markdown editor
    // and Notes tab first appear, can initialise from it — the selection event
    // fires before those components exist on the very first open.
    public string? ActiveMarkdownPath { get; private set; }
    public string ActiveMarkdownContent { get; private set; } = string.Empty;

    /// <summary>Records the active note and notifies listeners it was selected.</summary>
    public void SelectMarkdown(string path, string content)
    {
        ActiveMarkdownPath = path;
        ActiveMarkdownContent = content;
        MarkdownFileSelected?.Invoke(path, content);
    }

    /// <summary>Records edited note content and notifies the live preview.</summary>
    public void UpdateMarkdownContent(string content)
    {
        ActiveMarkdownContent = content;
        MarkdownContentChanged?.Invoke(content);
    }

    public Action<string>? VideoSelected { get; set; }

    /// <summary>The currently-selected video, persisted for the lazily-created player (see markdown note above).</summary>
    public string? ActiveVideoPath { get; private set; }

    /// <summary>Records the active video and notifies listeners it was selected.</summary>
    public void SelectVideo(string path)
    {
        ActiveVideoPath = path;
        VideoSelected?.Invoke(path);
    }

    /// <summary>Raised when the user launches the Photopea tool from the asset panel.</summary>
    public Action? PhotoshopRequested { get; set; }
    
    private List<IBrowserFile> Assets { get; } = [];
    
    public List<IBrowserFile> FilterBy(string[] extensions)
    {
        return Assets.Where(file => extensions.Any(ext => file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public void AddAssets(IReadOnlyList<IBrowserFile> assets)
    {
        Assets.AddRange(assets);
    }

    public void ResetAssets()
    {
        Assets.Clear();
    }
}