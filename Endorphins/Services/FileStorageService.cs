using CommunityToolkit.Maui.Storage;
using Endorphins.Shared;

namespace Endorphins.Services;

public class FileStorageService : IFileStorageService
{
    public string? Root;
    public Action<List<string>>? FilesLoaded { get; set; }

    private List<string> _filePaths = [];

    /// <summary>All loaded file paths, relative to <see cref="Root"/>.</summary>
    public IReadOnlyList<string> FilePaths => _filePaths;

    public async Task<string?> PickProjectFolderAsync()
    {
        var result = await MainThread.InvokeOnMainThreadAsync(() => FolderPicker.Default.PickAsync());
        if (result.IsSuccessful)
        {
            Root = result.Folder!.Path;
        }
        return Root;
    }

    public List<string> FilterBy(string[] extensions)
    {
        return _filePaths.Where(path => extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();
    }
    
    public void SetFilePathsAsync()
    {
        EnsureRoot();
        _filePaths = Directory
            .EnumerateFiles(Root!, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root!, path))
            .ToList();

        FilesLoaded?.Invoke(_filePaths);
    }

    public Task<string> ReadFileAsTextAsync(string relativePath)
    {
        EnsureRoot();
        return File.ReadAllTextAsync(Path.Combine(Root!, relativePath));
    }    
    
    public Task<byte[]> ReadFileAsBytesAsync(string relativePath)
    {
        EnsureRoot();
        return File.ReadAllBytesAsync(Path.Combine(Root!, relativePath));
    }

    public Task WriteFileAsync(string relativePath, string content)
    {
        EnsureRoot();
        var full = Path.Combine(Root!, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return File.WriteAllTextAsync(full, content);   // overwrites in place
    }

    /// <summary>Renames/moves a file within the project. Throws if the destination already exists.</summary>
    public void RenameFile(string relativeOld, string relativeNew)
    {
        EnsureRoot();
        var oldFull = Path.Combine(Root!, relativeOld);
        var newFull = Path.Combine(Root!, relativeNew);
        Directory.CreateDirectory(Path.GetDirectoryName(newFull)!);
        File.Move(oldFull, newFull, overwrite: false);
    }

    private void EnsureRoot()
    {
        if (Root is null)
        {
            throw new InvalidOperationException("No folder picked yet.");
        }
    }
}