using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Json;
using Musoq.DataSources.SeparatedValues;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public class ComputedProjectionIntegrationTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ComputedOnlyInputs_AreAbsentFromCarrier_AndMatchEngineFallback(bool csv)
    {
        WithFile(csv, (path, source) =>
        {
            var probe = new Probe();
            var sql = $"select Optional is null as Missing from {source} s";
            var optimized = Run(sql, csv, probe);
            var fallback = Run(sql, csv, new Probe { Enabled = false });
            CollectionAssert.AreEqual(new object?[] { false, true, false }, optimized.Select(row => row[0]).ToArray());
            AssertSame(fallback, optimized);
            Assert.IsTrue(probe.Accepted > 0, "The query must exercise provider computation.");
            Assert.IsNotNull(probe.Shape);
            Assert.IsTrue(probe.Shape.Fields.All(field => field.ComputedProjection is not null), "Input-only columns must stay out of the row carrier.");
        });
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PortableArithmeticComparisonAndCoalesce_MatchFallback(bool csv)
    {
        WithFile(csv, (path, source) =>
        {
            foreach (var projection in new[] { "Id + 0", "Optional > 2", "Optional ?? 9" })
            {
                var sql = $"select {projection} as Value from {source} s";
                var probe = new Probe();
                var optimized = Run(sql, csv, probe);
                AssertSame(Run(sql, csv, new Probe { Enabled = false }), optimized);
                Assert.IsTrue(probe.Accepted > 0, projection);
                var expected = projection == "Id + 0" ? new object?[] { 1L, 2L, 3L }
                    : projection == "Optional > 2" ? [false, null, true] : [2L, 9L, 4L];
                CollectionAssert.AreEqual(expected, optimized.Select(row => row[0]).ToArray());
            }
        });
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MixedProjection_FilterPagingAndRepeatedExecution_MatchFallback(bool csv)
    {
        WithFile(csv, (path, source) =>
        {
            var probe = new Probe();
            var sql = $"select Id + 0 as Value, Optional is null as Missing, ToUpper(Name) as Label from {source} s where Id >= 1 order by Id skip 1 take 2";
            var optimized = Run(sql, csv, probe);
            AssertSame(Run(sql, csv, new Probe { Enabled = false }), optimized);
            Assert.AreEqual(2, optimized.Length);
            Assert.AreEqual("B", optimized[0][2]);
            Assert.AreEqual("C", optimized[1][2]);
            Assert.AreEqual(0, probe.Accepted, "Core keeps conditional and mixed callable regions in engine execution.");
            AssertSame(optimized, Run(sql, csv, new Probe()));
        });
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnsafeArithmetic_RemainsInEngine_AndPreservesFailure(bool csv)
    {
        WithFile(csv, (path, source) =>
        {
            var probe = new Probe();
            Assert.Throws<Exception>(() => Run($"select Id / 0 from {source} s", csv, probe));
            Assert.AreEqual(0, probe.Accepted);
        });
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ParallelComputedReaders_PreserveValuesAndOrder(bool csv)
    {
        WithFile(csv, (path, source) =>
        {
            var values = Enumerable.Range(0, 12000).ToArray();
            File.WriteAllText(path, csv
                ? "Id,Optional,Name\n" + string.Join("\n", values.Select(id => $"{id},{(id % 2 == 0 ? "" : "1")},row"))
                : "[" + string.Join(",", values.Select(id => $"{{\"Id\":{id},\"Optional\":{(id % 2 == 0 ? "null" : "1")}}}")) + "]");
            var sql = $"select Optional is null as Missing from {source} s";
            var sequential = Run(sql, csv, new Probe());
            var probe = new Probe();
            var parallel = Run(sql, csv, probe, 4);
            Assert.AreEqual(values.Length, parallel.Length);
            AssertSame(sequential, parallel);
            Assert.IsTrue(probe.Accepted > 0);
            for (var index = 0; index < parallel.Length; index++) Assert.AreEqual(index % 2 == 0, parallel[index][0]);
        });
    }

    private static object?[][] Run(string sql, bool csv, Probe probe, int parallelism = 1)
    {
        using var compiled = InstanceCreatorHelpers.CompileForExecution(sql, "Computed_" + Guid.NewGuid().ToString("N"),
            new Provider(csv, probe), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
                new Dictionary<string, string> { [csv ? "separatedvalues.max_parallelism" : "json.max_parallelism"] = parallelism.ToString() }));
        using var result = compiled.Run();
        return result.Select(row => row.ReadValues()).ToArray();
    }

    private static void AssertSame(object?[][] expected, object?[][] actual)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        for (var index = 0; index < actual.Length; index++) CollectionAssert.AreEqual(expected[index], actual[index]);
    }

    private static void WithFile(bool csv, Action<string, string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "computed-" + Guid.NewGuid().ToString("N") + (csv ? ".csv" : ".json"));
        File.WriteAllText(path, csv ? "Id,Optional,Name\n1,2,a\n2,,b\n3,4,c\n" : "[{\"Id\":1,\"Optional\":2,\"Name\":\"a\"},{\"Id\":2,\"Optional\":null,\"Name\":\"b\"},{\"Id\":3,\"Optional\":4,\"Name\":\"c\"}]");
        try
        {
            var literal = path.Replace('\\', '/');
            action(path, csv ? $"separatedvalues.comma('{literal}', true, 0)" : $"json.file('{literal}')");
        }
        finally { File.Delete(path); }
    }

    private sealed class Probe
    {
        public bool Enabled = true;
        public int Accepted;
        public QueryRowShape? Shape;
        public SourceDescriptor Describe(SourceDescriptor descriptor) => descriptor with
        { ComputedProjectionCapabilities = Enabled ? descriptor.ComputedProjectionCapabilities : SourceComputedProjectionCapabilities.None };
        public SourcePlanResult Plan(SourcePlanResult result) { Accepted += result.AcceptedComputedProjections.Count; return result; }
    }

    private sealed class Provider(bool csv, Probe probe) : ISchemaProvider
    {
        private readonly ISchema _schema = csv ? new CsvProbe(probe) : new JsonProbe(probe);
        public ISchema GetSchema(string schema) => _schema;
    }

    private sealed class JsonProbe(Probe probe) : JsonSchema, IQueryScopedRowSourceSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object?[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
            => probe.Plan(base.TryPlanSource(name, request, parameters));
        RowSource<T> IQueryScopedRowSourceSchema.GetQueryScopedRowSource<T, TMaterializer, TArguments>(string name, QueryScopedRowSourceRequest request, TArguments arguments)
        {
            probe.Shape = request.Shape;
            return GetQueryScopedRowSource<T, TMaterializer, TArguments>(name, request, arguments);
        }
    }

    private sealed class CsvProbe(Probe probe) : SeparatedValuesSchema, IQueryScopedRowSourceSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object?[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
            => probe.Plan(base.TryPlanSource(name, request, parameters));
        RowSource<T> IQueryScopedRowSourceSchema.GetQueryScopedRowSource<T, TMaterializer, TArguments>(string name, QueryScopedRowSourceRequest request, TArguments arguments)
        {
            probe.Shape = request.Shape;
            return GetQueryScopedRowSource<T, TMaterializer, TArguments>(name, request, arguments);
        }
    }
}
