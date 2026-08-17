using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class WindowsUnbufferedMultiReader
{
    private const int BlockSize = 4 * 1024 * 1024;
    private const int Alignment = 4096;
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileBegin = 0;
    private const uint FileFlagNoBuffering = 0x20000000;

    public static long Read(string path, int concurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
        var length = new FileInfo(path).Length;
        if (length % Alignment != 0)
            throw new InvalidOperationException("The unbuffered NVMe fixture length must be sector-aligned.");

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
        return lanes.Sum(task => task.Result);
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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the NVMe fixture without buffering.");

        var buffer = NativeMemory.AlignedAlloc(BlockSize, Alignment);
        if (buffer is null)
            throw new OutOfMemoryException();
        try
        {
            long checksum = 0;
            var blockCount = length / BlockSize;
            var firstBlock = blockCount * lane / concurrency;
            var endBlock = blockCount * (lane + 1L) / concurrency;
            for (var blockIndex = firstBlock; blockIndex < endBlock; blockIndex++)
            {
                var offset = blockIndex * BlockSize;
                var requested = (uint)Math.Min(BlockSize, length - offset);
                if (!SetFilePointerEx(handle, offset, out _, FileBegin))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot seek in the NVMe fixture.");
                if (!ReadFile(handle, buffer, requested, out var read, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the NVMe fixture without buffering.");
                if (read != requested)
                    throw new EndOfStreamException("The unbuffered NVMe read ended before the expected file length.");
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
}
