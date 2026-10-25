using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public class EarlyPatternTests
{
    [TestMethod]
    public void RejectionsAvoidWrappers_KeepRawEntryIndexes_AndReleaseArchive()
    {
        var path = Path.Combine(Path.GetTempPath(), "musoq-early-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
                foreach (var name in new[] { "reject0", "keep", "reject2" })
                { using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write(name + " content"); }
            var schema = new ArchivesSchema();
            var predicate = new SourcePredicateRegexMatch(new("Key"), "^keep$", TimeSpan.FromMilliseconds(31));
            var plan = schema.TryPlanSource("file", new() { Identity = new("archives", "s", "s", "file"), Predicate = predicate });
            var metrics = new PatternWorkMetrics();
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
            var row = schema.GetRowSource<EntryWrapper, SourceArguments<string, EmptySourceArguments>>("file", context, SourceArguments.Create(path, SourceArguments.Empty)).Chunks.SelectMany(chunk => chunk).Single();
            Assert.AreEqual("keep content", new ArchivesLibrary().GetTextContent(row));
            Assert.AreEqual(3L, metrics["Pattern.Candidates"]);
            Assert.AreEqual(2L, metrics["Pattern.RejectedCandidates"]);
            Assert.AreEqual(1L, metrics["Pattern.ConstructedRows"]);
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }
}
