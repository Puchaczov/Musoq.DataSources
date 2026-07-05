using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesCsvConfigurationFactoryTests
{
    [TestMethod]
    public void Create_WhenBufferIsHuge_CapsParserBuffer()
    {
        var configuration = SeparatedValuesCsvConfigurationFactory.Create(
            ",",
            4 * 1024 * 1024,
            false);

        Assert.AreEqual(SeparatedValuesCsvConfigurationFactory.MaximumParserBufferSize, configuration.BufferSize);
    }

    [TestMethod]
    public void Create_WhenBufferIsSmall_UsesRequestedParserBuffer()
    {
        var configuration = SeparatedValuesCsvConfigurationFactory.Create(
            ",",
            64 * 1024,
            false);

        Assert.AreEqual(64 * 1024, configuration.BufferSize);
    }

    [TestMethod]
    public void Create_UsesSafeParserDefaults()
    {
        var configuration = SeparatedValuesCsvConfigurationFactory.Create(
            ",",
            64 * 1024,
            false);

        Assert.AreEqual(SeparatedValuesCsvConfigurationFactory.ProcessFieldBufferSize, configuration.ProcessFieldBufferSize);
        Assert.IsFalse(configuration.CountBytes);
        Assert.IsFalse(configuration.DetectDelimiter);
        Assert.IsFalse(configuration.DetectColumnCountChanges);
        Assert.AreEqual(TrimOptions.None, configuration.TrimOptions);
        Assert.IsFalse(configuration.ExceptionMessagesContainRawData);
    }
}
