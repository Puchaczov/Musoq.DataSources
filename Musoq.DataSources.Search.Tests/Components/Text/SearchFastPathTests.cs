#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchFastPathTests
{
    [TestMethod]
    public void SearchSource_ShouldResetReusableMatcherBetweenFiles()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "fixture");
            Write(root, "second.txt", "fixture");

            var rows = new SearchMatchesSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    filePath => new StringReader(
                        Path.GetFileName(filePath) == "first.txt" ? "TO" : "DO"))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(0, rows.Length);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void NoMatchLiteralScan_ShouldHaveFlatAllocationSlopeAsInputGrows()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "input.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "fixture");

            var small = MeasureNoMatchAllocation(path, BuildNoMatchContent(128));
            var large = MeasureNoMatchAllocation(path, BuildNoMatchContent(8_192));

            Assert.IsTrue(
                large - small <= 32 * 1024,
                $"No-match allocation grew by {large - small:N0} bytes for input growth.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void OptionalLineText_ShouldChangeMaterializationWithoutChangingOccurrenceSet()
    {
        var root = CreateTemporaryRoot();
        const string content = "TODO TODO\nordinary\nTODO\n";

        try
        {
            Write(root, "input.txt", "fixture");

            var matches = new SearchMatchesSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new StringReader(content))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
            var lines = new SearchLinesSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new StringReader(content))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(3, matches.Length);
            Assert.AreEqual(
                matches.Length,
                lines.Sum(static row => checked((int)row.OccurrenceCount)));
            Assert.IsTrue(matches.All(static row => row.MatchText == "TODO"));
            Assert.IsTrue(lines.All(static row => row.LineText is not null));
            Assert.IsTrue(lines.All(static row => row.Origin is null && row.PatternId is null));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static long MeasureNoMatchAllocation(string path, string content)
    {
        RunNoMatch(path, content);
        ForceCollection();

        var before = GC.GetAllocatedBytesForCurrentThread();
        RunNoMatch(path, content);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void RunNoMatch(string path, string content)
    {
        var rows = new SearchMatchesSource(
                Path.GetDirectoryName(path)!,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader(content))
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();

        Assert.AreEqual(0, rows.Length);
    }

    private static string BuildNoMatchContent(int lineCount)
    {
        var lines = new string[lineCount];
        for (var index = 0; index < lines.Length; index++)
            lines[index] = $"ordinary line {index:D5} without the marker\n";

        return string.Concat(lines);
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-fast-path-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
