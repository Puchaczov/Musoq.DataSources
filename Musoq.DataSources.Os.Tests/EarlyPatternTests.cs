using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
[DoNotParallelize]
public class EarlyPatternTests
{
    [TestMethod]
    public void RejectedParentStillTraversesChildren_AndConstructsOnlyMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-early-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "reject", "keep"));
        try
        {
            var schema = new OsSchema();
            var identity = new SourceIdentity("os", "s", "s", "directories");
            var predicate = new SourcePredicateRegexMatch(new("Name"), "^keep$", TimeSpan.FromMilliseconds(23));
            var plan = schema.TryPlanSource("directories", new() { Identity = identity, Predicate = predicate });
            Assert.AreSame(predicate, plan.ExecutionPlan.PredicateApplications.Single().Predicate);
            var sink = new Metrics();
            var rows = Read<Directories.DirectoryEntity>(schema, "directories", root, true, plan.ExecutionPlan, sink);
            Assert.AreEqual("keep", rows.Single().Name);
            Assert.AreEqual(2L, sink.Values["Pattern.Candidates"]);
            Assert.AreEqual(1L, sink.Values["Pattern.RejectedCandidates"]);
            Assert.AreEqual(1L, sink.Values["Pattern.ConstructedRows"]);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ExcludedMetadataFileIsNeverOpened_IncludedLockedFileStillFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-early-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "locked.png");
        File.WriteAllText(file, "not an image");
        try
        {
            using var locked = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var schema = new OsSchema();
            var identity = new SourceIdentity("os", "s", "s", "metadata");
            SourceExecutionPlan Plan(string pattern) => schema.TryPlanSource("metadata", new() { Identity = identity,
                Predicate = new SourcePredicateRegexMatch(new("FullName"), pattern, TimeSpan.FromMilliseconds(23)) }).ExecutionPlan;
            var sink = new Metrics();
            Assert.AreEqual(0, Read<Metadata.MetadataEntity>(schema, "metadata", root, false, Plan("does-not-match"), sink).Length);
            Assert.AreEqual(1L, sink.Values["Pattern.RejectedCandidates"]);
            Assert.AreEqual(0L, sink.Values["Pattern.ContentOpens"]);
            Assert.ThrowsException<IOException>(() => Read<Metadata.MetadataEntity>(schema, "metadata", root, false, Plan("locked"), new Metrics()));
        }
        finally { Directory.Delete(root, true); }
    }

    private static T[] Read<T>(OsSchema schema, string source, string root, bool recursive, SourceExecutionPlan plan, Metrics sink)
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan, sourceDiagnostics: new SourceDiagnostics(sink));
        T[] ReadArgs<TArgs>(TArgs args) where TArgs : struct, ISourceArguments => schema.GetRowSource<T, TArgs>(source, context, args).Chunks.SelectMany(chunk => chunk).ToArray();
        return source == "metadata" ? ReadArgs(SourceArguments.Create(root, SourceArguments.Create(recursive, SourceArguments.Create(true, SourceArguments.Empty))))
            : ReadArgs(SourceArguments.Create(root, SourceArguments.Create(recursive, SourceArguments.Empty)));
    }

    private sealed class Metrics : ISourceDiagnosticsSink
    {
        public Dictionary<string, long> Values { get; } = new();
        public IDisposable Measure(string name, SourceDiagnosticOperation operation) => new Noop();
        public void AddRowsProduced(long count) { }
        public void AddBytesRead(long bytes) { }
        public void AddMetric(string name, long value) => Values[name] = Values.GetValueOrDefault(name) + value;
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    [TestMethod]
    public void MeasurePatternSelectivity()
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-early-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (var i = 0; i < 1000; i++) File.WriteAllText(Path.Combine(root, $"{i:D4}.txt"), "content");
            foreach (var (pattern, expected) in new[] { ("^000", 10), ("^[0-4]", 1000), ("^0[0-4]", 500), (".", 1000) })
            {
                var schema = new OsSchema();
                var identity = new SourceIdentity("os", "s", "s", "files");
                var plan = schema.TryPlanSource("files", new() { Identity = identity,
                    Predicate = new SourcePredicateRegexMatch(new("Name"), pattern, TimeSpan.FromSeconds(2)) });
                int ReadRows<TArgs>(TArgs arguments) where TArgs : struct, ISourceArguments => schema.GetRowSource<FileEntity, TArgs>("files",
                    RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan),
                    arguments).Chunks.Sum(chunk => chunk.Count);
                int Read() => ReadRows(SourceArguments.Create(root, SourceArguments.Create(false, SourceArguments.Empty)));
                Assert.AreEqual(expected, Read());
                var allocated = GC.GetTotalAllocatedBytes(true);
                var timer = Stopwatch.StartNew();
                Assert.AreEqual(expected, Read());
                timer.Stop();
                Console.WriteLine($"pattern={pattern}; rows={expected}; elapsedMs={timer.Elapsed.TotalMilliseconds}; allocatedBytes={GC.GetTotalAllocatedBytes(true) - allocated}");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}

