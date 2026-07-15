using Microsoft.AspNetCore.Components.Forms;

namespace Endorphins.Services;

public sealed class AssetsService
{
    public Action<string, string>? MarkdownFileSelected { get; set; }
    public Action<string>? VideoSelected { get; set; }
    
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