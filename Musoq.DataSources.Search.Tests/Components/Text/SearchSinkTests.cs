#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchSinkTests
{
    [TestMethod]
    public void SearchSinks_ShouldPreserveOccurrenceLineAndFileCardinality()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "TODO TODO\nno match\nTODO");
            Write(root, "empty.txt", "no match\n");

            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var matches = new SearchMatchesSource(root, "TODO", context)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
            var lines = new SearchLinesSource(root, "TODO", context)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
            var files = new SearchFilesSource(root, "TODO", context)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(3, matches.Length);
            CollectionAssert.AreEqual(
                new[] { "first.txt", "first.txt", "first.txt" },
                matches.Select(static row => row.Path).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 0, 1, 2 },
                matches.Select(static row => row.MatchIndex).ToArray());

            Assert.AreEqual(2, lines.Length);
            CollectionAssert.AreEqual(
                new long[] { 1, 3 },
                lines.Select(static row => row.LineNumber).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 2, 1 },
                lines.Select(static row => row.OccurrenceCount).ToArray());
            CollectionAssert.AreEqual(
                new[] { "TODO TODO\n", "TODO" },
                lines.Select(static row => row.LineText).ToArray());
            Assert.IsTrue(lines.All(static row => row.Path == "first.txt"));
            Assert.IsTrue(lines.All(static row => row.Origin is null && row.PatternId is null));
            CollectionAssert.AreEqual(
                new long?[] { 0, 19 },
                lines.Select(static row => row.ByteOffset).ToArray());

            Assert.AreEqual(1, files.Length);
            Assert.AreEqual("first.txt", files[0].Path);
            Assert.IsNull(files[0].Origin);
            Assert.IsNull(files[0].PatternId);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchSinks_ShouldRemainProjectionInvariant()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "TODO TODO\nno match\nTODO");
            Write(root, "empty.txt", "no match\n");
            var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

            var matches = Compile($"select Path from search.matches('{escapedRoot}', 'TODO') order by MatchIndex")
                .Run();
            var lines = Compile($"select Path from search.lines('{escapedRoot}', 'TODO') order by LineNumber")
                .Run();
            var files = Compile($"select Path from search.files('{escapedRoot}', 'TODO') order by Path")
                .Run();

            Assert.AreEqual(3, matches.Count);
            CollectionAssert.AreEqual(
                new[] { "first.txt", "first.txt", "first.txt" },
                matches.Rows.Select(row => (string)row[0]!).ToArray());
            Assert.AreEqual(2, lines.Count);
            CollectionAssert.AreEqual(
                new[] { "first.txt", "first.txt" },
                lines.Rows.Select(row => (string)row[0]!).ToArray());
            Assert.AreEqual(1, files.Count);
            Assert.AreEqual("first.txt", files.Rows.Single()[0]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void FilesSource_ShouldStopAfterTheFirstQualifyingOccurrence()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "first.txt");
        var reader = new ReadOnceThenThrowTextReader("TODO trailing content");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "reader content is supplied by the test");

            var rows = new SearchFilesSource(
                    path,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => reader)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("first.txt", rows[0].Path);
            Assert.AreEqual(1, reader.ReadCount);
            Assert.IsTrue(reader.Disposed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void LinesSource_ShouldGroupMatchesWhenThePhysicalLineCrossesReaderBlocks()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "boundary.txt");
        var content = new string('x', 8_190) + "TODO TODO\n";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "reader content is supplied by the test");

            var rows = new SearchLinesSource(
                    path,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new StringReader(content))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(1L, rows[0].LineNumber);
            Assert.AreEqual(2L, rows[0].OccurrenceCount);
            Assert.AreEqual(content, rows[0].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);

        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-sinks-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class ReadOnceThenThrowTextReader(string content) : TextReader
    {
        private int _position;

        public int ReadCount { get; private set; }

        public bool Disposed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            ReadCount++;
            if (ReadCount > 1)
                throw new InvalidOperationException("The files sink read past the first qualifying occurrence.");

            var length = Math.Min(count, content.Length - _position);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
