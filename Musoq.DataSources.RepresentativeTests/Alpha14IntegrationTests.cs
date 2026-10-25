using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Json;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Os;
using Musoq.DataSources.Search;
using Musoq.DataSources.SeparatedValues;
using Musoq.DataSources.System;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class Alpha14IntegrationTests
{
    private string _root = null!;
    private static readonly string?[] Names = [null, "", "Alpha", "alpha", "beta", "Łódź", "aab", "quote\"x", "line\nx"];

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "musoq-alpha14-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "rows.json"), JsonSerializer.Serialize(Names.Select((name, id) => new { Id = id, Name = name })));
        File.WriteAllText(Path.Combine(_root, "rows.csv"), "Id,Name\n" + string.Join("\n", Names.Select((name, id) =>
            id + "," + (name is null ? "" : "\"" + name.Replace("\"", "\"\"") + "\""))) + "\n");
        File.WriteAllLines(Path.Combine(_root, "rows.txt"), new[] { "", "Alpha", "alpha", "beta", "Łódź", "a.b", "quote\"x" });
        foreach (var name in new[] { "Alpha.txt", "alpha.log", "beta.txt", "Łódź.txt", "aab.txt" })
            File.WriteAllText(Path.Combine(_root, name), "fixture");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [DataTestMethod]
    [DataRow("json", "^Al", false)]
    [DataRow("json", "(?i)^al", false)]
    [DataRow("json", "a", true)]
    [DataRow("json", "^$", false)]
    [DataRow("json", "Ł", false)]
    [DataRow("json", "^(.)\\1", false)]
    [DataRow("json", "quote\"", false)]
    [DataRow("json", "(?s)line.x", false)]
    [DataRow("csv", "^Al", false)]
    [DataRow("csv", "(?i)^al", false)]
    [DataRow("csv", "a", true)]
    [DataRow("csv", "^$", false)]
    [DataRow("csv", "Ł", false)]
    [DataRow("csv", "^(.)\\1", false)]
    [DataRow("csv", "quote\"", false)]
    [DataRow("csv", "(?s)line.x", false)]
    [DataRow("os", "^Al", false)]
    [DataRow("os", "(?i)^al", false)]
    [DataRow("os", "a", true)]
    [DataRow("os", "^$", false)]
    [DataRow("os", "Ł", false)]
    [DataRow("os", "^(.)\\1", false)]
    [DataRow("os", "quote\"", false)]
    [DataRow("os", "(?s)line.x", false)]
    [DataRow("search", "^Al", false)]
    [DataRow("search", "(?i)^al", false)]
    [DataRow("search", "a", true)]
    [DataRow("search", "^$", false)]
    [DataRow("search", "Ł", false)]
    [DataRow("search", "^(.)\\1", false)]
    [DataRow("search", "quote\"", false)]
    [DataRow("search", "(?s)line.x", false)]
    public void RegexPushdown_MatchesCoreResidualEvaluation(string provider, string pattern, bool negated)
    {
        var probe = new Probe();
        var source = Source(provider);
        var column = provider == "search" ? "Path" : "Name";
        var query = $"select d.{column} from {source} d where d.{column} {(negated ? "not " : "")}rlike '{pattern.Replace("'", "''")}' order by d.{column}";
        var actual = Run(query, new Provider(probe));
        Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateRegexMatch), "Expected an accepted regex predicate.");
        Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 1));
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), actual);
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    [DataRow("os")]
    [DataRow("search")]
    public void Regex_WithResidualPredicateAndPaging_MatchesCore(string provider)
    {
        var probe = new Probe();
        var column = provider == "search" ? "Path" : "Name";
        var query = $"select d.{column} from {Source(provider)} d where d.{column} rlike 'a' and d.{column} || '' <> 'alpha' order by d.{column} skip 1 take 2";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedSkip is null && plan.AcceptedTake is null));
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    public void MultipleRegexes_KeepUnprojectedPredicateColumn(string provider)
    {
        var probe = new Probe();
        var query = $"select d.Id from {Source(provider)} d where d.Name rlike 'a' and d.Name not rlike '^b' order by d.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 2));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(99)]
    public void UnknownCapabilityVersion_KeepsRegexResidual(int version)
    {
        var probe = new Probe { Version = version };
        var query = $"select d.Id from {Source("json")} d where d.Name rlike 'a' order by d.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedPredicate is null));
    }

    [TestMethod]
    public void ColumnValuedPattern_KeepsRegexResidual()
    {
        var probe = new Probe();
        var query = $"select d.Id from {Source("json")} d where d.Name rlike d.Name order by d.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.All(plan => plan.ExecutionPlan.PredicateApplications.Count == 0));
    }

    [TestMethod]
    public void CrossSourceRegex_PreservesResidualJoinEvaluation()
    {
        var probe = new Probe();
        var query = $"select l.Id, r.Id from {Source("json")} l cross join {Source("json")} r where l.Name rlike r.Name order by l.Id, r.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.All(plan => plan.ExecutionPlan.PredicateApplications.Count == 0));
    }

    [TestMethod]
    public void RepeatedAndOverlappingExecutions_IsolateBoundPlans()
    {
        var queries = new[] { "^Al", "(?i)^al", "Ł", "^b" }.Select(pattern =>
            $"select d.Id from {Source("json")} d where d.Name rlike '{pattern}' order by d.Id").ToArray();
        var expected = queries.Select(query => Run(query, new Provider(new Probe { Enabled = false }))).ToArray();
        Parallel.For(0, 12, index => CollectionAssert.AreEqual(expected[index % queries.Length],
            Run(queries[index % queries.Length], new Provider(new Probe()))));
    }

    [DataTestMethod]
    [DataRow("let n: int = 2;", "$n")]
    [DataRow("param(n: int = 2)", "$n")]
    [DataRow("params(n: int = 2)", "$n")]
    public void DeclaredPagingCounts_WorkWithRealProvider(string declaration, string count)
    {
        var query = $"{declaration} select d.Id from {Source("json")} d order by d.Id skip {count} take {count}";
        CollectionAssert.AreEqual(new[] { "2", "3" }, Run(query, new Provider(new Probe())));
    }

    [TestMethod]
    public void ComputedArgumentsAndCorrelatedSubquery_WorkWithRealProvider()
    {
        var path = _root.Replace('\\', '/').Replace("'", "''");
        CollectionAssert.AreEqual(Enumerable.Range(0, 9).Select(id => id.ToString()).ToArray(),
            Run($"select d.Id from json.file('{path}' + '/rows.json') d order by d.Id", new Provider(new Probe())));
        var query = $"select r.Value from system.range(0, 9) r where r.Value > 5 and exists " +
            $"(select d.Id from {Source("json")} d where d.Id > r.Value and d.Id > 5) order by r.Value";
        CollectionAssert.AreEqual(new[] { "6", "7" }, Run(query, new Provider(new Probe())));
    }

    [TestMethod]
    public void RevisedNullAndCaseSemantics_WorkWithRealProvider()
    {
        var query = $"select case when d.Id = 100 then 1 end, d.Name || '!' from {Source("json")} d where d.Id = 0";
        CollectionAssert.AreEqual(new[] { "<null>|<null>" }, Run(query, new Provider(new Probe())));
    }

    [TestMethod]
    public void TableCouple_RegexPreservesDeclaredStringValues()
    {
        var path = Path.Combine(_root, "rows.csv").Replace('\\', '/');
        var query = "table Shape { Id: int, Name: string }; couple separatedvalues.comma with table Shape as Rows;" +
            $"select d.Id from Rows('{path}', true, 0) d where d.Name rlike '(?i)^al' order by d.Id";
        CollectionAssert.AreEqual(new[] { "2", "3" }, Run(query, new Provider(new Probe())));
    }

    [TestMethod]
    public void RegexMatcher_ReusedQueryFollowsExecutionCulture()
    {
        File.WriteAllText(Path.Combine(_root, "rows.json"), "[{\"Id\":0,\"Name\":\"I\"}]");
        var query = $"select d.Id from {Source("json")} d where d.Name rlike '(?i)^i$'";
        using var compiled = InstanceCreatorHelpers.CompileForExecution(query, "Culture_" + Guid.NewGuid().ToString("N"),
            new Provider(new Probe()), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using var table = compiled.Run();
                Assert.AreEqual(culture == "tr-TR" ? 0 : 1, table.Count);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void RegexTimeout_PreservesPublicTypedCauseAndBudget()
    {
        File.WriteAllText(Path.Combine(_root, "rows.json"), JsonSerializer.Serialize(new[] { new { Id = 0, Name = new string('a', 50000) + "!" } }));
        var probe = new Probe();
        using var compiled = Converter.InstanceCreator.CompileForExecution(
            $"select d.Id from {Source("json")} d where d.Name rlike '^(?=(a+)+$)a+$'",
            "Timeout_" + Guid.NewGuid().ToString("N"), new Provider(probe), new SilentLoggerResolver(),
            new Evaluator.CompilationOptions().WithRegexMatchTimeout(TimeSpan.FromMilliseconds(1)));
        var failure = Assert.ThrowsException<Evaluator.Exceptions.QueryExecutionException>(() =>
        {
            using var table = compiled.Run();
            _ = table.Count;
        });
        Assert.AreEqual(Parser.Diagnostics.DiagnosticCode.MQ7017_RegexMatchTimeout, failure.Envelope!.Code);
        Exception? cause = failure;
        while (cause is not null && cause is not global::System.Text.RegularExpressions.RegexMatchTimeoutException) cause = cause.InnerException;
        Assert.IsNotNull(cause);
        var supplied = (SourcePredicateRegexMatch)probe.Plans.Single(plan => plan.AcceptedPredicate is SourcePredicateRegexMatch).AcceptedPredicate!;
        Assert.AreEqual(TimeSpan.FromMilliseconds(1), supplied.MatchTimeout);
        Assert.AreEqual(supplied.MatchTimeout, ((global::System.Text.RegularExpressions.RegexMatchTimeoutException)cause).MatchTimeout);
        Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 1));
    }

    [TestMethod]
    public void Cancellation_StopsRegexSourceAndAllowsSubsequentExecution()
    {
        using var compiled = InstanceCreatorHelpers.CompileForExecution(
            $"select d.Id from {Source("json")} d where d.Name rlike 'a'", "Cancel_" + Guid.NewGuid().ToString("N"),
            new Provider(new Probe()), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => { using var table = compiled.Run(cancellation.Token); _ = table.Count; });
        using var result = compiled.Run();
        Assert.IsTrue(result.Count > 0);
    }

    [DataTestMethod]
    [DataRow("let n: int = -1;")]
    [DataRow("param(n: int = -1)")]
    public void InvalidPaging_FailsBeforeSourceOpen(string declaration)
    {
        var probe = new Probe();
        Exception? failure = null;
        try { Run($"{declaration} select d.Id from {Source("json")} d take $n", new Provider(probe)); }
        catch (Exception exception) { failure = exception; }
        Assert.IsNotNull(failure);
        Assert.AreEqual(0, probe.Opens);
    }

    [DataTestMethod]
    [DataRow("select Value from system.range(3) order by Value", "0", "1", "2")]
    [DataRow("select Value from system.range(1, 4) order by Value", "1", "2", "3")]
    [DataRow("select Value from system.range(1L, 4L) order by Value", "1", "2", "3")]
    public void RangeArguments_PreserveLosslessIntegralWidening(string query, string first, string second, string third)
        => CollectionAssert.AreEqual(new[] { first, second, third }, Run(query, new Provider(new Probe())));

    [DataTestMethod]
    [DataRow("Name", "alpha.log")]
    [DataRow("Name", "Al%")]
    [DataRow("FileName", "%.txt")]
    [DataRow("FullPath", "%musoq-alpha14%")]
    public void NativeLikePushdown_MatchesCoreForNewAndExistingColumns(string column, string pattern)
    {
        foreach (var negated in new[] { false, true })
        {
            var probe = new Probe();
            var query = $"select d.Name from {Source("os")} d where d.{column} {(negated ? "not " : "")}like '{pattern}' order by d.Name";
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
            Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateStringMatch));
        }
    }

    [TestMethod]
    public void NativeRegexPushdown_NewPathColumn_MatchesCore()
    {
        var probe = new Probe();
        var query = $"select d.Name from {Source("os")} d where d.FullPath rlike 'musoq-alpha14' order by d.Name";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateRegexMatch));
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    [DataRow("flat")]
    public void StructuredAndTextPatterns_AllLikeShapesMatchCore(string provider)
    {
        var column = provider == "flat" ? "Line" : "Name";
        var id = provider == "flat" ? "LineNumber" : "Id";
        foreach (var pattern in new[] { "alpha", "Al%", "%ha", "%a%", "", "Ł%", "%quote\"%" })
        foreach (var negated in new[] { false, true })
        {
            var probe = new Probe();
            var query = $"select d.{id} from {Source(provider)} d where d.{column} {(negated ? "not " : "")}like '{pattern}' order by d.{id}";
            try { CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)), query); }
            catch (Exception exception) { throw new InvalidOperationException(query, exception); }
            // Core 17.0.14 transfers only ASCII LIKE literals; Unicode patterns stay residual.
            if (pattern == "Ł%") Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedPredicate is null), query);
            else
            {
                Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateStringMatch), query);
                Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 1));
            }
        }
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    [DataRow("flat")]
    public void MixedPatterns_WithResidualAndPaging_KeepProjectionDependencies(string provider)
    {
        var column = provider == "flat" ? "Line" : "Name";
        var id = provider == "flat" ? "LineNumber" : "Id";
        foreach (var residual in new[] { "", $" and d.{column} || '' <> 'alpha'" })
        {
            var probe = new Probe();
            var query = $"select d.{id} from {Source(provider)} d where d.{column} like '%a%' and d.{column} not rlike '^b'{residual} order by d.{id} skip 1 take 2";
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
            Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 2));
            if (provider != "flat" || residual.Length > 0) Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedTake is null));
        }
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    public void MissingFields_NegatedPatternsMatchCore(string provider)
    {
        File.WriteAllText(Path.Combine(_root, "rows.json"), "[{\"Id\":0},{\"Id\":1,\"Name\":null},{\"Id\":2,\"Name\":\"Alpha\"},{\"Id\":3,\"Name\":\"beta\"}]");
        File.WriteAllText(Path.Combine(_root, "rows.csv"), "Id,Name\n0\n1,\n2,Alpha\n3,beta\n");
        foreach (var op in new[] { "like", "rlike", "not like", "not rlike" })
        {
            var pattern = op.EndsWith("rlike") ? "^Al" : "Al%";
            var query = $"select d.Id from {Source(provider)} d where d.Name {op} '{pattern}' order by d.Id";
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(new Probe())), query);
        }
    }

    [TestMethod]
    public void TableCouple_LikeAndRegexRetainUnprojectedDecodedText()
    {
        var path = Path.Combine(_root, "rows.csv").Replace('\\', '/');
        var probe = new Probe();
        var query = "table Shape { Id: int, Name: string }; couple separatedvalues.comma with table Shape as Rows;" +
            $"select d.Id from Rows('{path}', true, 0) d where d.Name like '%a%' and d.Name not rlike '^b' order by d.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.Any(plan => plan.ExecutionPlan.PredicateApplications.Count == 2));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LikeAnyString_EnabledDisabledAndRejectedPushdownAgree(bool negated)
    {
        File.WriteAllText(Path.Combine(_root, "rows.json"), "[{\"Id\":0},{\"Id\":1,\"Name\":null},{\"Id\":2,\"Name\":\"\"},{\"Id\":3,\"Name\":\"Alpha\"}]");
        var query = $"select d.Id from {Source("json")} d where d.Name {(negated ? "not " : "")}like '%' order by d.Id";
        foreach (var mode in new[] { "enabled", "disabled", "rejected" })
        {
            var probe = CreateProbe(mode);
            CollectionAssert.AreEqual(negated ? new[] { "0", "1" } : new[] { "2", "3" }, Run(query, new Provider(probe)), mode);
            Assert.AreEqual(mode == "enabled" ? 1 : 0, probe.Plans.Sum(plan => plan.ExecutionPlan.PredicateApplications.Count), mode);
        }
    }

    [TestMethod]
    public void UnsupportedLikeWildcardShapes_RemainResidual()
    {
        foreach (var pattern in new[] { "a_b", "a%b%", "%a_b%" })
        {
            var probe = new Probe();
            var query = $"select d.Id from {Source("json")} d where d.Name like '{pattern}' order by d.Id";
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
            Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedPredicate is null));
        }
    }

    [DataTestMethod]
    [DataRow("matches", "MatchText", "T%")]
    [DataRow("many", "MatchText", "T%")]
    [DataRow("many", "PatternId", "a%")]
    [DataRow("lines", "LineText", "%Alpha%")]
    [DataRow("counts", "Path", "%Alpha%")]
    [DataRow("audit", "Root", "%search-text%")]
    [DataRow("bytes", "MatchText", "T%")]
    public void SearchTextPatterns_CompiledQueriesRetainPredicateOnlyFields(string source, string column, string pattern)
    {
        var root = Path.Combine(_root, "search-text");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Alpha.txt"), "TODO Alpha\nTODO beta\nTODO\n");
        File.WriteAllText(Path.Combine(root, "Beta.txt"), "TODO gamma\n");
        var escaped = root.Replace('\\', '/');
        var argument = source == "many" ? "array {(Id: 'alpha', Pattern: 'TODO')}" : source == "bytes" ? "'544F444F'" : "'TODO'";
        var output = source == "audit" ? "Occurrences" : "Path";
        foreach (var op in new[] { "like", "not like", "rlike", "not rlike" })
        {
            var probe = new Probe();
            var needle = op.EndsWith("rlike") ? "a" : pattern;
            var query = $"select d.{output} from search.{source}('{escaped}', {argument}) d where d.{column} {op} '{needle}' order by d.{output} skip 0 take 2";
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)), query);
            Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicatePatternMatch), query);
            Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedTake is null));
        }
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(99)]
    public void UnknownCapabilityVersion_KeepsLikeResidual(int version)
    {
        var probe = new Probe { Version = version };
        var query = $"select d.Id from {Source("json")} d where d.Name like '%a%' order by d.Id";
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(probe)));
        Assert.IsTrue(probe.Plans.All(plan => plan.AcceptedPredicate is null));
    }

    [TestMethod]
    public void DeclaredCsvNumericLookingStrings_KeepOriginalDecodedText()
    {
        File.WriteAllText(Path.Combine(_root, "rows.csv"), "Id,Name\n1,001\n2,002\n3,010\n");
        var path = Path.Combine(_root, "rows.csv").Replace('\\', '/');
        var probe = new Probe();
        var query = "table Shape { Id: int, Name: string }; couple separatedvalues.comma with table Shape as Rows;" +
            $"select d.Name from Rows('{path}', true, 0) d where d.Name like '00%' order by d.Id";
        CollectionAssert.AreEqual(new[] { "001", "002" }, Run(query, new Provider(probe)));
        CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(new Probe())));
        Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateStringMatch));
    }

    [DataTestMethod]
    [DataRow("json")]
    [DataRow("csv")]
    public void ResolvedStrings_QuotedColumnNamesSupportPatterns(string provider)
    {
        File.WriteAllText(Path.Combine(_root, "rows.csv"), "Id,Display.Label,Label\n1,Alpha,Beta\n2,Beta,Alpha\n");
        File.WriteAllText(Path.Combine(_root, "rows.json"), "[{\"Id\":1,\"Display.Label\":\"Alpha\",\"Label\":\"Beta\"},{\"Id\":2,\"Display.Label\":\"Beta\",\"Label\":\"Alpha\"}]");
        foreach (var op in new[] { "like", "not like", "rlike", "not rlike" })
        {
            var probe = new Probe();
            var pattern = op.EndsWith("rlike") ? "^Al" : "Al%";
            var query = $"select d.Id from {Source(provider)} d where d.[Display.Label] {op} '{pattern}' order by d.Id";
            CollectionAssert.AreEqual(new[] { op.StartsWith("not") ? "2" : "1" }, Run(query, new Provider(probe)));
            CollectionAssert.AreEqual(Run(query, new Provider(new Probe { Enabled = false })), Run(query, new Provider(new Probe())));
            Assert.IsTrue(probe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicatePatternMatch));
        }
    }

    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(0, true)]
    [DataRow(17, false)]
    [DataRow(17, true)]
    [DataRow(73, false)]
    [DataRow(73, true)]
    public void RegexBudgets_ReachAcceptedAndRejectedPredicates(int milliseconds, bool negated)
    {
        var budget = milliseconds == 0 ? (TimeSpan?)null : TimeSpan.FromMilliseconds(milliseconds);
        var expectedBudget = budget ?? TimeSpan.FromSeconds(2);
        var query = $"select d.Id from {Source("json")} d where d.Name {(negated ? "not " : "")}rlike 'a' order by d.Id";
        var expected = Names.Select((value, id) => (value, id))
            .Where(row => Evaluator.Operators.RLikeDynamic(row.value, "a") != negated).Select(row => row.id.ToString()).ToArray();
        foreach (var mode in new[] { "enabled", "disabled", "rejected" })
        {
            var probe = CreateProbe(mode);
            using var compiled = CompileWithBudget(query, new Provider(probe), budget);
            CollectionAssert.AreEqual(expected, Read(compiled), mode);
            var negotiated = probe.Requests.Select(request => request.Predicate).OfType<SourcePredicateRegexMatch>().ToArray();
            if (mode == "disabled") Assert.AreEqual(0, negotiated.Length);
            else
            {
                Assert.IsTrue(negotiated.Length > 0, mode);
                Assert.IsTrue(negotiated.All(predicate => predicate.MatchTimeout == expectedBudget && predicate.IsNegated == negated), mode);
            }
            Assert.AreEqual(mode == "enabled" ? 1 : 0, probe.Plans.Sum(plan => plan.ExecutionPlan.PredicateApplications.Count), mode);
        }
    }

    [TestMethod]
    public void RegexBudgets_StayIsolatedAcrossRepeatedAndOverlappingExecutions()
    {
        var firstProbe = new Probe();
        var secondProbe = new Probe();
        var query = $"select d.Id from {Source("json")} d where d.Name rlike 'a' order by d.Id";
        using var first = CompileWithBudget(query, new Provider(firstProbe), TimeSpan.FromMilliseconds(17));
        using var second = CompileWithBudget(query, new Provider(secondProbe), TimeSpan.FromMilliseconds(73));
        var expected = Run(query, new Provider(new Probe { Enabled = false }));
        CollectionAssert.AreEqual(expected, Read(first));
        CollectionAssert.AreEqual(expected, Read(second));
        using var barrier = new Barrier(2);
        Task.WhenAll(new[] { first, second }.Select(compiled => Task.Run(() =>
        {
            Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(30)));
            for (var repeat = 0; repeat < 3; repeat++) CollectionAssert.AreEqual(expected, Read(compiled));
        }))).GetAwaiter().GetResult();
        Assert.IsTrue(firstProbe.Requests.Select(request => request.Predicate).OfType<SourcePredicateRegexMatch>().All(predicate => predicate.MatchTimeout == TimeSpan.FromMilliseconds(17)));
        Assert.IsTrue(secondProbe.Requests.Select(request => request.Predicate).OfType<SourcePredicateRegexMatch>().All(predicate => predicate.MatchTimeout == TimeSpan.FromMilliseconds(73)));
        Assert.IsTrue(firstProbe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateRegexMatch));
        Assert.IsTrue(secondProbe.Plans.Any(plan => plan.AcceptedPredicate is SourcePredicateRegexMatch));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void JsonSelfCorrelation_RepeatedAndOverlappingBindingsReturnCorrectRows(bool equality)
    {
        var source = Source("json");
        var query = equality
            ? $"select d.Id from {source} d where exists (select e.Id from {source} e where e.Id = d.Id and e.Id > 5) order by d.Id"
            : $"select d.Id from {source} d where d.Id > 5 and exists (select e.Id from {source} e where e.Id > d.Id and e.Id > 5) order by d.Id";
        var expected = equality ? new[] { "6", "7", "8" } : new[] { "6", "7" };
        foreach (var mode in new[] { "enabled", "disabled", "rejected" })
        {
            using var first = CompileWithBudget(query, new Provider(CreateProbe(mode)), null);
            using var second = CompileWithBudget(query, new Provider(CreateProbe(mode)), null);
            for (var repeat = 0; repeat < 2; repeat++) CollectionAssert.AreEqual(expected, Read(first), mode);
            using var barrier = new Barrier(2);
            Task.WhenAll(new[] { first, second }.Select(compiled => Task.Run(() =>
            {
                Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(30)));
                for (var repeat = 0; repeat < 2; repeat++) CollectionAssert.AreEqual(expected, Read(compiled), mode);
            }))).GetAwaiter().GetResult();
        }
    }

    [TestMethod]
    public void OrdinaryProviderReadFailure_RetainsDatasourceDiagnostic()
    {
        using var compiled = CompileWithBudget($"select d.Id from {Source("json")} d", new Provider(new Probe { FailReads = true }), null);
        var failure = Assert.ThrowsException<Evaluator.Exceptions.QueryExecutionException>(() => Read(compiled));
        Assert.AreEqual(Parser.Diagnostics.DiagnosticCode.MQ7011_DataSourceReadFailed, failure.Envelope!.Code);
        Exception? cause = failure;
        while (cause is not null && cause is not IOException) cause = cause.InnerException;
        Assert.IsNotNull(cause);
        Assert.AreEqual("Controlled provider read failure.", cause.Message);
    }

    private static Probe CreateProbe(string mode) => new() { Enabled = mode != "disabled", RejectPredicates = mode == "rejected" };

    private static Evaluator.CompiledQuery CompileWithBudget(string query, ISchemaProvider provider, TimeSpan? budget)
    {
        var options = new Evaluator.CompilationOptions(Evaluator.ParallelizationMode.Full, usePrimitiveTypeValidation: true);
        if (budget.HasValue) options = options.WithRegexMatchTimeout(budget.Value);
        return Converter.InstanceCreator.CompileForExecution(query, "Alpha14Fixes_" + Guid.NewGuid().ToString("N"), provider, new SilentLoggerResolver(), options);
    }

    private static string[] Read(Evaluator.CompiledQuery compiled)
    {
        using var table = compiled.Run();
        return table.Select(row => string.Join("|", row.ReadValues().Select(value => value is null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture)))).ToArray();
    }

    private sealed class FailingRows<T> : Schema.DataSources.RowSource<T>
    {
        public override IEnumerable<IReadOnlyList<T>> Chunks
        {
            get { yield return Fail(); }
        }
        private static IReadOnlyList<T> Fail() => throw new IOException("Controlled provider read failure.");
    }

    private string Source(string provider)
    {
        var path = _root.Replace('\\', '/').Replace("'", "''");
        return provider switch
        {
            "flat" => $"flat.file('{path}/rows.txt')",
            "json" => $"json.file('{path}/rows.json')",
            "csv" => $"separatedvalues.comma('{path}/rows.csv', true, 0)",
            "os" => $"os.files('{path}', false)",
            "search" => $"search.paths('{path}')",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private static string[] Run(string query, ISchemaProvider provider)
    {
        using var compiled = InstanceCreatorHelpers.CompileForExecution(query, "Alpha14_" + Guid.NewGuid().ToString("N"),
            provider, EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        using var table = compiled.Run();
        return table.Select(row => string.Join("|", row.ReadValues().Select(value =>
            value is null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture)))).ToArray();
    }

    private static SourcePlanRequest BaselineRequest(SourcePlanRequest request, bool enabled) => enabled ? request : request with { Predicate = null, Skip = null, Take = null };
    private static SourcePlanResult DisablePredicate(SourcePlanResult result, SourcePlanRequest request, bool enabled) => enabled ? result : result with
    {
        ResidualPredicate = request.Predicate, ResidualSkip = request.Skip, ResidualTake = request.Take
    };

    private sealed class SilentLoggerResolver : Converter.ILoggerResolver
    {
        public Microsoft.Extensions.Logging.ILogger ResolveLogger() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public Microsoft.Extensions.Logging.ILogger<T> ResolveLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
    }

    private sealed class Probe
    {
        internal bool Enabled { get; init; } = true;
        internal bool RejectPredicates { get; init; }
        internal bool FailReads { get; init; }
        internal bool AcceptsPredicates => Enabled && !RejectPredicates;
        internal List<SourcePlanRequest> Requests { get; } = [];
        internal int Version { get; init; } = 2;
        internal List<SourcePlanResult> Plans { get; } = [];
        internal int Opens;
        internal SourceDescriptor Describe(SourceDescriptor descriptor) => descriptor with
        {
            PredicateCapabilities = Enabled ? descriptor.PredicateCapabilities with { ContractVersion = Version } : SourcePredicateCapabilities.None
        };
        internal SourcePlanResult Plan(SourcePlanRequest request, Func<SourcePlanResult> plan)
        {
            Requests.Add(request);
            var result = plan();
            Plans.Add(result);
            return result;
        }
    }

    private sealed class Provider(Probe probe) : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => schema.ToLowerInvariant() switch
        {
            "#json" => new JsonProbe(probe), "#separatedvalues" => new CsvProbe(probe),
            "#flat" => new FlatProbe(probe), "#os" => new OsProbe(probe), "#search" => new SearchProbe(probe), "#system" => new SystemSchema(),
            _ => throw new NotSupportedException(schema)
        };
    }
    private sealed class JsonProbe(Probe probe) : JsonSchema, IQueryScopedRowSourceSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
            => probe.Plan(request, () => DisablePredicate(base.TryPlanSource(name, BaselineRequest(request, probe.AcceptsPredicates), parameters), request, probe.AcceptsPredicates));
        Schema.DataSources.RowSource<T> IQueryScopedRowSourceSchema.GetQueryScopedRowSource<T, TMaterializer, TArguments>(string name, QueryScopedRowSourceRequest request, TArguments arguments)
        {
            probe.Opens++;
            return probe.FailReads ? new FailingRows<T>() : GetQueryScopedRowSource<T, TMaterializer, TArguments>(name, request, arguments);
        }

    }
    private sealed class CsvProbe(Probe probe) : SeparatedValuesSchema, IQueryScopedRowSourceSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
            => probe.Plan(request, () => DisablePredicate(base.TryPlanSource(name, BaselineRequest(request, probe.AcceptsPredicates), parameters), request, probe.AcceptsPredicates));
        Schema.DataSources.RowSource<T> IQueryScopedRowSourceSchema.GetQueryScopedRowSource<T, TMaterializer, TArguments>(string name, QueryScopedRowSourceRequest request, TArguments arguments)
        {
            probe.Opens++;
            return GetQueryScopedRowSource<T, TMaterializer, TArguments>(name, request, arguments);
        }

    }
    private sealed class FlatProbe(Probe probe) : FlatFileSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
            => probe.Plan(request, () => DisablePredicate(base.TryPlanSource(name, BaselineRequest(request, probe.AcceptsPredicates), parameters), request, probe.AcceptsPredicates));
    }
    private sealed class OsProbe(Probe probe) : OsSchema
    {
        public override SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object[] parameters)
            => probe.Describe(base.DescribeSource(name, context, parameters));
        public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
            => probe.Plan(request, () => DisablePredicate(base.TryPlanSource(name, BaselineRequest(request, probe.AcceptsPredicates), parameters), request, probe.AcceptsPredicates));
    }
    private sealed class SearchProbe(Probe probe) : ISchema, Schema.DataSources.ITypedSourceSchema
    {
        private readonly ISchema _inner = new SearchSchema();
        public bool TryGetTypedSourceRegistration(string name, out Schema.DataSources.TypedSourceRegistration registration) => ((Schema.DataSources.ITypedSourceSchema)_inner).TryGetTypedSourceRegistration(name, out registration!);
        public string Name => _inner.Name;
        public ISchemaTable GetTableByName(string name, SourceMetadataContext context, params object?[] parameters) => _inner.GetTableByName(name, context, parameters);
        public SourceDescriptor DescribeSource(string name, SourceDescribeContext context, params object?[] parameters) => probe.Describe(_inner.DescribeSource(name, context, parameters));
        public IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(string name, SourceRuntimeSettingsDescribeContext context, params object?[] parameters) => _inner.DescribeSourceRuntimeSettings(name, context, parameters);
        public SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters) => probe.Plan(request, () => DisablePredicate(_inner.TryPlanSource(name, BaselineRequest(request, probe.AcceptsPredicates), parameters), request, probe.AcceptsPredicates));
        public Schema.DataSources.RowSource<T> GetRowSource<T, TArguments>(string name, SourceExecutionContext context, TArguments arguments) where TArguments : struct, ISourceArguments => _inner.GetRowSource<T, TArguments>(name, context, arguments);
        public Schema.Reflection.SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext context) => _inner.GetRawConstructors(context);
        public Schema.Reflection.SchemaMethodInfo[] GetRawConstructors(string name, SourceMetadataContext context) => _inner.GetRawConstructors(name, context);
        public bool TryResolveMethod(string name, Type[] parameters, Type? entity, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveMethod(name, parameters, entity, out method);
        public bool TryResolveRawMethod(string name, Type[] parameters, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveRawMethod(name, parameters, out method);
        public bool TryResolveAggregationMethod(string name, Type[] parameters, Type? entity, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveAggregationMethod(name, parameters, entity, out method);
        public bool TryResolveAggregationMethod(string name, Type[] parameters, Type? entity, Func<global::System.Reflection.MethodInfo, bool> filter, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveAggregationMethod(name, parameters, entity, filter, out method);
        public bool TryResolveWindowFunction(string name, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveWindowFunction(name, out method);
        public bool TryResolveWindowFunction(string name, Type[] arguments, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::System.Reflection.MethodInfo? method) => _inner.TryResolveWindowFunction(name, arguments, out method);
        public IReadOnlyDictionary<string, IReadOnlyList<global::System.Reflection.MethodInfo>> GetAllLibraryMethods() => _inner.GetAllLibraryMethods();
    }
}
