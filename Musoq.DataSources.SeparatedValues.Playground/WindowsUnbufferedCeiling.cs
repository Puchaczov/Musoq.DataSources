#nullable enable

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Musoq.DataSources.SeparatedValues.Playground;

internal static class WindowsUnbufferedCeiling
{
    private const int BlockSize = 4 * 1024 * 1024;
    private const int Alignment = 4096;
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileBegin = 0;
    private const uint FileFlagNoBuffering = 0x20000000;

    public static UnbufferedResult Read(string path, int concurrency)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Unbuffered ceiling reads are available only on Windows.");
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
        var length = new FileInfo(path).Length / BlockSize * BlockSize;
        if (length == 0)
            throw new InvalidOperationException("The raw-ceiling fixture must contain at least one 4 MiB block.");

        var lanes = new Task<long>[concurrency];
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var capturedLane = lane;
            lanes[lane] = Task.Factory.StartNew(
                () => ReadLane(path, length, capturedLane, concurrency),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        Task.WaitAll(lanes);
        return new UnbufferedResult(length, lanes.Sum(task => task.Result));
    }

    private static unsafe long ReadLane(string path, long length, int lane, int concurrency)
    {
        using var handle = CreateFile(
            path,
            GenericRead,
            ShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagNoBuffering,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the fixture without buffering.");

        var buffer = NativeMemory.AlignedAlloc(BlockSize, Alignment);
        if (buffer is null)
            throw new OutOfMemoryException();
        try
        {
            long checksum = 0;
            var blocks = length / BlockSize;
            var firstBlock = blocks * lane / concurrency;
            var endBlock = blocks * (lane + 1L) / concurrency;
            for (var block = firstBlock; block < endBlock; block++)
            {
                var offset = block * BlockSize;
                if (!SetFilePointerEx(handle, offset, out _, FileBegin))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot seek in the fixture.");
                if (!ReadFile(handle, buffer, BlockSize, out var read, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the fixture without buffering.");
                if (read != BlockSize)
                    throw new EndOfStreamException("The unbuffered read ended before the expected block length.");
                checksum = unchecked(checksum + read + ((byte*)buffer)[read - 1]);
            }

            return checksum;
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool ReadFile(
        SafeFileHandle file,
        void* buffer,
        uint bytesToRead,
        out uint bytesRead,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFilePointerEx(
        SafeFileHandle file,
        long distance,
        out long newPosition,
        uint moveMethod);

    internal readonly record struct UnbufferedResult(long BytesRead, long Checksum);
}
