using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
namespace Musoq.DataSources.FlatFile.Tests;
[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void FlatFileLinePatterns_MatchCoreOnDecodedLines()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "", "Alpha", "beta", "Łódź", "quote\"text" });
            PatternContractVerifier.For<FlatFileEntity>().Verify(new FlatFileSchema(), "file", SourceArguments.Create(path, SourceArguments.Empty), [path]);
        }
        finally { File.Delete(path); }
    }
}
