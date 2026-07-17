namespace Endorphins.Shared;

public interface IFileStorageService
{
    Task<string?> PickProjectFolderAsync();
    Task<string> ReadFileAsTextAsync(string relativePath);
    Task WriteFileAsync(string relativePath, string content);
    void SetFilePathsAsync();
}