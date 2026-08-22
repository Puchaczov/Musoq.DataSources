#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesOutputMemoryBudgetTests
{
    [TestMethod]
    public async Task AcquireAsync_WhenFirstResultIsDelayed_BoundsTheHighWaterMark()
    {
        var budget = new SeparatedValuesOutputMemoryBudget(1024, 1);
        using var first = await budget.AcquireAsync(700, CancellationToken.None);
        var secondTask = budget.AcquireAsync(400, CancellationToken.None).AsTask();

        Assert.IsTrue(SpinWait.SpinUntil(() => budget.HighWaterBytes == budget.CapacityBytes, 1000));
        Assert.IsFalse(secondTask.IsCompleted);
        Assert.AreEqual(1024L, budget.CurrentReservedBytes);
        Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);

        first.Dispose();
        using var second = await secondTask;
        Assert.AreEqual(400L, budget.CurrentReservedBytes);
        Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);
    }

    [TestMethod]
    public async Task AcquireAsync_WhenResultIsOversized_RunsItExclusively()
    {
        var budget = new SeparatedValuesOutputMemoryBudget(1024, 1);
        using var predecessor = await budget.AcquireAsync(256, CancellationToken.None);
        var oversizedTask = budget.AcquireAsync(2048, CancellationToken.None).AsTask();

        await Task.Delay(20);
        Assert.IsFalse(oversizedTask.IsCompleted);
        predecessor.Dispose();

        using var oversized = await oversizedTask;
        Assert.IsTrue(oversized.IsOversized);
        Assert.AreEqual(2048L, oversized.RequestedBytes);
        Assert.AreEqual(1024L, oversized.ReservedBytes);
        Assert.AreEqual(1024L, budget.CurrentReservedBytes);
        Assert.AreEqual(1L, budget.OversizedReservationCount);
        Assert.AreEqual(2048L, budget.LargestOversizedRequestBytes);

        var followerTask = budget.AcquireAsync(1, CancellationToken.None).AsTask();
        await Task.Delay(20);
        Assert.IsFalse(followerTask.IsCompleted);
        oversized.Dispose();
        using var follower = await followerTask;
        Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);
    }

    [TestMethod]
    public async Task AcquireAsync_WhenCancelledAfterPartialAcquisition_ReturnsEveryPermit()
    {
        var budget = new SeparatedValuesOutputMemoryBudget(1024, 1);
        using var predecessor = await budget.AcquireAsync(800, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var pending = budget.AcquireAsync(500, cancellation.Token).AsTask();

        await Task.Delay(20);
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await pending);
        Assert.AreEqual(800L, budget.CurrentReservedBytes);

        predecessor.Dispose();
        using var complete = await budget.AcquireAsync(1024, CancellationToken.None);
        Assert.AreEqual(1024L, budget.CurrentReservedBytes);
    }

    [TestMethod]
    public void QueryEstimator_IncludesUtf16ExpansionAndPerStringObjects()
    {
        var shape = new QueryRowShape(
        [
            new QueryRowField(0, 0, "A", typeof(string), true),
            new QueryRowField(1, 1, "B", typeof(string), true)
        ]);
        var estimator = SeparatedValuesQueryOutputMemoryEstimator.Create<TestRow2<string, string>>(shape);
        var withoutEncodedContent = estimator.Estimate(10, 0);
        var withEncodedContent = estimator.Estimate(10, 500);

        Assert.AreEqual(1000L, withEncodedContent - withoutEncodedContent);
    }
}
