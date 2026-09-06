#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchMutationRecoveryTests
{
    [DataTestMethod]
    [DataRow("append")]
    [DataRow("truncate")]
    [DataRow("rename")]
    [DataRow("replace")]
    public void MidReadMutation_ShouldNotPublishACompleteScope(string mutation)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO\n");
            var source = new SearchMatchesSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext(),
                filePath => new MutatingTextReader(
                    "TODO\n",
                    () => ApplyMutation(root, filePath, mutation)));

            var exception = Assert.ThrowsException<SearchSourceChangedException>(
                () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.SourceChanged, exception.Diagnostic.Code);
            Assert.IsNotNull(source.LastExecution);
            Assert.AreEqual(SearchOutcome.Failed, source.LastExecution!.Outcome);
            Assert.AreEqual("source-changed", source.LastExecution.TerminalReason);
            Assert.AreEqual(SearchDiagnosticCodes.SourceChanged, source.LastExecution.FailureCode);
            Assert.IsFalse(source.LastExecution.Complete);
            Assert.IsFalse(source.LastExecution.CountsExact);
            Assert.AreEqual(1L, source.LastExecution.FilesFailed);
            Assert.AreEqual(0L, source.LastExecution.FilesCompleted);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void PermissionFailureAtReaderOpen_ShouldRemainTypedAndIncomplete()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO\n");
            var source = new SearchMatchesSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => throw new UnauthorizedAccessException("synthetic permission change"));

            var exception = Assert.ThrowsException<SearchSourceAccessException>(
                () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.SourceOpenFailed, exception.Diagnostic.Code);
            Assert.IsNotNull(source.LastExecution);
            Assert.AreEqual(SearchOutcome.Failed, source.LastExecution!.Outcome);
            Assert.AreEqual("unreadable-file", source.LastExecution.TerminalReason);
            Assert.AreEqual(SearchDiagnosticCodes.SourceOpenFailed, source.LastExecution.FailureCode);
            Assert.IsFalse(source.LastExecution.CountsExact);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [DataTestMethod]
    [DataRow("read")]
    [DataRow("decoder")]
    public void ReadAndDecoderFailures_ShouldRemainTypedAndIncomplete(string failureKind)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO\n");
            Exception failure = failureKind == "decoder"
                ? new DecoderFallbackException("synthetic decoder failure")
                : new IOException("synthetic read failure");
            var source = new SearchMatchesSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new ThrowingTextReader(failure));

            var exception = Assert.ThrowsException<SearchSourceReadException>(
                () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.IsNotNull(source.LastExecution);
            Assert.AreEqual(SearchOutcome.Failed, source.LastExecution!.Outcome);
            Assert.AreEqual("unreadable-file", source.LastExecution.TerminalReason);
            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, source.LastExecution.FailureCode);
            Assert.IsFalse(source.LastExecution.CountsExact);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [DataTestMethod]
    [DataRow("append")]
    [DataRow("truncate")]
    [DataRow("delete")]
    [DataRow("rename")]
    [DataRow("replace")]
    public void EvidenceHandle_ShouldRejectLaterMutation(string mutation)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "before\nhit TODO\nafter\n");
            var handle = SearchEvidenceHandle.Capture(
                path,
                SearchEncodingMode.Auto);

            ApplyMutation(root, path, mutation);

            Assert.ThrowsException<SearchEvidenceStaleException>(() =>
                handle.Expand(
                    matchingLine: 2,
                    new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 128)));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EvidenceHandle_ShouldRejectReplacementWithPreservedMetadata()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");
        const string content = "before\nhit TODO\nafter\n";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content);
            var handle = SearchEvidenceHandle.Capture(
                path,
                SearchEncodingMode.Auto);
            var lastWrite = File.GetLastWriteTimeUtc(path);
            var creation = File.GetCreationTimeUtc(path);
            var replacement = Path.Combine(root, "replacement.txt");
            File.WriteAllText(replacement, OperatingSystem.IsWindows()
                ? content
                : "before\nhit DONE\nafter\n");
            File.Move(replacement, path, overwrite: true);
            File.SetLastWriteTimeUtc(path, lastWrite);
            if (OperatingSystem.IsWindows())
                File.SetCreationTimeUtc(path, creation);

            Assert.ThrowsException<SearchEvidenceStaleException>(() =>
                handle.Expand(
                    matchingLine: 2,
                    new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 128)));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EvidenceHandle_ShouldClassifyDecoderFailureAsSourceReadFailure()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "invalid-utf8.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path, [
                (byte)'b', (byte)'e', (byte)'f', (byte)'o', (byte)'r', (byte)'e', (byte)'\n',
                (byte)'h', (byte)'i', (byte)'t', (byte)' ', (byte)'T', (byte)'O', (byte)'D', (byte)'O', (byte)'\n',
                0xe2
            ]);
            var handle = SearchEvidenceHandle.Capture(
                path,
                SearchEncodingMode.Utf8);

            var exception = Assert.ThrowsException<SearchSourceReadException>(() =>
                handle.Expand(
                    matchingLine: 2,
                    new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 128)));

            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.IsInstanceOfType<DecoderFallbackException>(exception.InnerException);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static void ApplyMutation(string root, string path, string mutation)
    {
        switch (mutation)
        {
            case "append":
                File.AppendAllText(path, "appended\n");
                break;
            case "truncate":
                using (var stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Write,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    stream.SetLength(1);
                    stream.Flush(flushToDisk: true);
                }

                break;
            case "delete":
                File.Delete(path);
                break;
            case "rename":
                File.Move(path, path + ".moved");
                break;
            case "replace":
                var replacement = Path.Combine(root, "replacement.txt");
                File.WriteAllText(replacement, "DONE\n");
                File.Move(replacement, path, overwrite: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-mutation-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class MutatingTextReader(
        string content,
        Action mutation) : TextReader
    {
        private int _position;
        private bool _mutated;

        public override int Read(char[] buffer, int index, int count)
        {
            if (!_mutated)
            {
                _mutated = true;
                mutation();
            }

            if (_position >= content.Length)
                return 0;

            var length = Math.Min(count, content.Length - _position);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class ThrowingTextReader(Exception failure) : TextReader
    {
        public override int Read(char[] buffer, int index, int count)
        {
            throw failure;
        }
    }
}
