#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Traversal;

/// <summary>
///     Identifies the exact file content from which an evidence handle was created.
/// </summary>
internal readonly record struct SearchSourceVersion(
    string CanonicalPath,
    long Length,
    long LastWriteTimeUtcTicks,
    string ContentHash,
    long CreationTimeUtcTicks,
    string? FileIdentity)
{
    private const int HashBufferSize = 64 * 1024;

    public static SearchSourceVersion Capture(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var canonicalPath = Path.GetFullPath(path);
        var before = ReadMetadata(canonicalPath);
        if (!before.Exists)
            throw new FileNotFoundException(
                $"The Search evidence source '{SearchDiagnosticText.Display(canonicalPath, 256)}' does not exist.",
                canonicalPath);

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(
            canonicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            HashBufferSize,
            FileOptions.SequentialScan);
        var fileIdentity = SearchFileIdentity.TryCapture(stream.SafeFileHandle);
        if (stream.Length != before.Length)
            throw new SearchEvidenceStaleException(canonicalPath);

        var contentHash = ComputeContentHash(stream, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var after = ReadMetadata(canonicalPath);
        if (!after.Exists ||
            after.Length != before.Length ||
            after.LastWriteTimeUtcTicks != before.LastWriteTimeUtcTicks ||
            after.CreationTimeUtcTicks != before.CreationTimeUtcTicks)
        {
            throw new SearchEvidenceStaleException(canonicalPath);
        }

        return new SearchSourceVersion(
            canonicalPath,
            before.Length,
            before.LastWriteTimeUtcTicks,
            contentHash,
            before.CreationTimeUtcTicks,
            fileIdentity);
    }

    public static SearchSourceVersion FromSnapshot(
        SearchSourceObservation observation,
        ReadOnlySpan<byte> snapshot)
    {
        if (snapshot.Length != observation.Length)
        {
            throw new SearchSourceChangedException(observation.CanonicalPath);
        }

        return new SearchSourceVersion(
            observation.CanonicalPath,
            observation.Length,
            observation.LastWriteTimeUtcTicks,
            Convert.ToHexString(SHA256.HashData(snapshot)),
            observation.CreationTimeUtcTicks,
            observation.FileIdentity);
    }

    public bool MatchesCurrent(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            var current = Capture(path, cancellationToken);
            return PathComparer.Equals(CanonicalPath, current.CanonicalPath) &&
                   Length == current.Length &&
                   LastWriteTimeUtcTicks == current.LastWriteTimeUtcTicks &&
                   CreationTimeUtcTicks == current.CreationTimeUtcTicks &&
                   string.Equals(FileIdentity, current.FileIdentity, StringComparison.Ordinal) &&
                   string.Equals(ContentHash, current.ContentHash, StringComparison.Ordinal);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static string ComputeContentHash(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                hash.AppendData(buffer, 0, bytesRead);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static SearchFileMetadata ReadMetadata(string path)
    {
        return SearchSourceMetadata.Read(path);
    }
}

/// <summary>
///     Captures a cheap observed boundary for one live-file read. It is a
///     mutation guard, not an atomic filesystem snapshot.
/// </summary>
internal readonly record struct SearchSourceObservation(
    string CanonicalPath,
    long Length,
    long LastWriteTimeUtcTicks,
    long CreationTimeUtcTicks,
    string? FileIdentity)
{
    public static SearchSourceObservation Capture(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var canonicalPath = Path.GetFullPath(path);
        var before = SearchSourceMetadata.Read(canonicalPath);
        if (!before.Exists)
            throw new SearchSourceChangedException(canonicalPath);

        cancellationToken.ThrowIfCancellationRequested();
        FileStream stream;
        try
        {
            stream = new FileStream(
                canonicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw new SearchSourceChangedException(canonicalPath);
        }
        catch (DirectoryNotFoundException)
        {
            throw new SearchSourceChangedException(canonicalPath);
        }

        using (stream)
        {
            var fileIdentity = SearchFileIdentity.TryCapture(stream.SafeFileHandle);
            var after = SearchSourceMetadata.Read(canonicalPath);
            if (!SameMetadata(before, after))
                throw new SearchSourceChangedException(canonicalPath);

            return new SearchSourceObservation(
                canonicalPath,
                before.Length,
                before.LastWriteTimeUtcTicks,
                before.CreationTimeUtcTicks,
                fileIdentity);
        }
    }

    public void EnsureCurrent(CancellationToken cancellationToken = default)
    {
        if (!MatchesCurrent(cancellationToken))
            throw new SearchSourceChangedException(CanonicalPath);
    }

    private bool MatchesCurrent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var current = SearchSourceMetadata.Read(CanonicalPath);
            if (!current.Exists ||
                current.Length != Length ||
                current.LastWriteTimeUtcTicks != LastWriteTimeUtcTicks ||
                current.CreationTimeUtcTicks != CreationTimeUtcTicks)
            {
                return false;
            }

            if (FileIdentity is null)
                return true;

            using var stream = new FileStream(
                CanonicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.SequentialScan);
            return string.Equals(
                FileIdentity,
                SearchFileIdentity.TryCapture(stream.SafeFileHandle),
                StringComparison.Ordinal);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool SameMetadata(
        SearchFileMetadata left,
        SearchFileMetadata right)
    {
        return right.Exists &&
               left.Length == right.Length &&
               left.LastWriteTimeUtcTicks == right.LastWriteTimeUtcTicks &&
               left.CreationTimeUtcTicks == right.CreationTimeUtcTicks;
    }
}

internal readonly record struct SearchFileMetadata(
    bool Exists,
    long Length,
    long LastWriteTimeUtcTicks,
    long CreationTimeUtcTicks);

internal static class SearchSourceMetadata
{
    public static SearchFileMetadata Read(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        return new SearchFileMetadata(
            file.Exists,
            file.Exists ? file.Length : 0,
            file.Exists ? file.LastWriteTimeUtc.Ticks : 0,
            file.Exists ? file.CreationTimeUtc.Ticks : 0);
    }
}

internal sealed class SearchEvidenceStaleException(string path)
    : IOException(
        $"Search evidence for '{SearchDiagnosticText.Display(path, 256)}' is stale because the source changed.")
{
    public string Path { get; } = path;
}
