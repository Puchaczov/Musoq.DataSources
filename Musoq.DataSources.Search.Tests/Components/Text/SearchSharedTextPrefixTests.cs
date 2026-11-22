#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchSharedTextPrefixTests
{
    private string _path = null!;

    [TestInitialize]
    public void Initialize() =>
        _path = Path.Combine(Path.GetTempPath(), $"musoq-shared-prefix-{Guid.NewGuid():N}.jsonl");

    [TestCleanup]
    public void Cleanup() => File.Delete(_path);

    [TestMethod]
    [DataRow(16)]
    [DataRow(4 * 1024 * 1024)]
    public void SharedRead_WhenWriterAppends_SubsequentBytesAreExcluded(int padding)
    {
        File.WriteAllText(_path, new string('x', padding) + "\nmarker\nmarker pending", Encoding.UTF8);
        using var writer = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var hooks = SearchTestHooks.Install(new()
        {
            AfterObservation = _ =>
            {
                writer.Write("\nmarker later\n"u8);
                writer.Flush();
            }
        });
        var source = CreateSource(_path);

        var matches = source.Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.AreEqual(padding + Encoding.UTF8.GetPreamble().Length + 1L, matches.Single().ByteOffset);
        Assert.IsTrue(source.LastExecution!.Complete);
    }

    [TestMethod]
    public void SharedRead_ForUtf16_IncludesOnlyCompleteLinesWithOriginalOffsets()
    {
        File.WriteAllText(_path, "first\r\nmarker\r\nmarker", Encoding.BigEndianUnicode);
        var source = CreateSource(_path);

        var match = source.Chunks.SelectMany(static chunk => chunk).Single();

        Assert.AreEqual(16L, match.ByteOffset);
        Assert.AreEqual(2L, match.LineNumber);
    }

    [TestMethod]
    public void SharedEvidence_WhenExpandedLater_AllowsAppendAndDetectsEdits()
    {
        File.WriteAllText(_path, "before\nmarker\n");
        var options = new SearchContextOptions(beforeLines: 1, afterLines: 1);
        var source = new SearchMatchesSource(SearchRequest.Create(_path, "marker", context: options), SharedContext());
        var match = source.Chunks.SelectMany(static chunk => chunk).Single();

        File.AppendAllText(_path, "new line\n");
        Assert.IsTrue(match.ExpandContext(options).All(static line => line.LineNumber <= 2));

        File.WriteAllText(_path, "edited\nmarker\nnew line\n");
        Assert.ThrowsException<SearchSourceChangedException>(() => match.ExpandContext(options));
    }

    [TestMethod]
    public void SharedMany_PreservesLabeledMatchesAndDropsThePendingLine()
    {
        File.WriteAllText(_path, "alpha\nbeta\nalpha");
        var source = new SearchManySource(_path, new SearchManyRequest(
        [
            new SearchManyPattern("a", "alpha", SearchPatternMode.Literal),
            new SearchManyPattern("b", "beta", SearchPatternMode.Literal)
        ]), SharedContext());

        var matches = source.Chunks.SelectMany(static chunk => chunk)
            .Select(static match => (match.PatternId, match.ByteOffset)).ToArray();

        CollectionAssert.AreEqual(new (string, long?)[] { ("a", 0L), ("b", 6L) }, matches);
    }

    [TestMethod]
    public void StableLargeFile_WhenWriterIsHeld_ReportsSharingViolation()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The diagnostic represents a Windows sharing violation.");

        File.WriteAllText(_path, new string('x', 4 * 1024 * 1024) + "\nmarker\n");
        using var writer = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var source = new SearchLinesSource(_path, "marker", RuntimeV2TestContexts.CreateExecutionContext());

        var failure = Assert.ThrowsException<SearchSourceAccessException>(
            () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

        Assert.AreEqual(SearchDiagnosticCodes.SharingViolation, failure.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("truncate")]
    [DataRow("edit")]
    [DataRow("replace")]
    public void SharedRead_OnPrefixMutation_FailsTheScan(string mutation)
    {
        File.WriteAllText(_path, "marker\n");
        using var hooks = SearchTestHooks.Install(new() { AfterObservation = path => ChangePrefix(path, mutation) });
        var source = CreateSource(_path);

        var failure = Assert.ThrowsException<SearchSourceChangedException>(
            () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

        Assert.AreEqual(SearchDiagnosticCodes.SourceChanged, failure.Diagnostic.Code);
        Assert.IsFalse(source.LastExecution!.Complete);
    }

    [TestMethod]
    public void SharedRead_WhenAccessIsDeniedByAnotherHandle_ReportsSharingViolation()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The diagnostic represents a Windows sharing violation.");

        File.WriteAllText(_path, "marker\n");
        using var locked = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.None);
        var source = CreateSource(_path);

        var failure = Assert.ThrowsException<SearchSourceAccessException>(
            () => source.Chunks.SelectMany(static chunk => chunk).ToArray());

        Assert.AreEqual(SearchDiagnosticCodes.SharingViolation, failure.Diagnostic.Code);
    }

    private static SearchMatchesSource CreateSource(string path) => new(SearchRequest.Create(path, "marker"), SharedContext());

    private static Musoq.Schema.Optimization.SourceExecutionContext SharedContext() =>
        RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: new Dictionary<string, string>
        {
            [SearchSharedTextPrefix.SettingName] = "shared-prefix",
            ["search.max_parallelism"] = "1"
        });

    private static void ChangePrefix(string path, string mutation)
    {
        switch (mutation)
        {
            case "truncate":
                File.WriteAllText(path, string.Empty);
                break;
            case "edit":
                File.WriteAllText(path, "edited\n");
                break;
            default:
                var replacement = path + ".replacement";
                try
                {
                    File.WriteAllText(replacement, "marker\n");
                    File.Move(replacement, path, overwrite: true);
                }
                finally
                {
                    File.Delete(replacement);
                }
                break;
        }
    }
}
