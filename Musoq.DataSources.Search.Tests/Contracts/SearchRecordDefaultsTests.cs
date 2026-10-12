#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchRecordDefaultsTests
{
    [TestMethod]
    public void ParameterlessRecords_ShouldUsePhysicalLineDefaults()
    {
        var records = new SearchRecordsInput();

        Assert.AreEqual("physical-line", records.Mode);
        Assert.AreEqual(1_048_576L, records.MaxBytes);
        Assert.IsNull(records.StartDelimiter);
        Assert.IsNull(records.EndDelimiter);
        Assert.IsFalse(records.AllowUnterminatedEof);
    }

    [TestMethod]
    public void OmittedRecords_ShouldCreatePhysicalLineRegexRequest()
    {
        var request = CreateRegexRequest(records: null);

        Assert.AreEqual(SearchPatternMode.Regex, request.PatternMode);
        Assert.AreEqual(SearchRecordMode.PhysicalLine, request.RecordMode);
        Assert.AreEqual(1_048_576, request.MaxRecordBytes);
        Assert.IsNull(request.RecordFraming);
    }

    [TestMethod]
    public void ExplicitInvalidRecords_ShouldRemainRejected()
    {
        var invalidMode = Assert.ThrowsException<SearchRequestException>(() =>
            CreateRegexRequest(new SearchRecordsInput(mode: "unknown")));
        Assert.AreEqual("records.mode", invalidMode.Diagnostic.Location?.ArgumentName);

        var invalidSize = Assert.ThrowsException<SearchResourceLimitException>(() =>
            CreateRegexRequest(new SearchRecordsInput(maxBytes: 0)));
        Assert.AreEqual("records.maxBytes", invalidSize.Diagnostic.Location?.ArgumentName);

        var missingDelimiters = Assert.ThrowsException<SearchRequestException>(() =>
            CreateRegexRequest(new SearchRecordsInput(mode: "bounded-multiline")));
        Assert.AreEqual("records", missingDelimiters.Diagnostic.Location?.ArgumentName);
    }

    private static SearchRequest CreateRegexRequest(SearchRecordsInput? records)
    {
        return SearchTypedInputNormalizer.CreateSingle(
            "fixture",
            "TODO|FIXME",
            new SearchTextInput(mode: "regex"),
            records,
            context: null,
            scope: null,
            limits: null);
    }
}
