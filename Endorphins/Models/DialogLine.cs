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

    /// <summary>
    /// Avatar for this line, as the reference the script gave — e.g.
    /// <c>"assets/images/characters/aiden.png"</c>. Null when the line has no portrait.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// <see cref="Image"/> resolved against the project root into a loadable URL, filled in
    /// when the line is created. Resolved once here rather than during rendering: the runner
    /// re-renders every line on every story step, so resolving at render time would repeat
    /// the whole project-file lookup for each line, each step.
    /// </summary>
    public string? ImageUrl { get; set; }
}