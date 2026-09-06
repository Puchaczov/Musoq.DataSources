#nullable enable

using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Probes;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchIoBackendTests
{
    [TestMethod]
    public void StableTinyAndLargeFiles_ShouldMatchAcrossBufferedAndMappedReads()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var tinyPath = Path.Combine(root, "tiny.bin");
            File.WriteAllBytes(tinyPath, [0, 1, 2, 127, 128, 255]);

            var largePath = Path.Combine(root, "large.bin");
            var large = new byte[2 * 1024 * 1024];
            for (var index = 0; index < large.Length; index++)
                large[index] = unchecked((byte)(index * 17 + index / 11));
            File.WriteAllBytes(largePath, large);

            foreach (var path in new[] { tinyPath, largePath })
            {
                var buffered = SearchIoBackendProbe.Read(
                    path,
                    SearchIoBackend.BufferedSequential);
                var mapped = SearchIoBackendProbe.Read(
                    path,
                    SearchIoBackend.MemoryMapped);

                Assert.AreEqual(SearchIoReadStatus.Completed, buffered.Status, path);
                Assert.AreEqual(SearchIoReadStatus.Completed, mapped.Status, path);
                Assert.AreEqual(buffered.BytesRead, mapped.BytesRead, path);
                Assert.AreEqual(buffered.Sha256, mapped.Sha256, path);
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EmptyFile_ShouldCompleteWithoutCreatingAZeroLengthMapping()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var path = Path.Combine(root, "empty.bin");
            File.WriteAllBytes(path, []);

            var buffered = SearchIoBackendProbe.Read(
                path,
                SearchIoBackend.BufferedSequential);
            var mapped = SearchIoBackendProbe.Read(
                path,
                SearchIoBackend.MemoryMapped);

            Assert.AreEqual(SearchIoReadStatus.Completed, buffered.Status);
            Assert.AreEqual(SearchIoReadStatus.Completed, mapped.Status);
            Assert.AreEqual(0L, mapped.BytesRead);
            Assert.AreEqual(buffered.Sha256, mapped.Sha256);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ChangingFile_ShouldNeverBeReportedAsAStableMappedRead()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var path = Path.Combine(root, "changing.bin");
            File.WriteAllBytes(path, CreatePayload(2 * 1024 * 1024));
            var mutationApplied = false;
            Exception? mutationFailure = null;

            var result = SearchIoBackendProbe.Read(
                path,
                SearchIoBackend.MemoryMapped,
                afterOpen: () =>
                {
                    try
                    {
                        using var writer = new FileStream(
                            path,
                            FileMode.Open,
                            FileAccess.Write,
                            FileShare.ReadWrite | FileShare.Delete);
                        writer.SetLength(1);
                        writer.Flush(flushToDisk: true);
                        File.SetLastWriteTimeUtc(
                            path,
                            File.GetLastWriteTimeUtc(path).AddMinutes(10));
                        mutationApplied = true;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        mutationFailure = exception;
                    }
                });

            Assert.IsTrue(mutationApplied || mutationFailure is not null);
            Assert.IsTrue(
                !mutationApplied || result.Status != SearchIoReadStatus.Completed,
                $"A mapped read was reported stable after mutation: {result.Status}.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void MappingFailure_ShouldBeReportedWithoutBufferedFallback()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-missing-{Guid.NewGuid():N}.bin");

        var result = SearchIoBackendProbe.Read(
            path,
            SearchIoBackend.MemoryMapped);

        Assert.AreEqual(SearchIoReadStatus.Failed, result.Status);
        Assert.IsNotNull(result.ExceptionType);
        Assert.AreEqual(0L, result.BytesRead);
    }

    [TestMethod]
    public void Cancellation_ShouldStopBufferedAndMappedReads()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var path = Path.Combine(root, "cancel.bin");
            var payloadLength = 2 * 1024 * 1024;
            File.WriteAllBytes(path, CreatePayload(payloadLength));

            foreach (var backend in Enum.GetValues<SearchIoBackend>())
            {
                using var cancellation = new CancellationTokenSource();
                var result = SearchIoBackendProbe.Read(
                    path,
                    backend,
                    cancellation.Token,
                    afterRead: bytesRead =>
                    {
                        if (bytesRead > 0)
                            cancellation.Cancel();
                    });

                Assert.AreEqual(SearchIoReadStatus.Cancelled, result.Status, backend.ToString());
                Assert.IsTrue(result.BytesRead > 0, backend.ToString());
                Assert.IsTrue(result.BytesRead < payloadLength, backend.ToString());
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static byte[] CreatePayload(int length)
    {
        var payload = new byte[length];
        for (var index = 0; index < payload.Length; index++)
            payload[index] = unchecked((byte)(index * 17 + index / 11));

        return payload;
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-io-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
