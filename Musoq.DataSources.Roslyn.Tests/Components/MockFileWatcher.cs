using Musoq.DataSources.Roslyn.Components;
using System.IO;

namespace Musoq.DataSources.Roslyn.Tests.Components;

public class MockFileWatcher : IFileWatcher
{
    private bool _enableRaisingEvents;
    
    public bool EnableRaisingEvents 
    {
        get => _enableRaisingEvents;
        set => _enableRaisingEvents = value;
    }
    
    public event FileSystemEventHandler? Created;
    public event FileSystemEventHandler? Deleted;
    public event RenamedEventHandler? Renamed;
    
    public void SimulateFileCreated(string path)
    {
        if (!_enableRaisingEvents) return;
        
        // Extract just the filename, not full path
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
        if (!_enableRaisingEvents) return;
        
        var args = new FileSystemEventArgs(
            WatcherChangeTypes.Deleted,
            Path.GetDirectoryName(path) ?? string.Empty,
            Path.GetFileName(path));
            
        Deleted?.Invoke(this, args);
    }
    
    public void SimulateFileRenamed(string oldPath, string newPath)
    {
        if (!_enableRaisingEvents) return;
        
        var args = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            Path.GetDirectoryName(newPath) ?? string.Empty,
            Path.GetFileName(newPath),
            Path.GetFileName(oldPath));
            
        Renamed?.Invoke(this, args);
    }
    
    public void Dispose()
    {
        // No resources to dispose in the mock
        Created = null;
        Deleted = null;
        Renamed = null;
    }
}
