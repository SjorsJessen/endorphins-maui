namespace Endorphins.Models;

/// <summary>Serialized form of a diagram (.diagram file) — nodes and links by id.</summary>
public sealed class DiagramFile
{
    public List<DiagramNodeData> Nodes { get; set; } = [];
    public List<DiagramLinkData> Links { get; set; } = [];
}

public sealed class DiagramNodeData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Node";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class DiagramLinkData
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
