using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesReadStrategySelectorTests
{
    [TestMethod]
    public void Select_WhenFileIsHuge_UsesHugeFileProfile()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            21L * 1024L * 1024L * 1024L,
            4,
            4,
            null,
            false,
            true));

        Assert.AreEqual(2048, strategy.RowChunkSize);
    }

    [TestMethod]
    public void Select_WhenTakeIsSmall_CapsChunkRows()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            10L * 1024L * 1024L * 1024L,
            2,
            2,
            10,
            false,
            true));

        Assert.AreEqual(10, strategy.RowChunkSize);
    }

    [TestMethod]
    public void Select_WhenProjectedRowIsWide_CapsChunkRowsByEstimatedMemory()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            512L * 1024L * 1024L,
            200,
            200,
            null,
            false,
            true));

        Assert.AreEqual(512, strategy.RowChunkSize);
    }
}
