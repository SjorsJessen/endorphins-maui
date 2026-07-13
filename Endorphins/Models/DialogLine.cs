namespace Endorphins.Models;

public struct DialogLine
{
    public string Speaker { get; set; }
    public string Line { get; set; }
    public bool IsMainCharacter { get; set; }
}