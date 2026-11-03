using System.Collections;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Common;
using Musoq.Parser.Diagnostics;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class ProviderDiagnosticsTests
{
    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest, DataSourceFailureKind.InvalidInput)]
    [DataRow(HttpStatusCode.Unauthorized, DataSourceFailureKind.AccessDenied)]
    [DataRow(HttpStatusCode.Forbidden, DataSourceFailureKind.AccessDenied)]
    [DataRow(HttpStatusCode.NotFound, DataSourceFailureKind.ResourceNotFound)]
    [DataRow(HttpStatusCode.RequestTimeout, DataSourceFailureKind.Timeout)]
    [DataRow(HttpStatusCode.TooManyRequests, DataSourceFailureKind.RateLimited)]
    [DataRow(HttpStatusCode.ServiceUnavailable, DataSourceFailureKind.Unavailable)]
    public void RecognizedHttpFailure_ProducesSafeProviderEnvelope(HttpStatusCode status, DataSourceFailureKind kind)
    {
        var diagnostics = new ProviderDiagnostics("TEST");
        var privateError = new HttpRequestException("PRIVATE-SECRET /private/path", null, status);
        var failure = Assert.ThrowsException<DataSourceException>(() => diagnostics.Run<int>(() => throw privateError));
        var envelope = MusoqErrorEnvelope.FromDiagnostic(failure.ToDiagnostic());
        Assert.AreEqual("TEST." + kind, envelope.Arguments["providerCode"]);
        Assert.AreEqual(kind.ToString(), envelope.Arguments["failureKind"]);
        Assert.AreSame(privateError, failure.InnerException);
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(envelope).Contains("PRIVATE-SECRET", StringComparison.Ordinal));
        Assert.AreEqual(1, failure.Report.SuggestedFixes.Count);
    }

    [TestMethod]
    public void UnknownException_RemainsGenericInsteadOfBeingRelabeled()
    {
        var diagnostics = new ProviderDiagnostics("TEST", fileSystem: true);
        Assert.IsFalse(diagnostics.TryMap(new InvalidOperationException("defect"), CancellationToken.None, out _));
        Assert.IsFalse(diagnostics.TryMap(new HttpRequestException("unknown", null, (HttpStatusCode)418), CancellationToken.None, out _));
    }

    [TestMethod]
    public void CancellationAndExistingInputDiagnostic_ArePreserved()
    {
        var diagnostics = new ProviderDiagnostics("TEST", fileSystem: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.IsFalse(diagnostics.TryMap(new TaskCanceledException(), cancellation.Token, out _));
        Assert.IsFalse(diagnostics.TryMap(new DataSourceInputException(DataSourceInputFailureReason.MalformedJson), CancellationToken.None, out _));
        Assert.IsFalse(diagnostics.TryMap(new AggregateException(new IOException(), new InvalidOperationException()), CancellationToken.None, out _));
    }

    [TestMethod]
    public void DeferredChunks_AreForwardedWithoutCopyingOrMaterializingRows()
    {
        var chunk = new[] { 1, 2, 3 };
        var source = new TestSource([chunk]);
        var wrapped = new ProviderDiagnostics("TEST").Wrap(source, CancellationToken.None);
        Assert.AreSame(chunk, wrapped.Chunks.Single());
    }

    [TestMethod]
    [DataRow(FailureStage.GetEnumerator)]
    [DataRow(FailureStage.MoveNext)]
    [DataRow(FailureStage.Current)]
    [DataRow(FailureStage.Dispose)]
    public void DeferredFailure_IsClassifiedAtItsActualBoundary(FailureStage stage)
    {
        var wrapped = new ProviderDiagnostics("TEST", fileSystem: true).Wrap(new TestSource(new FailingChunks(stage)), CancellationToken.None);
        var failure = Assert.ThrowsException<DataSourceException>(() => wrapped.Chunks.ToArray());
        Assert.AreEqual("TEST.ResourceNotFound", failure.Report.Code);
        Assert.AreEqual(DataSourceFailureKind.ResourceNotFound, failure.Report.FailureKind);
    }

    [TestMethod]
    public void RecognizedFallback_ReportsWarningAndExplainsContinuation()
    {
        var warnings = new DataSourceWarningCollector();
        var identity = new SourceIdentity("#test", "rows", "ctx", "r");
        var context = new SourceExecutionContext("query", SourceExecutionPlan.Empty(identity), CancellationToken.None,
            [], new Dictionary<string, string>(), NullLogger.Instance) { WarningReporter = warnings.ForSource(identity) };
        var continued = new ProviderDiagnostics("TEST").Warn(context,
            new HttpRequestException("PRIVATE BODY", null, HttpStatusCode.ServiceUnavailable), "Cached metadata was used.");
        var warning = warnings.Snapshot().Single();
        Assert.IsTrue(continued);
        Assert.AreEqual(DiagnosticSeverity.Warning, warning.Severity);
        Assert.AreEqual("TEST.Unavailable.Fallback", warning.Arguments["providerCode"]);
        StringAssert.Contains(warning.Message, "Cached metadata was used.");
        Assert.IsFalse(warning.Message.Contains("PRIVATE BODY", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PatternExecution_RetainsTheExecutionWarningChannel(bool nativeOwnsWindow)
    {
        var identity = new SourceIdentity("#test", "rows", "ctx", "r");
        var pattern = new SourcePredicateRegexMatch(new("Value"), "^1", TimeSpan.FromSeconds(1));
        var plan = PatternSourcePlan.Plan(new SourcePlanRequest { Identity = identity, Predicate = pattern, Take = 1 },
            new PatternColumnBindings<int>(("Value", value => value.ToString())), SourcePlanResult.RejectAll);
        var warnings = new DataSourceWarningCollector();
        var context = new SourceExecutionContext("query", plan.ExecutionPlan, CancellationToken.None,
            [], new Dictionary<string, string>(), NullLogger.Instance) { WarningReporter = warnings.ForSource(identity) };
        var source = PatternSourcePlan.Read(context, inner =>
        {
            Assert.AreSame(context.WarningReporter, inner.WarningReporter);
            inner.ReportWarning(new DataSourceDiagnostic("TEST.WARNING", "fallback", DataSourceDiagnosticSeverity.Warning, "Fallback was used."));
            return new TestSource([new[] { 1 }]);
        }, nativeOwnsWindow ? ["Value"] : [], nativeOwnsWindow);
        Assert.AreEqual(1, source.Chunks.Sum(chunk => chunk.Count));
        Assert.AreEqual("TEST.WARNING", warnings.Snapshot().Single().Arguments["providerCode"]);
    }

    public enum FailureStage { GetEnumerator, MoveNext, Current, Dispose }

    private sealed class TestSource(IEnumerable<IReadOnlyList<int>> chunks) : RowSource<int>
    {
        public override IEnumerable<IReadOnlyList<int>> Chunks => chunks;
    }

    private sealed class FailingChunks(FailureStage stage) : IEnumerable<IReadOnlyList<int>>
    {
        public IEnumerator<IReadOnlyList<int>> GetEnumerator()
            => stage == FailureStage.GetEnumerator ? throw MissingFile() : new FailingEnumerator(stage);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FailingEnumerator(FailureStage stage) : IEnumerator<IReadOnlyList<int>>
    {
        private bool _first = true;
        public IReadOnlyList<int> Current => stage == FailureStage.Current ? throw MissingFile() : new[] { 1 };
        object IEnumerator.Current => Current;
        public bool MoveNext()
        {
            if (stage == FailureStage.MoveNext) throw MissingFile();
            var first = _first;
            _first = false;
            return first;
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { if (stage == FailureStage.Dispose) throw MissingFile(); }
    }

    private static FileNotFoundException MissingFile() => new("PRIVATE PATH");
}
