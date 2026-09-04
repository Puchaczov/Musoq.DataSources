#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Exceptions;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SeparatedValuesQueryRowInspectionTests
{
    private static readonly ILoggerResolver LoggerResolver = new NullLoggerResolver();
    private static readonly CompilationOptions CompilationOptions = new(
        ParallelizationMode.Full,
        usePrimitiveTypeValidation: true);

    [TestMethod]
    public void CompileForInspection_WhenEnabledAndExact_EmitsTypedQueryRowReads()
    {
        WithCsv("Name,Team,Age\nAda,A,36\n", path =>
        {
            var code = Inspect(
                $"select d.Name, d.Age from separatedvalues.comma('{QueryPath(path)}', true, 0) d " +
                "where d.Age >= 30 order by d.Age, d.Name skip 0 take 2").GeneratedCSharpCode;

            StringAssert.Contains(code, "GetQueryScopedRowSource<");
            StringAssert.Contains(code, "IQueryRowMaterializer<");
            StringAssert.Contains(code, "Read<long?>(0)");
            StringAssert.Contains(code, "Read<string>(1)");
            Assert.IsFalse(code.Contains("GetRowSource<object", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void CompileForInspection_WhenCorePlannerFallsBack_EmitsGuardedLegacyCall()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var code = Inspect(
                $"select d.Name, d.Age from separatedvalues.comma('{QueryPath(path)}', true, 0) d",
                new ForcedLegacySchemaProvider()).GeneratedCSharpCode;

            Assert.IsFalse(code.Contains("GetQueryScopedRowSource<", StringComparison.Ordinal));
            StringAssert.Contains(code, "GetRowSource<object");
        });
    }

    [TestMethod]
    public void CompiledQuery_WhenCorePlannerFallsBack_ThrowsLegacyGuardBeforeFileAccess()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var query = $"select d.Name from separatedvalues.comma('{QueryPath(path)}', true, 0) d";
            using var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesForbiddenLegacy_{Guid.NewGuid():N}",
                new ForcedLegacySchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            File.Delete(path);

            using var table = compiled.Run();
            var exception = Assert.ThrowsExactly<QueryExecutionException>(() =>
            {
                _ = table.Count;
            });
            var messages = FlattenMessages(exception);

            StringAssert.Contains(messages, "separatedvalues.comma");
            StringAssert.Contains(messages, "System.Object[]");
            StringAssert.Contains(messages, "core planner selected unsupported legacy row transfer");
        });
    }

    [TestMethod]
    public void CompileForInspection_WhenTableIsCoupled_EmitsTypedTableShape()
    {
        WithCsv("Name,Amount\nAda,12\n", path =>
        {
            var query =
                "table CsvShape { Name: string, Amount: int };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Name, Amount from Rows('{QueryPath(path)}', true, 0)";
            var code = Inspect(query).GeneratedCSharpCode;

            StringAssert.Contains(code, "GetQueryScopedRowSource<");
            StringAssert.Contains(code, "Read<string>(0)");
            StringAssert.Contains(code, "Read<int?>(1)");
        });
    }

    [TestMethod]
    public void CompileForInspection_WhenCapabilityChanges_DoesNotCrossContaminateCachedRouting()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var query = $"select d.Name from separatedvalues.comma('{QueryPath(path)}', true, 0) d";

            var enabledFirst = Inspect(query).GeneratedCSharpCode;
            var disabled = Inspect(query, new ForcedLegacySchemaProvider()).GeneratedCSharpCode;
            var enabledAgain = Inspect(query).GeneratedCSharpCode;

            StringAssert.Contains(enabledFirst, "GetQueryScopedRowSource<");
            Assert.IsFalse(disabled.Contains("GetQueryScopedRowSource<", StringComparison.Ordinal));
            StringAssert.Contains(disabled, "GetRowSource<object");
            StringAssert.Contains(enabledAgain, "GetQueryScopedRowSource<");
        });
    }

    [TestMethod]
    public void CompileForInspection_WhenDescriptorDoesNotAdvertiseUsableMetadata_FallsBackToDeclaredRows()
    {
        WithCsv("Payload\nvalue\n", path =>
        {
            var query = $"select d.Payload from separatedvalues.comma('{QueryPath(path)}', true, 0) d";
            var code = Inspect(query, new ForcedLegacySchemaProvider()).GeneratedCSharpCode;

            Assert.IsFalse(code.Contains("GetQueryScopedRowSource<", StringComparison.Ordinal));
            StringAssert.Contains(code, "GetRowSource<object");
        });
    }

    [TestMethod]
    public void CompileForInspection_SelectsStructForScanLocalAndClassForEscapingJoinRows()
    {
        WithCsv("Name,Age\nAda,36\nGrace,41\n", path =>
        {
            var source = $"separatedvalues.comma('{QueryPath(path)}', true, 0)";
            var scanCode = Inspect(
                $"select d.Name from {source} d where d.Age > 0").GeneratedCSharpCode;
            var joinCode = Inspect(
                $"select l.Name, r.Name from {source} l inner join {source} r on l.Age = r.Age").GeneratedCSharpCode;

            StringAssert.Contains(scanCode, "private readonly struct QueryRow_");
            StringAssert.Contains(joinCode, "private sealed class QueryRow_");
        });
    }

    private static QueryInspectionResult Inspect(string query)
    {
        return Inspect(query, new CsvSchemaProvider());
    }

    private static QueryInspectionResult Inspect(string query, ISchemaProvider provider)
    {
        return InstanceCreator.CompileForInspection(
            query,
            $"SeparatedValuesInspection_{Guid.NewGuid():N}",
            provider,
            LoggerResolver,
            CompilationOptions);
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(Environment.NewLine, messages);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-query-inspection-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));
        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class NullLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;

        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }

    private sealed class ForcedLegacySchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => new ForcedLegacySeparatedValuesSchema();
    }

    private sealed class ForcedLegacySeparatedValuesSchema : SeparatedValuesSchema
    {
        public ForcedLegacySeparatedValuesSchema()
            : base()
        {
        }

        public override SourceDescriptor DescribeSource(
            string name,
            SourceDescribeContext context,
            params object?[] parameters)
        {
            return base.DescribeSource(name, context, parameters) with
            {
                TransferCapabilities = SourceTransferCapabilities.None
            };
        }
    }

}
