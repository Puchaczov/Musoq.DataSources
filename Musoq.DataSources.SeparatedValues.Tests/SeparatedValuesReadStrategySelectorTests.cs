using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesReadStrategySelectorTests
{
    [TestMethod]
    public void Select_WhenStreamSizeIsUnknown_UsesConservativeDefaults()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            null,
            true,
            3,
            3,
            null,
            false,
            false,
            true));

        Assert.AreEqual(64 * 1024, strategy.StreamBufferSize);
        Assert.AreEqual(RowChunking.DefaultChunkSize, strategy.RowChunkSize);
    }

    [TestMethod]
    public void Select_WhenFileIsHuge_UsesHugeFileProfile()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            21L * 1024L * 1024L * 1024L,
            false,
            4,
            4,
            null,
            false,
            true,
            true));

        Assert.AreEqual(4 * 1024 * 1024, strategy.StreamBufferSize);
        Assert.AreEqual(2048, strategy.RowChunkSize);
        Assert.IsTrue(strategy.AvoidSecondHeaderOpen);
    }

    [TestMethod]
    public void Select_WhenTakeIsSmall_CapsChunkRows()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            10L * 1024L * 1024L * 1024L,
            false,
            2,
            2,
            10,
            false,
            true,
            true));

        Assert.AreEqual(10, strategy.RowChunkSize);
        Assert.IsTrue(strategy.EnableEarlyTakeFastPath);
    }

    [TestMethod]
    public void Select_WhenProjectionIsZeroColumn_EnablesZeroColumnFastPath()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            1024,
            false,
            0,
            8,
            null,
            false,
            true,
            true));

        Assert.IsTrue(strategy.EnableZeroColumnFastPath);
    }

    [TestMethod]
    public void Select_WhenProjectedRowIsWide_CapsChunkRowsByEstimatedMemory()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            512L * 1024L * 1024L,
            false,
            200,
            200,
            null,
            false,
            true,
            true));

        Assert.AreEqual(512, strategy.RowChunkSize);
    }
}
