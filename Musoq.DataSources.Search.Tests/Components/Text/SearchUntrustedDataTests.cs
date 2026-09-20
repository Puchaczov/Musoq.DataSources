#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Many;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchUntrustedDataTests
{
    [TestMethod]
    public void SearchRows_ShouldPreserveFilenamesWithLeadingDashesAndNewlines()
    {
        var root = CreateTemporaryRoot();
        var relativePath = OperatingSystem.IsWindows()
            ? "-leading-dash.txt"
            : "-leading-\nfile.txt";

        try
        {
            File.WriteAllText(Path.Combine(root, relativePath), "TODO");

            var source = new SearchMatchesSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext());
            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(relativePath, rows[0].Path);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void DiagnosticLocation_ShouldPreserveMachinePathAndExposeSafePresentation()
    {
        const string path = "C:\\quoted\"\\-leading-\nfile\u001B.txt";
        var diagnostic = SearchDiagnosticCatalog.SourceOpenFailed(path);
        var location = diagnostic.Location ?? throw new AssertFailedException(
            "The source diagnostic did not include a location.");

        Assert.AreEqual(path, location.Path);
        Assert.IsFalse(location.DisplayPath!.Any(char.IsControl));
        StringAssert.Contains(location.DisplayPath, "\\u000A");
        StringAssert.Contains(location.DisplayPath, "\\u001B");
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "musoq-search-untrusted-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
