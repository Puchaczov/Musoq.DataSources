using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchWarningTests
{
    [TestMethod]
    public void TolerantDecoding_ReportsFirstLossOnceAndContinuesReading()
    {
        var reports = new List<DataSourceDiagnostic>();
        byte[] bytes = [0x41, 0xff, 0x42, 0xff];
        using var reader = SearchTextReader.Open(bytes, bytes.Length, SearchEncodingMode.Auto, reports.Add);
        Assert.AreEqual("A\ufffdB\ufffd", reader.ReadToEnd());
        var report = reports.Single();
        Assert.AreEqual("SEARCH.DecodingReplacement", report.Code);
        Assert.AreEqual(DataSourceDiagnosticSeverity.Warning, report.Severity);
        Assert.AreEqual(1L, report.InputLocation!.ByteOffset);
    }

    [TestMethod]
    public void TypedExecutionAdapter_RetainsOriginalSearchDiagnosticAndCause()
    {
        var root = Path.Combine(Path.GetTempPath(), "missing-search-" + Guid.NewGuid().ToString("N"));
        var source = new SearchMatchesTypedSource(root, "text", RuntimeV2TestContexts.CreateExecutionContext());
        var error = Assert.ThrowsException<DataSourceException>(() => source.Chunks.SelectMany(chunk => chunk).ToArray());
        Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, error.Report.Code);
        Assert.IsInstanceOfType<SearchSourceAccessException>(error.InnerException);
        Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, ((SearchSourceAccessException)error.InnerException!).Diagnostic.Code);
    }

    [TestMethod]
    public void FailedAudit_RetainsSummaryAndReportsSafeWarning()
    {
        var root = Path.Combine(Path.GetTempPath(), "PRIVATE-MISSING-" + Guid.NewGuid().ToString("N"));
        var warnings = new DataSourceWarningCollector();
        var baseline = RuntimeV2TestContexts.CreateExecutionContext();
        var context = new SourceExecutionContext(baseline.QueryId, baseline.Plan, CancellationToken.None,
            baseline.AllColumns, baseline.SourceRuntimeSettings, baseline.Logger)
        { WarningReporter = warnings.ForSource(baseline.Plan.Identity) };
        var source = new SearchAuditSource(SearchRequest.Create(root, "text"), context);
        var summary = source.Chunks.SelectMany(chunk => chunk).Single();
        Assert.AreEqual(SearchOutcome.Failed, summary.Outcome);
        var warning = warnings.Snapshot().Single();
        Assert.AreEqual("SEARCH-SOURCE-001.Fallback", warning.Arguments["providerCode"]);
        Assert.IsFalse((warning.Message + string.Join(" ", warning.Arguments.Values)).Contains(root, StringComparison.Ordinal));
    }
}
