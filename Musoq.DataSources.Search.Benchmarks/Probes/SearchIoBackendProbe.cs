#nullable enable

using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace Musoq.DataSources.Search.Benchmarks.Probes;

public enum SearchIoBackend
{
    BufferedSequential,
    MemoryMapped
}

public enum SearchIoReadStatus
{
    Completed,
    Cancelled,
    MutationDetected,
    Failed
}

public sealed record SearchIoReadResult(
    SearchIoBackend Backend,
    SearchIoReadStatus Status,
    long BytesRead,
    string Sha256,
    string? ExceptionType);

public static class SearchIoBackendProbe
{
    private const int BufferSize = 64 * 1024;

    public static SearchIoReadResult Read(
        string path,
        SearchIoBackend backend,
        CancellationToken cancellationToken = default,
        Action? afterOpen = null,
        Action<long>? afterRead = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (backend is not SearchIoBackend.BufferedSequential and not SearchIoBackend.MemoryMapped)
            throw new ArgumentOutOfRangeException(nameof(backend));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        FileStream? file = null;
        MemoryMappedFile? mapping = null;
        Stream? stream = null;
        var bytesRead = 0L;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BufferSize,
                FileOptions.SequentialScan);
            var before = CaptureFingerprint(path);

            if (backend == SearchIoBackend.MemoryMapped && file.Length > 0)
            {
                var fileLength = file.Length;
                mapping = MemoryMappedFile.CreateFromFile(
                    file,
                    mapName: null,
                    capacity: 0,
                    access: MemoryMappedFileAccess.Read,
                    inheritability: HandleInheritability.None,
                    leaveOpen: false);
                file = null;
                stream = mapping.CreateViewStream(
                    0,
                    fileLength,
                    MemoryMappedFileAccess.Read);
            }
            else if (backend == SearchIoBackend.MemoryMapped)
            {
                // A zero-length file has no page that can be mapped. Treat it as a
                // valid empty read rather than inventing a fallback path.
                afterOpen?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                return Complete(
                    path,
                    backend,
                    before,
                    bytesRead,
                    hash);
            }
            else
            {
                stream = file;
                file = null;
            }

            afterOpen?.Invoke();
            var buffer = new byte[BufferSize];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream!.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;

                hash.AppendData(buffer.AsSpan(0, read));
                bytesRead = checked(bytesRead + read);
                afterRead?.Invoke(bytesRead);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Complete(
                path,
                backend,
                before,
                bytesRead,
                hash);
        }
        catch (OperationCanceledException exception)
        {
            return CreateResult(
                backend,
                SearchIoReadStatus.Cancelled,
                bytesRead,
                hash,
                exception);
        }
        catch (Exception exception)
        {
            return CreateResult(
                backend,
                SearchIoReadStatus.Failed,
                bytesRead,
                hash,
                exception);
        }
        finally
        {
            stream?.Dispose();
            mapping?.Dispose();
            file?.Dispose();
        }
    }

    private static SearchIoReadResult Complete(
        string path,
        SearchIoBackend backend,
        FileFingerprint before,
        long bytesRead,
        IncrementalHash hash)
    {
        var after = CaptureFingerprint(path);
        return after == before
            ? CreateResult(
                backend,
                SearchIoReadStatus.Completed,
                bytesRead,
                hash,
                exception: null)
            : CreateResult(
                backend,
                SearchIoReadStatus.MutationDetected,
                bytesRead,
                hash,
                exception: null);
    }

    private static SearchIoReadResult CreateResult(
        SearchIoBackend backend,
        SearchIoReadStatus status,
        long bytesRead,
        IncrementalHash hash,
        Exception? exception)
    {
        return new SearchIoReadResult(
            backend,
            status,
            bytesRead,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            exception?.GetType().FullName);
    }

    private static FileFingerprint CaptureFingerprint(string path)
    {
        return new FileFingerprint(
            new FileInfo(path).Length,
            File.GetLastWriteTimeUtc(path));
    }

    private readonly record struct FileFingerprint(
        long Length,
        DateTime LastWriteTimeUtc);
}
