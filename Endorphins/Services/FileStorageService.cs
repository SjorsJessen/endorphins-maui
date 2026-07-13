using CommunityToolkit.Maui.Storage;
using Endorphins.Shared;

namespace Endorphins.Services;

public class FileStorageService : IFileStorageService
{
    private string? _root;

    public async Task<string?> PickProjectFolderAsync()
    {
        var result = await MainThread.InvokeOnMainThreadAsync(
            () => FolderPicker.Default.PickAsync());

        if (result.IsSuccessful)
            _root = result.Folder!.Path;
        return _root;
    }

    public Task<IEnumerable<string>> ListFilesAsync()
    {
        EnsureRoot();
        var files = Directory
            .EnumerateFiles(_root!, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_root!, p));
        return Task.FromResult(files);
    }

    public Task<string> ReadFileAsync(string relativePath)
    {
        EnsureRoot();
        return File.ReadAllTextAsync(Path.Combine(_root!, relativePath));
    }

    public Task WriteFileAsync(string relativePath, string content)
    {
        EnsureRoot();
        var full = Path.Combine(_root!, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return File.WriteAllTextAsync(full, content);   // overwrites in place
    }

    private void EnsureRoot()
    {
        if (_root is null) throw new InvalidOperationException("No folder picked yet.");
    }
}