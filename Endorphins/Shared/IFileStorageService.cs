namespace Endorphins.Shared;

public interface IFileStorageService
{
    Task<string?> PickProjectFolderAsync();          // returns the granted folder path
    Task<IEnumerable<string>> ListFilesAsync();      // relative paths in the project
    Task<string> ReadFileAsync(string relativePath);
    Task WriteFileAsync(string relativePath, string content);
}