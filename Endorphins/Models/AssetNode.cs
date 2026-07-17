namespace Endorphins.Models;

/// <summary>
/// A node in the asset tree — either a folder (with children) or a file.
/// Built from the flat relative paths reported by the storage service so the
/// panel mirrors the project's real folder nesting at any depth.
/// </summary>
public sealed class AssetNode
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Relative path from the project root (folders included).</summary>
    public string Path { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public List<AssetNode> Children { get; } = [];
}
