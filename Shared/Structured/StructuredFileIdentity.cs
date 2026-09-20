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

    public bool MatchesCurrentMetadata(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        var canonicalPath = Path.GetFullPath(path);
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (!pathComparer.Equals(CanonicalPath, canonicalPath))
            return false;

        var current = new FileInfo(canonicalPath);
        current.Refresh();
        return current.Exists &&
               current.Length == Length &&
               current.LastWriteTimeUtc.Ticks == LastWriteTimeUtcTicks;
    }

    public static StructuredFileFingerprint ComputeFingerprint(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        if (content.Length <= EdgeBytes * 2)
        {
            SHA256.HashData(content, digest);
        }
        else
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(content[..EdgeBytes]);
            hash.AppendData(content[^EdgeBytes..]);
            if (!hash.TryGetHashAndReset(digest, out var written) || written != digest.Length)
                throw new CryptographicException("Could not compute the structured-source fingerprint.");
        }

        return FromDigest(digest);
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
        var fingerprintBytes = checked((int)Math.Min(length, EdgeBytes * 2L));
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, fingerprintBytes));

        try
        {
            if (length <= EdgeBytes * 2L)
            {
                ReadBytes(stream, buffer, 0, fingerprintBytes, cancellationToken);
            }
            else
            {
                ReadBytes(stream, buffer, 0, EdgeBytes, cancellationToken);
                stream.Seek(-EdgeBytes, SeekOrigin.End);
                ReadBytes(stream, buffer, EdgeBytes, EdgeBytes, cancellationToken);
            }

            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(buffer.AsSpan(0, fingerprintBytes), digest);
            return FromDigest(digest);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static StructuredFileFingerprint FromDigest(ReadOnlySpan<byte> digest)
    {
        return new StructuredFileFingerprint(
            BinaryPrimitives.ReadUInt64LittleEndian(digest[..sizeof(ulong)]),
            BinaryPrimitives.ReadUInt64LittleEndian(digest.Slice(sizeof(ulong), sizeof(ulong))));
    }

    private static void ReadBytes(
        Stream stream,
        byte[] buffer,
        int offset,
        int bytesToRead,
        CancellationToken cancellationToken)
    {
        var remaining = bytesToRead;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, offset, remaining);
            if (read == 0)
                throw new EndOfStreamException("Structured source ended while capturing its fingerprint.");
            offset += read;
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
