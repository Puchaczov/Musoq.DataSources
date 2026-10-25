using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.SeparatedValues.Tests.Infrastructure;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;
using Musoq.DataSources.SeparatedValues.Components.Planning;
using Musoq.DataSources.SeparatedValues.Components.Contracts;
using Musoq.DataSources.SeparatedValues.Components.Execution;
using Musoq.DataSources.SeparatedValues.Components.Parsing;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public class EarlyPatternTests
{
    [TestMethod]
    public void SparseSlots_AreClearedAfterMaterializationAndRejection()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, "Name,Other\n\"keep\"\"one\",other\nreject,other2\n");
        try
        {
            var schema = new SeparatedValuesSchema();
            var plan = schema.TryPlanSource("comma", new() { Identity = SourceIdentity.Empty,
                Predicate = new SourcePredicateRegexMatch(new("Name"), "^keep", TimeSpan.FromMilliseconds(31)), RequiredColumns = [new("Name"), new("Other")] }, path, true, 0);
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan,
                allColumns: [new SchemaColumn("Name", 0, typeof(string)), new SchemaColumn("Other", 1, typeof(string))]);
            var contract = SeparatedValuesSourceContract.From(plan.ExecutionPlan);
            var shape = new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true), new QueryRowField(1, 1, "Other", typeof(string), true)]);
            Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(contract,
                (StructuredExecutionLayout)plan.ExecutionPlan.Properties[SeparatedValuesPlanning.LayoutPropertyName]!, context.AllColumns, shape, out var mapping, out var reason), reason);
            using var kernel = SeparatedValuesRecordProgram.CompileQuery(contract, context, mapping!).CreateExecutor();
            var projector = kernel.CreateQueryProjector<string, NameMaterializer>();
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            reader.TryRead(out _);
            Assert.IsTrue(reader.TryRead(out var first));
            Assert.IsTrue(kernel.Prepare(first, 1));
            Assert.AreEqual(1, kernel.ReuseSlots);
            Assert.AreEqual(1, kernel.RetainedDecodedStrings);
            Assert.AreEqual("keep\"one", projector.Materialize(first, 1));
            Assert.AreEqual(0, kernel.RetainedDecodedStrings);
            Assert.IsTrue(reader.TryRead(out var rejected));
            Assert.IsFalse(kernel.Prepare(rejected, 2));
            Assert.AreEqual(0, kernel.RetainedDecodedStrings);
            var malformedPath = path + ".malformed";
            File.WriteAllText(malformedPath, "reject,other,extra\n");
            try
            {
                using var malformed = new SeparatedValuesUtf8Reader(malformedPath, (byte)',');
                Assert.IsTrue(malformed.TryRead(out var bad));
                var failed = false;
                try { kernel.Prepare(bad, 3); }
                catch (StructuredSchemaDriftException) { failed = true; }
                Assert.IsTrue(failed, "Rejected records must still fail width validation.");
                Assert.AreEqual(0, kernel.RetainedDecodedStrings);
            }
            finally { File.Delete(malformedPath); }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void EscapedPredicateAndOutput_ShareOnlyOverlappingDecodedValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, "Name,Age\n\"keep\"\"value\",1\nreject,2\n");
        try
        {
            var metrics = new PatternWorkMetrics();
            var source = Open(path, metrics, new SourcePredicateLogical(SourcePredicateLogicalOperator.And,
                new SourcePredicateRegexMatch(new("Name"), "^keep", TimeSpan.FromMilliseconds(31)),
                new SourcePredicateStringMatch(new("Name"), SourceStringMatchKind.Contains, "%value%", "value")));
            CollectionAssert.AreEqual(new[] { "keep\"value" }, source.Chunks.SelectMany(chunk => chunk).ToArray());
            Assert.AreEqual(1L, metrics["Pattern.ReuseSlots"]);
            Assert.AreEqual(2L, metrics["Pattern.DecodedStrings"]);
            Assert.AreEqual(2L, metrics["Pattern.ReusedStrings"]);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void RejectedRecord_StillValidatesSampledNumericTail()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, "Name,Age\nkeep,1\nreject,invalid\n");
        try
        {
            var metrics = new PatternWorkMetrics();
            var source = Open(path, metrics, new SourcePredicateRegexMatch(new("Name"), "^keep", TimeSpan.FromMilliseconds(31)),
                new Dictionary<string, string> { [SeparatedValuesInferenceOptions.MaximumRowsSettingName] = "1", [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000" });
            var failure = Assert.ThrowsException<FormatException>(() => source.Chunks.ToArray());
            StringAssert.Contains(failure.Message, "Age");
            Assert.AreEqual(2L, metrics["Pattern.DecodedStrings"]);
        }
        finally { File.Delete(path); }
    }

    private static RowSource<string> Open(string path, PatternWorkMetrics metrics, SourcePredicateExpression predicate, IReadOnlyDictionary<string, string>? settings = null)
    {
        var schema = new SeparatedValuesSchema();
        var plan = schema.TryPlanSource("comma", new() { Identity = new("separatedvalues", "s", "s", "comma"), Predicate = predicate,
            RequiredColumns = [new("Name")], SourceRuntimeSettings = settings ?? new Dictionary<string, string>() }, path, true, 0);
        var context = RuntimeV2TestContexts.CreateExecutionContext(allColumns: [new SchemaColumn("Name", 0, typeof(string))],
            executionPlan: plan.ExecutionPlan, sourceRuntimeSettings: settings, sourceDiagnostics: new SourceDiagnostics(metrics));
        return NativeSourceCalls.Query((IQueryScopedRowSourceSchema)schema, SourceArguments.Create(path, SourceArguments.Create(true, SourceArguments.Create(0, SourceArguments.Empty))))
            .Read<string, NameMaterializer>("comma", new QueryScopedRowSourceRequest(context, new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true)])));
    }
    private readonly struct NameMaterializer : IQueryRowMaterializer<string>
    {
        public static string Materialize<TReader>(scoped ref TReader reader) where TReader : IQuerySourceFieldReader, allows ref struct => reader.Read<string>(0);
    }

    [TestMethod]
    public void MeasurePatternSelectivity()
    {
        var path = Path.Combine(Path.GetTempPath(), "musoq-reuse-" + Guid.NewGuid().ToString("N") + ".separatedvalues");
        File.WriteAllText(path, "Name\n" + string.Join("\n", Enumerable.Range(0, 1000).Select(i => "\"" + i.ToString("D4") + "\"\"" + new string('x', 1024) + "\"")));
        try
        {
            var queryPath = path.Replace('\\', '/');
            foreach (var (pattern, expected) in new[] { ("^000", 10), ("^0[0-4]", 500), ("^0", 1000) })
            {
                using var compiled = InstanceCreatorHelpers.CompileForExecution(
                    $"select d.Name from separatedvalues.comma('{queryPath}', true, 0) d where d.Name rlike '{pattern}'",
                    "Reuse_" + Guid.NewGuid().ToString("N"), new CsvSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
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
