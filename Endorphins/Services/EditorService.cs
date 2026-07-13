namespace Endorphins.Services;

public sealed class EditorService
{
    public EditorType ActiveEditor { get; private set; } = EditorType.Ink;
    public event Action? EditorChanged;

    public void SetEditor(EditorType type)
    {
        ActiveEditor = type;
        EditorChanged?.Invoke();
    }
}

public enum EditorType
{
    Ink = 1,
    Markdown = 2
}