using Musoq.DataSources.Roslyn.Components;

namespace Musoq.DataSources.Roslyn.Tests.Components;

public class MockFileWatcher : IFileWatcher
{
    public bool EnableRaisingEvents { get; set; }

    public event FileSystemEventHandler? Created;
    public event FileSystemEventHandler? Deleted;
    public event RenamedEventHandler? Renamed;

    public void Dispose()
    {
        Created = null;
        Deleted = null;
        Renamed = null;
    }

    public void SimulateFileCreated(string path)
    {
        if (!EnableRaisingEvents) return;


        var fileName = Path.GetFileName(path);
        var dirName = Path.GetDirectoryName(path) ?? string.Empty;

        var args = new FileSystemEventArgs(
            WatcherChangeTypes.Created,
            dirName,
            fileName);

        Console.WriteLine($"MockFileWatcher: Simulating file created event for {fileName} in {dirName}");
        Created?.Invoke(this, args);
    }

    public void SimulateFileDeleted(string path)
    {
        if (!EnableRaisingEvents) return;

        var args = new FileSystemEventArgs(
            WatcherChangeTypes.Deleted,
            Path.GetDirectoryName(path) ?? string.Empty,
            Path.GetFileName(path));

        Deleted?.Invoke(this, args);
    }

    public void SimulateFileRenamed(string oldPath, string newPath)
    {
        if (!EnableRaisingEvents) return;

        var args = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            Path.GetDirectoryName(newPath) ?? string.Empty,
            Path.GetFileName(newPath),
            Path.GetFileName(oldPath));

        Renamed?.Invoke(this, args);
    }
}