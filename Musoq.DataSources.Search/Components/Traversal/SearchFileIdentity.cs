#nullable enable

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Musoq.DataSources.Search.Components.Traversal;

internal static class SearchFileIdentity
{
    public static string? TryCapture(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!OperatingSystem.IsWindows() || handle.IsInvalid)
            return null;

        if (!GetFileInformationByHandle(handle, out var information))
            return null;

        return string.Create(
            8 + 1 + 16,
            information,
            static (destination, value) =>
            {
                value.VolumeSerialNumber.TryFormat(
                    destination[..8],
                    out _,
                    "x8");
                destination[8] = ':';
                value.FileIndexHigh.TryFormat(
                    destination.Slice(9, 8),
                    out _,
                    "x8");
                value.FileIndexLow.TryFormat(
                    destination.Slice(17, 8),
                    out _,
                    "x8");
            });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
