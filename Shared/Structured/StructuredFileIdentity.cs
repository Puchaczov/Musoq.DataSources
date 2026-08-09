#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Musoq.DataSources.Structured;

internal readonly record struct StructuredFileFingerprint(ulong High, ulong Low)
{
    public override string ToString()
    {
        return $"{High:x16}{Low:x16}";
    }
}

internal readonly record struct StructuredFileIdentity(
    string CanonicalPath,
    long Length,
    long LastWriteTimeUtcTicks,
    string ParserOptions,
    StructuredFileFingerprint Fingerprint)
{
    private const int EdgeBytes = 64 * 1024;

    public static StructuredFileIdentity Capture(
        string path,
        string parserOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(parserOptions);

        var canonicalPath = Path.GetFullPath(path);
        var before = new FileInfo(canonicalPath);
        before.Refresh();

        if (!before.Exists)
            throw new FileNotFoundException($"Structured source '{canonicalPath}' does not exist.", canonicalPath);

        var length = before.Length;
        var lastWriteTicks = before.LastWriteTimeUtc.Ticks;
        var fingerprint = CaptureFingerprint(canonicalPath, length, cancellationToken);
        var after = new FileInfo(canonicalPath);
        after.Refresh();

        if (!after.Exists || after.Length != length || after.LastWriteTimeUtc.Ticks != lastWriteTicks)
            throw new StructuredSourceChangedException(canonicalPath);

        return new StructuredFileIdentity(canonicalPath, length, lastWriteTicks, parserOptions, fingerprint);
    }

    private static StructuredFileFingerprint CaptureFingerprint(
        string path,
        long length,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            EdgeBytes,
            FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(EdgeBytes);

        try
        {
            if (length <= EdgeBytes * 2L)
            {
                AppendBytes(stream, hash, buffer, length, cancellationToken);
            }
            else
            {
                AppendBytes(stream, hash, buffer, EdgeBytes, cancellationToken);
                stream.Seek(-EdgeBytes, SeekOrigin.End);
                AppendBytes(stream, hash, buffer, EdgeBytes, cancellationToken);
            }

            var digest = hash.GetHashAndReset();
            return new StructuredFileFingerprint(
                BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(0, sizeof(ulong))),
                BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(sizeof(ulong), sizeof(ulong))));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void AppendBytes(
        Stream stream,
        IncrementalHash hash,
        byte[] buffer,
        long bytesToRead,
        CancellationToken cancellationToken)
    {
        var remaining = bytesToRead;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = stream.Read(buffer, 0, requested);
            if (read == 0)
                throw new EndOfStreamException("Structured source ended while capturing its fingerprint.");
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
    }
}

internal sealed class StructuredFileIdentityComparer : IEqualityComparer<StructuredFileIdentity>
{
    public static StructuredFileIdentityComparer Instance { get; } = new();

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public bool Equals(StructuredFileIdentity left, StructuredFileIdentity right)
    {
        return PathComparer.Equals(left.CanonicalPath, right.CanonicalPath) &&
               left.Length == right.Length &&
               left.LastWriteTimeUtcTicks == right.LastWriteTimeUtcTicks &&
               string.Equals(left.ParserOptions, right.ParserOptions, StringComparison.Ordinal) &&
               left.Fingerprint == right.Fingerprint;
    }

    public int GetHashCode(StructuredFileIdentity identity)
    {
        var hash = new HashCode();
        hash.Add(identity.CanonicalPath, PathComparer);
        hash.Add(identity.Length);
        hash.Add(identity.LastWriteTimeUtcTicks);
        hash.Add(identity.ParserOptions, StringComparer.Ordinal);
        hash.Add(identity.Fingerprint);
        return hash.ToHashCode();
    }
}
