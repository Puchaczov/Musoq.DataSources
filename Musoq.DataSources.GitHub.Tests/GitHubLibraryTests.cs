using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class GitHubLibraryTests
{
    private readonly GitHubLibrary _library = new();

    static GitHubLibraryTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void ParseRepoFullName_WithValidFullName_ShouldReturnOwnerAndRepo()
    {
        var result = _library.ParseRepoFullName("owner/repo");

        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repo);
    }

    [TestMethod]
    public void ParseRepoFullName_WithEmptyString_ShouldReturnEmptyTuple()
    {
        var result = _library.ParseRepoFullName("");

        Assert.AreEqual(string.Empty, result.Owner);
        Assert.AreEqual(string.Empty, result.Repo);
    }

    [TestMethod]
    public void ParseRepoFullName_WithInvalidFormat_ShouldReturnEmptyTuple()
    {
        var result = _library.ParseRepoFullName("invalid");

        Assert.AreEqual(string.Empty, result.Owner);
        Assert.AreEqual(string.Empty, result.Repo);
    }

    [TestMethod]
    public void HasLabel_WithMatchingLabel_ShouldReturnTrue()
    {
        var result = _library.HasLabel("bug, feature, enhancement", "feature");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void HasLabel_WithNonMatchingLabel_ShouldReturnFalse()
    {
        var result = _library.HasLabel("bug, feature", "enhancement");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HasLabel_WithNullLabels_ShouldReturnFalse()
    {
        var result = _library.HasLabel(null, "feature");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HasLabel_CaseInsensitive_ShouldReturnTrue()
    {
        var result = _library.HasLabel("Bug, Feature", "bug");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void LabelCount_WithMultipleLabels_ShouldReturnCount()
    {
        var result = _library.LabelCount("bug, feature, enhancement");

        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void LabelCount_WithEmptyLabels_ShouldReturnZero()
    {
        var result = _library.LabelCount("");

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void LabelCount_WithNull_ShouldReturnZero()
    {
        var result = _library.LabelCount(null);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void ShortSha_WithLongSha_ShouldReturnShortened()
    {
        var result = _library.ShortSha("abc123def456ghi789");

        Assert.AreEqual("abc123d", result);
    }

    [TestMethod]
    public void ShortSha_WithCustomLength_ShouldReturnRequestedLength()
    {
        var result = _library.ShortSha("abc123def456ghi789", 10);

        Assert.AreEqual("abc123def4", result);
    }

    [TestMethod]
    public void ShortSha_WithShortSha_ShouldReturnOriginal()
    {
        var result = _library.ShortSha("abc");

        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void ShortSha_WithNull_ShouldReturnEmpty()
    {
        var result = _library.ShortSha(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void IsStale_WithOldDate_ShouldReturnTrue()
    {
        var oldDate = DateTimeOffset.UtcNow.AddDays(-60);

        var result = _library.IsStale(oldDate);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsStale_WithRecentDate_ShouldReturnFalse()
    {
        var recentDate = DateTimeOffset.UtcNow.AddDays(-10);

        var result = _library.IsStale(recentDate);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsStale_WithNull_ShouldReturnFalse()
    {
        var result = _library.IsStale(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void AgeDays_ShouldReturnPositiveValue()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-10);

        var result = _library.AgeDays(createdAt);

        Assert.IsTrue(result >= 9.9 && result <= 10.1);
    }

    [TestMethod]
    public void DaysBetween_WithTwoDates_ShouldReturnDifference()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var end = DateTimeOffset.UtcNow;

        var result = _library.DaysBetween(start, end);

        Assert.IsTrue(result >= 9.9 && result <= 10.1);
    }
}