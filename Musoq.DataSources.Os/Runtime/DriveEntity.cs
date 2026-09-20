using System;
using System.IO;

namespace Musoq.DataSources.Os.Runtime;

public sealed class DriveEntity
{
    public DriveEntity(DriveInfo drive)
    {
        Name = drive.Name;
        DriveType = drive.DriveType.ToString();
        IsReady = drive.IsReady;
        RootDirectory = drive.RootDirectory.FullName;

        if (!IsReady)
            return;

        DriveFormat = TryRead(() => drive.DriveFormat);
        AvailableFreeSpace = TryRead(() => drive.AvailableFreeSpace);
        TotalFreeSpace = TryRead(() => drive.TotalFreeSpace);
        TotalSize = TryRead(() => drive.TotalSize);
    }

    public string Name { get; }
    public string DriveType { get; }
    public string? DriveFormat { get; }
    public bool IsReady { get; }
    public long? AvailableFreeSpace { get; }
    public long? TotalFreeSpace { get; }
    public long? TotalSize { get; }
    public string RootDirectory { get; }

    private static T? TryRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }
}
