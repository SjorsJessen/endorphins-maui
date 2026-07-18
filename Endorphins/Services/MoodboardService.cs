namespace Endorphins.Services;

public sealed class MoodboardService
{
    public string? ActiveMoodboardPath { get; set; }

    /// <summary>Raised with (relativePath, jsonContent) when a moodboard file is opened for editing.</summary>
    public Action<string, string>? MoodboardFileSelected { get; set; }
}
