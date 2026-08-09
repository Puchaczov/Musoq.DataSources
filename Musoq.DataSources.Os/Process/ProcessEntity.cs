using System;
using System.ComponentModel;
using System.IO;

namespace Musoq.DataSources.Os.Process;

/// <summary>
///     A process row with permission-sensitive values read lazily.
/// </summary>
public sealed class ProcessEntity
{
    private readonly System.Diagnostics.Process _process;

    public ProcessEntity(System.Diagnostics.Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public int BasePriority => _process.BasePriority;
    public bool EnableRaisingEvents => _process.EnableRaisingEvents;
    public int ExitCode => _process.ExitCode;
    public DateTime ExitTime => _process.ExitTime;
    public IntPtr Handle => _process.Handle;
    public int HandleCount => _process.HandleCount;
    public bool HasExited => _process.HasExited;
    public int Id => _process.Id;
    public string MachineName => _process.MachineName;
    public string MainWindowTitle => _process.MainWindowTitle;
    public long PagedMemorySize64 => _process.PagedMemorySize64;
    public string ProcessName => _process.ProcessName;

    public IntPtr? ProcessorAffinity
    {
        get
        {
            try
            {
                return _process.ProcessorAffinity;
            }
            catch (Win32Exception)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public bool Responding => _process.Responding;
    public DateTime StartTime => _process.StartTime;
    public TimeSpan TotalProcessorTime => _process.TotalProcessorTime;
    public TimeSpan UserProcessorTime => _process.UserProcessorTime;

    public string Directory
    {
        get
        {
            try
            {
                return new FileInfo(_process.MainModule!.FileName).Directory!.FullName;
            }
            catch (Exception)
            {
                return "None";
            }
        }
    }

    public string FileName
    {
        get
        {
            try
            {
                return new FileInfo(_process.MainModule!.FileName).Name;
            }
            catch (Exception)
            {
                return "None";
            }
        }
    }
}
