#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Measurements;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchBytePipelineBenchmarkTests
{
    [TestMethod]
    public void BytePipelineVerification_ShouldMatchIndependentOracle()
    {
        var verification = SearchBytePipelineMeasurement.Verify();

        Assert.IsTrue(verification.Passed);
        Assert.AreEqual(2, verification.FileCount);
        Assert.IsTrue(verification.EligibleBytes >= 4L * 1024 * 1024);
        Assert.IsTrue(verification.ExactCandidateCount > 0);
        Assert.IsTrue(
            verification.MaskedCandidateCount > verification.ExactCandidateCount,
            "The masked fixture must exercise dense false positives.");
        Assert.IsTrue(verification.ParsedRecordCount > 0);
        Assert.AreEqual(
            verification.MaskedCandidateCount,
            verification.ParsedRecordCount + verification.ParseFailureCount);
        Assert.AreEqual(
            verification.MaskedCandidateCount,
            verification.ParseWindowValues);
        Assert.IsTrue(verification.CompleteWindows > 0);
        Assert.IsTrue(verification.IncompleteWindows > 0);
        Assert.IsTrue(verification.OptionalWindowOmitted);
    }
}
