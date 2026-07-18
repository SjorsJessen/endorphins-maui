namespace Endorphins.Models;

public enum DialogType { 
    Main, Npc, Narrator, Title, Divider 
}

public interface IDialogLine
{
    public DialogType Type { get; set; }
}

public class DialogLine : IDialogLine
{
    public string Speaker { get; set; }
    public string Line { get; set; }
    public DialogType Type { get; set; }
}