#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests.Common;

/// <summary>Executes every advertised string binding through the actual provider row source.</summary>
public static class PatternContractVerifier
{
    public const int CasesPerColumn = 10;

    public static RowVerifier<TRow> For<TRow>() => new();
    public readonly struct RowVerifier<TRow>
    {
        public int Verify<TArguments>(ISchema schema, string source, TArguments arguments, object?[] metadataArguments, params string[] exclusions)
            where TArguments : struct, ISourceArguments => Verify<TRow, TArguments>(schema, source, arguments, metadataArguments, exclusions);
    }

    public static int Verify<TRow, TArguments>(ISchema schema, string source, TArguments arguments,
        object?[] metadataArguments, params string[] excludedColumns)
        where TArguments : struct, ISourceArguments
    {
        return VerifySource(schema, source, metadataArguments, context => schema.GetRowSource<TRow, TArguments>(source, context, arguments), excludedColumns);
    }

    public static int VerifySource<TRow>(ISchema schema, string source, object?[] metadataArguments,
        Func<SourceExecutionContext, RowSource<TRow>> read, params string[] excludedColumns)
    {
        var identity = new SourceIdentity(schema.Name, "s", "s", source);
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: SourceExecutionPlan.Empty(identity));
        var descriptor = schema.DescribeSource(source, new SourceDescribeContext(identity, context), metadataArguments);
        var strings = descriptor.Columns.Where(column => column.ColumnType == typeof(string) && !excludedColumns.Contains(column.ColumnName)).ToArray();
        var advertised = descriptor.PredicateCapabilities.StringMatches.Select(column => column.Column.Name).OrderBy(name => name).ToArray();
        var expectedColumns = strings.Select(column => column.ColumnName).OrderBy(name => name).ToArray();
        if (!expectedColumns.SequenceEqual(advertised)) throw new InvalidOperationException($"{schema.Name}.{source}: incomplete string capability coverage: expected {string.Join(",", expectedColumns)}; got {string.Join(",", advertised)}.");
        if (strings.Length == 0) return 0;
        var baseline = read(context).Chunks.SelectMany(chunk => chunk).ToArray();
        if (baseline.Length == 0) throw new InvalidOperationException($"{schema.Name}.{source}: the pattern fixture must contain rows.");
        foreach (var column in strings)
        {
            var property = typeof(TRow).GetProperty(column.ColumnName) ?? throw new InvalidOperationException("Missing test row property " + column.ColumnName);
            string? Read(TRow row) => (string?)property.GetValue(row);
            var values = baseline.Select(Read).ToArray();
            foreach (var kind in Enum.GetValues<SourceStringMatchKind>().Select(value => (SourceStringMatchKind?)value).Append(null))
            foreach (var negated in new[] { false, true })
            {
                var regex = kind is null;
                var pattern = kind switch { SourceStringMatchKind.Exact => "a", SourceStringMatchKind.Prefix => "a%", SourceStringMatchKind.Suffix => "%a", _ => "%a%" };
                SourcePredicatePatternMatch predicate = regex
                    ? new SourcePredicateRegexMatch(new SourceColumnRef(column.ColumnName), "(?i)a", TimeSpan.FromMilliseconds(17), isNegated: negated)
                    : new SourcePredicateStringMatch(new SourceColumnRef(column.ColumnName), kind!.Value, pattern, "a", isNegated: negated);
                if (!descriptor.PredicateCapabilities.Supports(predicate, SourcePredicateEvaluationPhase.RowFiltering)) throw new InvalidOperationException("Missing advertised pattern operation.");
                var request = new SourcePlanRequest { Identity = identity, Predicate = predicate, RequiredColumns = new[] { new SourceColumnRef(descriptor.Columns.FirstOrDefault(item => item.ColumnName != column.ColumnName)?.ColumnName ?? column.ColumnName) } };
                var result = schema.TryPlanSource(source, request, metadataArguments);
                if (!ReferenceEquals(predicate, result.AcceptedPredicate) || result.ResidualPredicate is not null || result.ExecutionPlan.PredicateApplications.Count != 1 || !ReferenceEquals(result.ExecutionPlan.PredicateApplications[0].Predicate, predicate) || result.ExecutionPlan.PredicateApplications[0].Phase != SourcePredicateEvaluationPhase.RowFiltering)
                    throw new InvalidOperationException($"{schema.Name}.{source}.{column.ColumnName}: pattern was not accepted exactly once.");
                var execution = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: result.ExecutionPlan);
                var actual = read(execution).Chunks.SelectMany(chunk => chunk).Select(Read).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var matcher = Operators.PrepareLike(pattern);
                var expected = values.Where(value => (regex ? Operators.RLikeDynamic(value, "(?i)a") : Operators.LikePrepared(value, matcher)) != negated).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (!expected.SequenceEqual(actual)) throw new InvalidOperationException($"{schema.Name}.{source}.{column.ColumnName}: {(regex ? "RLIKE" : "LIKE")} negated={negated} differs from Core baseline ({expected.Length} vs {actual.Length} rows).");
            }
        }
        var repository = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        while (repository is not null && !System.IO.File.Exists(System.IO.Path.Combine(repository.FullName, "Directory.Build.props"))) repository = repository.Parent;
        if (repository is not null)
        {
            var directory = System.IO.Path.Combine(repository.FullName, "artifacts", "pattern-pushdown", "coverage");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, schema.Name.ToLowerInvariant() + "." + source + ".json"),
                System.Text.Json.JsonSerializer.Serialize(new { schema = schema.Name, source, rowType = typeof(TRow).FullName, columns = expectedColumns, excludedColumns, patternsPerColumn = CasesPerColumn, fixtureRows = baseline.Length }));
        }
        return strings.Length;
    }
}
