namespace Endorphins.Services;

public sealed class DiagramService
{
    public string? ActiveDiagramPath { get; set; }

    /// <summary>Raised with (relativePath, jsonContent) when a diagram file is opened for editing.</summary>
    public Action<string, string>? DiagramFileSelected { get; set; }
}
