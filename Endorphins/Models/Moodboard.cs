namespace Endorphins.Models;

public class MoodboardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ImageUrl { get; set; } = "";
    public double X { get; set; }        // top-left, px relative to board
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 200;
    public int ZIndex { get; set; }
    public double Rotation { get; set; } // degrees, optional
}

public class MoodboardModel
{
    public List<MoodboardItem> Items { get; set; } = new();
    public int NextZ { get; set; } = 1;
}