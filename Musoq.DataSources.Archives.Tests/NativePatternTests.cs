using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
namespace Musoq.DataSources.Archives.Tests;
[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void AllEntryStringMetadata_MatchesCore()
        => PatternContractVerifier.Verify<EntryWrapper, SourceArguments<string, EmptySourceArguments>>(
            new ArchivesSchema(), "file", SourceArguments.Create("./Files/Example1/archives.zip", SourceArguments.Empty), ["./Files/Example1/archives.zip"]);
}
