#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchCapabilityCardTests
{
    private static readonly string[] ExpectedMethods =
    [
        "matches",
        "many",
        "lines",
        "files",
        "counts",
        "paths",
        "bytes",
        "audit"
    ];

    [TestMethod]
    public void CapabilityCard_ShouldMatchStaticXmlAndRuntimeDescriptions()
    {
        var card = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "search", "search-capability-card-v1.md"));
        var xml = XDocument.Load(Path.ChangeExtension(typeof(SearchSchema).Assembly.Location, ".xml"));
        var schemaMember = xml.Root?
            .Element("members")?
            .Elements("member")
            .Single(member => member.Attribute("name")?.Value == "M:Musoq.DataSources.Search.SearchSchema.#ctor");

        Assert.IsNotNull(schemaMember, "Generated Search XML must contain the virtual-constructor snapshot.");

        var staticExamples = schemaMember!
            .Descendants("from")
            .Select(static element => element.Value.Trim())
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "search.matches(string root, string literal)",
                "search.many(string root, string request)",
                "search.bytes(string root, string patternJson)",
                "search.lines(string root, string literal)",
                "search.files(string root, string literal)",
                "search.counts(string root, string literal)",
                "search.paths(string root)",
                "search.audit(string root, string literal)"
            },
            staticExamples);

        foreach (var method in ExpectedMethods)
            StringAssert.Contains(card, $"`search.{method}(");

        StringAssert.Contains(card, "Cost/completeness boundary");
        StringAssert.Contains(card, "Optional when");
        StringAssert.Contains(card, "Complete`, `ScopeExhausted` and `CountsExact`");

        var schema = new SearchSchema();
        var metadataContext = CreateMetadataContext();
        var runtimeMethods = schema.GetRawConstructors(metadataContext)
            .Select(static constructor => constructor.MethodName)
            .ToArray();
        CollectionAssert.AreEqual(ExpectedMethods, runtimeMethods);

        foreach (var method in ExpectedMethods)
        {
            var descriptor = schema.DescribeSource(
                method,
                new SourceDescribeContext(
                    new SourceIdentity("search", method, "capability-card", method),
                    metadataContext),
                SourceArguments(method));

            Assert.IsTrue(descriptor.Columns.Count > 0, $"Runtime description for search.{method} has no columns.");
        }
    }

    private static object[] SourceArguments(string method)
    {
        const string root = "./fixture";
        return method switch
        {
            "paths" => [root],
            "many" => [root, "{\"version\":1,\"patterns\":[]}"],
            "bytes" => [root, "{\"version\":1,\"bytes\":\"54\"}"],
            _ => [root, "TODO"]
        };
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "capability-card",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the datasource repository root.");
    }
}
