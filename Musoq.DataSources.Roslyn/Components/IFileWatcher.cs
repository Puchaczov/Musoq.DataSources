using System;
using System.IO;

namespace Musoq.DataSources.Roslyn.Components;

internal interface IFileWatcher : IDisposable
{
    bool EnableRaisingEvents { get; set; }
    
    event FileSystemEventHandler Created;
    
    event FileSystemEventHandler Deleted;
    
    event RenamedEventHandler Renamed;
}
