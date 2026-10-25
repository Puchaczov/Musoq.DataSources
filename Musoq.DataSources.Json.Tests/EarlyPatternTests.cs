using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Moq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
[DoNotParallelize]
public class EarlyPatternTests
{
    [TestMethod]
    public void DuplicateProperties_StillFailDiscovery()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"Name\":\"first\",\"Name\":\"last\"}");
        try { Assert.ThrowsException<Musoq.DataSources.Structured.StructuredDuplicateFieldException>(() => JsonSchemaDiscovery.GetSnapshot(path)); }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void DuplicatePropertiesAndMultiplePredicates_ReuseOnlyTheFinalValue()
    {
        var record = "{\"Name\":\"first\",\"Name\":\"last\\n\\u017b\"}";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{\"Name\":\"seed\"}");
        try
        {
            var predicate = new SourcePredicateLogical(SourcePredicateLogicalOperator.And,
                new SourcePredicateRegexMatch(new("Name"), "^last", TimeSpan.FromMilliseconds(31)),
                new SourcePredicateStringMatch(new("Name"), SourceStringMatchKind.Contains, "%a%", "a"));
            var metrics = new PatternWorkMetrics();
            var context = Context(path, predicate, metrics);
            var rows = new List<string>();
            var writer = new Mock<IChunkWriter<string>>();
            writer.Setup(value => value.Write(It.IsAny<IReadOnlyList<string>>())).Callback((IReadOnlyList<string> chunk) => rows.AddRange(chunk));
            using (var processor = new JsonQueryRowProcessor<string, NameMaterializer>(JsonSchemaDiscovery.GetSnapshot(path), context,
                new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true)]), writer.Object, null, default))
            {
                Assert.AreEqual(1, processor.ReuseSlots);
                processor.Process(Encoding.UTF8.GetBytes(record));
                Assert.AreEqual(0, processor.RetainedDecodedStrings);
                processor.Complete();
            }
            CollectionAssert.AreEqual(new[] { "last\nŻ" }, rows);
            Assert.AreEqual(1L, metrics["Pattern.ReuseSlots"]);
            Assert.AreEqual(2L, metrics["Pattern.DecodedStrings"]);
            Assert.AreEqual(3L, metrics["Pattern.ReusedStrings"]);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ReusePreservesPoolingAndClearsRejectedAndCancelledRecords()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "[{\"Name\":\"Ada\"},{\"Name\":\"Ada\"},{\"Name\":\"Reject\"}]");
        try
        {
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var rows = new List<string>();
            var writer = new Mock<IChunkWriter<string>>();
            writer.Setup(value => value.Write(It.IsAny<IReadOnlyList<string>>())).Callback((IReadOnlyList<string> chunk) => rows.AddRange(chunk));
            using var cancellation = new CancellationTokenSource();
            var context = Context(path, new SourcePredicateRegexMatch(new("Name"), "^A", TimeSpan.FromMilliseconds(31)), new PatternWorkMetrics());
            using var processor = new JsonQueryRowProcessor<string, NameMaterializer>(snapshot, context,
                new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true)]), writer.Object, null, cancellation.Token);
            processor.Process("{\"Name\":\"Ada\"}"u8);
            processor.Process("{\"Name\":\"Ada\"}"u8);
            processor.Process("{\"Name\":\"Reject\"}"u8);
            processor.Complete();
            Assert.AreEqual(2, rows.Count);
            Assert.AreSame(rows[0], rows[1]);
            Assert.AreEqual(1, snapshot.StringPool.RetainedValueCount);
            Assert.AreEqual(0, processor.RetainedDecodedStrings);
            cancellation.Cancel();
            Assert.ThrowsException<OperationCanceledException>(() => processor.Process("{\"Name\":\"Ada\"}"u8));
            Assert.AreEqual(0, processor.RetainedDecodedStrings);
            using var failing = new JsonQueryRowProcessor<string, FailingMaterializer>(snapshot, context,
                new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true)]), writer.Object, null, default);
            Assert.ThrowsException<InvalidOperationException>(() => failing.Process("{\"Name\":\"Ada\"}"u8));
            Assert.AreEqual(0, failing.RetainedDecodedStrings);
        }
        finally { File.Delete(path); }
    }

    private static SourceExecutionContext Context(string path, SourcePredicateExpression predicate, PatternWorkMetrics metrics)
    {
        var plan = new JsonSchema().TryPlanSource("file", new() { Identity = new("json", "s", "s", "file"), Predicate = predicate,
            RequiredColumns = [new("Name")] }, path);
        return RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
    }
    private readonly struct NameMaterializer : IQueryRowMaterializer<string>
    {
        public static string Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct => reader.Read<string>(0);
    }
    private readonly struct FailingMaterializer : IQueryRowMaterializer<string>
    {
        public static string Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct
        { _ = reader.Read<string>(0); throw new InvalidOperationException("materializer failure"); }
    }

    [TestMethod]
    public void MeasurePatternSelectivity()
    {
        var path = Path.Combine(Path.GetTempPath(), "musoq-reuse-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(Enumerable.Range(0, 1000).Select(i => new { Name = i.ToString("D4") + "\n" + new string('x', 1024) })));
        try
        {
            var queryPath = path.Replace('\\', '/');
            foreach (var (pattern, expected) in new[] { ("^000", 10), ("^0[0-4]", 500), ("^0", 1000) })
            {
                using var compiled = InstanceCreatorHelpers.CompileForExecution(
                    $"select d.Name from json.file('{queryPath}') d where d.Name rlike '{pattern}'",
                    "Reuse_" + Guid.NewGuid().ToString("N"), new JsonSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
                using (var warmup = compiled.Run()) Assert.AreEqual(expected, warmup.Count);
                var allocated = GC.GetTotalAllocatedBytes(true);
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) { using var result = compiled.Run(); Assert.AreEqual(expected, result.Count); }
                timer.Stop();
                Console.WriteLine($"admitted={expected}/1000; iterations=5; elapsedMs={timer.Elapsed.TotalMilliseconds}; allocatedBytes={GC.GetTotalAllocatedBytes(true) - allocated}");
            }
        }
        finally { File.Delete(path); }
    }
}
