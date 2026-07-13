namespace Endorphins.Shared;

public interface IFileStorageService
{
    Task<string?> PickProjectFolderAsync();          // returns the granted folder path
    Task<IEnumerable<string>> ListFilePathsAsync();      // relative paths in the project
    Task<string> ReadFileAsTextAsync(string relativePath);
    Task WriteFileAsync(string relativePath, string content);
}