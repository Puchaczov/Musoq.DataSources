using System.IO;

namespace Musoq.DataSources.Roslyn.Components;

internal class DefaultFileWatcher(string path, string filter = "*.*", bool includeSubdirectories = false)
    : IFileWatcher
{
    private readonly FileSystemWatcher _fileSystemWatcher = new(path)
    {
        Filter = filter,
        IncludeSubdirectories = includeSubdirectories,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        InternalBufferSize = 65536
    };

    public bool EnableRaisingEvents
    {
        get => _fileSystemWatcher.EnableRaisingEvents;
        set => _fileSystemWatcher.EnableRaisingEvents = value;
    }

    public event FileSystemEventHandler Created
    {
        add => _fileSystemWatcher.Created += value;
        remove => _fileSystemWatcher.Created -= value;
    }

    public event FileSystemEventHandler Deleted
    {
        add => _fileSystemWatcher.Deleted += value;
        remove => _fileSystemWatcher.Deleted -= value;
    }

    public event RenamedEventHandler Renamed
    {
        add => _fileSystemWatcher.Renamed += value;
        remove => _fileSystemWatcher.Renamed -= value;
    }

    public void Dispose()
    {
        _fileSystemWatcher.Dispose();
    }
}