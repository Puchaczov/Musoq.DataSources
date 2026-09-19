#nullable enable

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.ContractTests.Infrastructure;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchTypedSurfaceContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ReflectedSurface_ShouldContainTypedConstructorsOnly()
    {
        var assembly = typeof(SearchSchema).Assembly;
        Assert.IsNull(
            assembly.GetType(
                "Musoq.DataSources.Search.Components.Many.SearchManyRequestParser",
                throwOnError: false));

        var manyConstructors = typeof(SearchManyTypedSource).GetConstructors();
        Assert.IsTrue(manyConstructors.Length >= 2);
        Assert.IsTrue(
            manyConstructors.All(constructor =>
                constructor.GetParameters()[1].ParameterType != typeof(string)));

        var byteParser = assembly.GetType(
            "Musoq.DataSources.Search.Components.Bytes.SearchBytePatternParser",
            throwOnError: false);
        Assert.IsNotNull(byteParser);
        Assert.IsNull(byteParser!.GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RawMetadata_ShouldExposeTypedArgumentNamesAndTypes()
    {
        var metadata = new SourceMetadataContext(
            "typed-surface",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
        var constructors = new SearchSchema().GetRawConstructors(metadata);

        Assert.IsFalse(constructors
            .SelectMany(constructor => constructor.ConstructorInfo.Arguments)
            .Any(argument => argument.Name is "requestJson" or "patternJson"));

        var many = constructors.Where(constructor => constructor.MethodName == "many").ToArray();
        Assert.AreEqual(2, many.Length);
        Assert.IsTrue(many.All(constructor =>
            constructor.ConstructorInfo.Arguments.Any(argument =>
                argument.Name == "patterns" &&
                argument.Type == typeof(IReadOnlyList<SearchPatternInput>))));

        var bytes = constructors.Where(constructor => constructor.MethodName == "bytes").ToArray();
        Assert.AreEqual(2, bytes.Length);
        Assert.IsTrue(bytes.All(constructor =>
            constructor.ConstructorInfo.Arguments.Any(argument =>
                argument.Name == "patternHex" && argument.Type == typeof(string))));
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RuntimeMetadata_ShouldExposeOnlySupportedExecutionSettings()
    {
        var metadata = new SourceMetadataContext(
            "runtime-settings",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
        var identity = new SourceIdentity("search", "matches", "runtime-settings", "matches");
        var context = new SourceRuntimeSettingsDescribeContext(identity, metadata);

        var requirements = new SearchSchema()
            .DescribeSourceRuntimeSettings("matches", context)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "search.max_parallelism", "search.buffered_output_bytes" },
            requirements.Select(setting => setting.Name).ToArray());

        Assert.IsTrue(requirements.All(static setting =>
            !setting.Required &&
            !setting.Secret &&
            setting.Phases == SourceRuntimeSettingPhase.Execution &&
            !string.IsNullOrWhiteSpace(setting.Description)));

        var parallelism = requirements.Single(static setting => setting.Name == "search.max_parallelism");
        StringAssert.Contains(parallelism.Description, "Zero or omission");
        StringAssert.Contains(parallelism.Description, "1 through 32");

        var bufferedOutput = requirements.Single(static setting => setting.Name == "search.buffered_output_bytes");
        StringAssert.Contains(bufferedOutput.Description, "33554432");
        StringAssert.Contains(bufferedOutput.Description, "65536 through 536870912");
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void DirectTypedAdapters_ShouldCoverEverySearchSource()
    {
        WithCorpus(ContractCorpusProfile.All, fixture =>
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var textRoot = fixture.PathFor("top.txt");
            var matches = SearchSqlHarness.MaterializeTyped(
                new SearchMatchesTypedSource(textRoot, "TODO", context).Chunks);
            var lines = SearchSqlHarness.MaterializeTyped(
                new SearchLinesTypedSource(textRoot, "TODO", context).Chunks);
            var files = SearchSqlHarness.MaterializeTyped(
                new SearchFilesTypedSource(textRoot, "TODO", context).Chunks);
            var counts = SearchSqlHarness.MaterializeTyped(
                new SearchCountsTypedSource(textRoot, "TODO", context).Chunks);
            var audit = SearchSqlHarness.MaterializeTyped(
                new SearchAuditTypedSource(textRoot, "TODO", context).Chunks);
            var paths = SearchSqlHarness.MaterializeTyped(
                new SearchPathsTypedSource(textRoot, context).Chunks);
            var many = SearchSqlHarness.MaterializeTyped(
                new SearchManyTypedSource(
                    textRoot,
                    [new SearchPatternInput("todo", "TODO")],
                    context).Chunks);
            var bytes = SearchSqlHarness.MaterializeTyped(
                new SearchBytesTypedSource(
                    fixture.PathFor("bytes/payload.bin"),
                    "54 4f 44 4f",
                    context).Chunks);

            Assert.IsTrue(matches.Count > 0);
            Assert.IsTrue(lines.Count > 0);
            Assert.IsTrue(files.Count > 0);
            Assert.AreEqual(1, counts.Count);
            Assert.AreEqual(1, audit.Count);
            Assert.AreEqual(1, paths.Count);
            Assert.IsTrue(many.Count > 0);
            Assert.IsTrue(bytes.Count > 0);
            Assert.IsTrue(audit[0].ScanId.Length > 0);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void NamedArguments_ShouldBindToTheTypedConstructorNames()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex from search.matches(" +
                        $"root: '{root}', pattern: 'TODO') m order by m.Path, m.MatchIndex");

            Assert.AreEqual(7, result.Count);
            Assert.IsTrue(result.Rows.All(row => row[0] is string && row[1] is long));
        });
    }
}
