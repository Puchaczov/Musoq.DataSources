#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchCaseAndWordBoundaryTests
{
    [TestMethod]
    public void CaseInsensitive_ShouldUseInvariantOrdinalRules()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "case.txt");
        const string content = "I i İ ı Σ σ ς ß SS ẞ TODO";
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.AreEqual(0, ReadMatches(path, "todo").Length);
            var invariantI = ReadMatches(path, "i", caseMode: "insensitive");
            CollectionAssert.AreEqual(
                new long[] { content.IndexOf('I'), content.IndexOf('i') },
                invariantI.Select(static match => match.Utf16Column).ToArray());

            var sigma = ReadMatches(path, "σ", caseMode: "insensitive");
            CollectionAssert.AreEqual(
                new long[]
                {
                    content.IndexOf('Σ'),
                    content.IndexOf('σ'),
                    content.IndexOf('ς')
                },
                sigma.Select(static match => match.Utf16Column).ToArray());

            var sharpS = ReadMatches(path, "ß", caseMode: "insensitive");
            CollectionAssert.AreEqual(
                new long[] { content.IndexOf('ß') },
                sharpS.Select(static match => match.Utf16Column).ToArray());

            var doubleS = ReadMatches(path, "ss", caseMode: "insensitive");
            CollectionAssert.AreEqual(
                new long[] { content.IndexOf("SS", StringComparison.Ordinal) },
                doubleS.Select(static match => match.Utf16Column).ToArray());

            var todo = ReadMatches(path, "todo", caseMode: "insensitive");
            Assert.AreEqual(1, todo.Length);
            Assert.AreEqual(content.IndexOf("TODO", StringComparison.Ordinal), todo[0].Utf16Column);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void WholeWord_ShouldTreatDigitsUnderscoresAndUnicodeLettersAsWordScalars()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "words.txt");
        const string content =
            "TODO TODO2 _TODO TODO_ TODO, (TODO) cafe\u0301 cafe żółć2 _żółć żółć_ żółć";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var todo = ReadMatches(path, "TODO", wholeWord: true);
            CollectionAssert.AreEqual(
                new long[]
                {
                    content.IndexOf("TODO", StringComparison.Ordinal),
                    content.IndexOf("TODO,", StringComparison.Ordinal),
                    content.IndexOf("(TODO)", StringComparison.Ordinal) + 1
                },
                todo.Select(static match => match.Utf16Column).ToArray());

            var combiningMark = ReadMatches(path, "cafe", wholeWord: true);
            Assert.AreEqual(1, combiningMark.Length);
            var decomposedStart = content.IndexOf("cafe\u0301", StringComparison.Ordinal);
            Assert.AreEqual(
                content.IndexOf("cafe", decomposedStart + "cafe\u0301".Length, StringComparison.Ordinal),
                combiningMark[0].Utf16Column);

            var decomposed = ReadMatches(path, "cafe\u0301", wholeWord: true);
            Assert.AreEqual(1, decomposed.Length);
            Assert.AreEqual(content.IndexOf("cafe\u0301", StringComparison.Ordinal), decomposed[0].Utf16Column);

            var polish = ReadMatches(path, "żółć", wholeWord: true);
            CollectionAssert.AreEqual(
                new long[]
                {
                    content.LastIndexOf("żółć", StringComparison.Ordinal)
                },
                polish.Select(static match => match.Utf16Column).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void WholeWord_ShouldClassifySupplementaryScalarsWithoutCultureDrift()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "scalars.txt");
        const string content = "😀foo foo😀 𐐀foo foo𐐀";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var matches = ReadMatches(path, "foo", wholeWord: true);
            var first = content.IndexOf("foo", StringComparison.Ordinal);
            var second = content.IndexOf("foo", first + 1, StringComparison.Ordinal);

            CollectionAssert.AreEqual(
                new long[] { first, second },
                matches.Select(static match => match.Utf16Column).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void WholeWord_ShouldClassifySupplementaryScalarsAcrossReaderBlocks()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "split-scalars.txt");
        var content = new string('x', SearchCharBuffer.RequestedLength - 5) +
                      " foo😀 foo𐐀";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var matches = ReadMatches(path, "foo", wholeWord: true);

            Assert.AreEqual(1, matches.Length);
            Assert.AreEqual(SearchCharBuffer.RequestedLength - 4, matches[0].Utf16Column);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CaseAndWordPolicy_ShouldUseStableDefaultsAndRejectUnknownModes()
    {
        Assert.AreEqual(SearchCaseMode.Sensitive, SearchCasePolicy.ParseOptional(null));
        Assert.AreEqual(SearchCaseMode.Sensitive, SearchCasePolicy.Parse("sensitive"));
        Assert.AreEqual(SearchCaseMode.Insensitive, SearchCasePolicy.Parse("INSENSITIVE"));
        Assert.AreEqual("sensitive", SearchCasePolicy.ToContractValue(SearchCaseMode.Sensitive));
        Assert.AreEqual("insensitive", SearchCasePolicy.ToContractValue(SearchCaseMode.Insensitive));

        var request = SearchRequest.Create("root", "literal");
        Assert.AreEqual(SearchCaseMode.Sensitive, request.CaseMode);
        Assert.IsFalse(request.WholeWord);

        var exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchCasePolicy.Parse(null));
        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("case", exception.Diagnostic.Location?.ArgumentName);

        exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchCasePolicy.Parse("unicode"));
        Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, exception.Diagnostic.Code);
        Assert.AreEqual("case", exception.Diagnostic.Location?.ArgumentName);
    }

    private static SearchMatch[] ReadMatches(
        string path,
        string literal,
        string? caseMode = null,
        bool wholeWord = false)
    {
        var request = SearchRequest.Create(
            path,
            literal,
            caseMode: caseMode,
            wholeWord: wholeWord);
        return new SearchMatchesSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-case-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
