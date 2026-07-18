namespace Endorphins.Models;

/// <summary>A bookmark/outline entry in a PDF, with its resolved page number and children.</summary>
public sealed class PdfOutlineNode
{
    public string Title { get; set; } = string.Empty;
    public int? Page { get; set; }
    public List<PdfOutlineNode> Items { get; set; } = [];
}
