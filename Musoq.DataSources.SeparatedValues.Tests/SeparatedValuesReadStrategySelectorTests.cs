using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesReadStrategySelectorTests
{
    [DataTestMethod]
    [DataRow(0L, 4 * 1024)]
    [DataRow(4 * 1024L + 1, 8 * 1024)]
    [DataRow(70 * 1024L, 128 * 1024)]
    [DataRow(2 * 1024L * 1024L, 1024 * 1024)]
    public void SequentialInputBuffer_ForOrdinaryScan_FitsFileWithinBoundedPowerOfTwo(
        long fileLength,
        int expected)
    {
        Assert.AreEqual(
            expected,
            SeparatedValuesScanPipeline.SelectSequentialInputBufferSize(fileLength, isEarlyTake: false));
    }

    [DataTestMethod]
    [DataRow(0L, 4 * 1024)]
    [DataRow(4 * 1024L + 1, 8 * 1024)]
    [DataRow(70 * 1024L, 64 * 1024)]
    [DataRow(2 * 1024L * 1024L, 64 * 1024)]
    public void SequentialInputBuffer_ForEarlyTake_NeverExceedsSixtyFourKibibytes(
        long fileLength,
        int expected)
    {
        Assert.AreEqual(
            expected,
            SeparatedValuesScanPipeline.SelectSequentialInputBufferSize(fileLength, isEarlyTake: true));
    }

    [DataTestMethod]
    [DataRow(0L, 4_096, 512)]
    [DataRow(1024L * 1024L, 4_096, 512)]
    [DataRow(1024L * 1024L + 1L, 4_096, 4_096)]
    [DataRow(100L, 16, 16)]
    public void SequentialQueryChunkSize_CapsOnlySmallSources(
        long fileLength,
        int plannedChunkSize,
        int expected)
    {
        Assert.AreEqual(
            expected,
            SeparatedValuesScanPipeline.SelectSequentialQueryChunkSize(fileLength, plannedChunkSize));
    }

    [TestMethod]
    public void Select_WhenFileIsHuge_UsesEstimatedOneMebibyteOutput()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            21L * 1024L * 1024L * 1024L,
            4,
            4,
            null,
            false,
            true));

        Assert.AreEqual(6553, strategy.RowChunkSize);
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

    [TestMethod]
    public void Select_WhenProjectionIsEmpty_KeepsRepeatedMillionRowChunks()
    {
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            64L * 1024L * 1024L * 1024L,
            0,
            100,
            null,
            false,
            true));

        Assert.AreEqual(1024 * 1024, strategy.RowChunkSize);
    }

    [TestMethod]
    public void Select_ForSameProjection_IsIndependentOfFileSize()
    {
        var small = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            1,
            1,
            100,
            null,
            false,
            true));
        var huge = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            long.MaxValue,
            1,
            100,
            null,
            false,
            true));

        Assert.AreEqual(16384, small.RowChunkSize);
        Assert.AreEqual(small.RowChunkSize, huge.RowChunkSize);
    }
}
