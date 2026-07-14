namespace Endorphins.Shared;

public interface IFileStorageService
{
    Task<string?> PickProjectFolderAsync();          // returns the granted folder path
    Task SetFilePathsAsync();
    Task<string> ReadFileAsTextAsync(string relativePath);
    Task WriteFileAsync(string relativePath, string content);
}