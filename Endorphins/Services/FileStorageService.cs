using CommunityToolkit.Maui.Storage;
using Endorphins.Shared;

namespace Endorphins.Services;

public class FileStorageService : IFileStorageService
{
    private List<string> _filePaths = [];
    private string? _root;

    public async Task<string?> PickProjectFolderAsync()
    {
        var result = await MainThread.InvokeOnMainThreadAsync(() => FolderPicker.Default.PickAsync());
        if (result.IsSuccessful)
        {
            _root = result.Folder!.Path;
        }
        return _root;
    }

    public List<string> FilterBy(string[] extensions)
    {
        return _filePaths.Where(path => extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();
    }
    
    public Task SetFilePathsAsync()
    {
        EnsureRoot();
        _filePaths = Directory
            .EnumerateFiles(_root!, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_root!, path))
            .ToList();
        return Task.CompletedTask;
    }

    public Task<string> ReadFileAsTextAsync(string relativePath)
    {
        EnsureRoot();
        return File.ReadAllTextAsync(Path.Combine(_root!, relativePath));
    }    
    
    public Task<byte[]> ReadFileAsBytesAsync(string relativePath)
    {
        EnsureRoot();
        return File.ReadAllBytesAsync(Path.Combine(_root!, relativePath));
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
        if (_root is null)
        {
            throw new InvalidOperationException("No folder picked yet.");
        }
    }
}