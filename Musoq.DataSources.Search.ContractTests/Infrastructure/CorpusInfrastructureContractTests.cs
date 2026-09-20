#nullable enable

using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Infrastructure;

[TestClass]
public sealed class CorpusInfrastructureContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void SameSeed_ShouldProduceIdenticalRelativeFilesBytesAndHashes()
    {
        using var first = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.All);
        using var second = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.All);

        var firstSignature = Signature(first);
        var secondSignature = Signature(second);

        Assert.AreEqual(first.Manifest.ManifestHash, second.Manifest.ManifestHash);
        Assert.AreEqual(firstSignature, secondSignature);
        Assert.AreNotEqual(first.Root, second.Root);
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void DifferentSeed_ShouldChangeScaleContentWithoutChangingManifest()
    {
        using var first = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.Scale, 1);
        using var second = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.Scale, 2);

        Assert.AreEqual(first.Manifest.ManifestHash, second.Manifest.ManifestHash);
        Assert.AreNotEqual(Signature(first), Signature(second));
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Fixture_ShouldRejectUnsafeManifestPaths()
    {
        var manifest = ContractCorpusManifest.Load(ManifestPath);
        foreach (var file in manifest.Files)
        {
            Assert.IsFalse(Path.IsPathRooted(file.Path));
            Assert.IsFalse(file.Path.Contains("..", StringComparison.Ordinal));
            Assert.IsFalse(file.Path.Contains('\\'));
        }
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void FailureReport_ShouldContainRootSeedProfileAndManifestHash()
    {
        using var fixture = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.Minimal);
        var root = fixture.Root;

        fixture.WriteFailureReport(new InvalidOperationException("deterministic failure"));

        var report = File.ReadAllText(fixture.FailureReportPath);
        StringAssert.Contains(report, $"Root: {root}");
        StringAssert.Contains(report, $"Profile: {ContractCorpusProfile.Minimal}");
        StringAssert.Contains(report, $"Seed: {fixture.Seed}");
        StringAssert.Contains(report, $"ManifestHash: {fixture.Manifest.ManifestHash}");

        fixture.Dispose();
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ExtendedManifest_ShouldBeDeterministicAndCarryMetadataFacts()
    {
        using var first = ContractCorpusBuilder.Create(
            ExtendedManifestPath,
            ContractCorpusProfile.Extended);
        using var second = ContractCorpusBuilder.Create(
            ExtendedManifestPath,
            ContractCorpusProfile.Extended);

        Assert.IsTrue(first.Files.Count >= 20);
        Assert.AreEqual(Signature(first), Signature(second));
        Assert.IsTrue(first.Files.Any(file => file.Specification.MetadataTag == "old"));
        Assert.IsTrue(first.Files.Any(file => file.Specification.ModifiedUtcTicks is not null));
        Assert.IsTrue(first.Files.All(file => file.Specification.ExpectedLength is null ||
                                              file.Specification.ExpectedLength == file.Bytes.LongLength));
    }

    private static string Signature(ContractCorpusFixture fixture)
    {
        return string.Join(
            "\n",
            fixture.Files
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => string.Join(
                    "|",
                    file.RelativePath,
                    file.Bytes.Length,
                    file.Sha256)));
    }
}
