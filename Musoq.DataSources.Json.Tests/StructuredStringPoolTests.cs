#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class StructuredStringPoolTests
{
    [TestMethod]
    public void GetOrAddUtf8_WhenValueRepeats_ReturnsSameStringInstance()
    {
        var pool = new StructuredStringPool(1);

        var first = pool.GetOrAddUtf8(0, "station-001"u8);
        var second = pool.GetOrAddUtf8(0, "station-001"u8);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, pool.RetainedValueCount);
        Assert.IsTrue(pool.RetainedBytes <= StructuredStringPool.MaximumRetainedBytes);
    }

    [TestMethod]
    public void GetOrAddUtf8_WhenCalledConcurrently_PublishesOneInstance()
    {
        var pool = new StructuredStringPool(1);
        var values = new ConcurrentBag<string>();

        Parallel.For(0, 1_000, _ => values.Add(pool.GetOrAddUtf8(0, "shared"u8)));

        var first = values.First();
        Assert.IsTrue(values.All(value => ReferenceEquals(first, value)));
        Assert.AreEqual(1, pool.RetainedValueCount);
    }

    [TestMethod]
    public void GetOrAddUtf8_WhenColumnCardinalityLimitIsExceeded_DisablesAndDiscardsPool()
    {
        var pool = new StructuredStringPool(1);

        for (var index = 0; index <= StructuredStringPool.MaximumValuesPerColumn; index++)
            _ = pool.GetOrAddUtf8(0, Encoding.UTF8.GetBytes($"value-{index:D5}"));

        Assert.IsTrue(pool.IsDisabled);
        Assert.AreEqual(0, pool.RetainedValueCount);
        Assert.AreEqual(0L, pool.RetainedBytes);
    }

    [TestMethod]
    public void GetOrAddUtf8_WhenSnapshotBudgetIsExceeded_DisablesAndDiscardsPool()
    {
        var pool = new StructuredStringPool(4);
        var value = new byte[1_500_000];
        Array.Fill(value, (byte)'a');

        for (var column = 0; column < 4 && !pool.IsDisabled; column++)
        {
            value[0] = (byte)('a' + column);
            _ = pool.GetOrAddUtf8(column, value);
        }

        Assert.IsTrue(pool.IsDisabled);
        Assert.AreEqual(0, pool.RetainedValueCount);
        Assert.AreEqual(0L, pool.RetainedBytes);
    }

    [TestMethod]
    public void Snapshot_DefaultEstimate_ReservesTheMaximumStringPoolBudget()
    {
        var identity = new StructuredFileIdentity(
            "pool-budget",
            0,
            0,
            "test",
            new StructuredFileFingerprint(0, 0));
        var snapshot = new StructuredSchemaSnapshot(
            identity,
            [
                new StructuredColumnSnapshot(
                    "Value",
                    0,
                    new StructuredTypeState(StructuredValueKind.String, false),
                    1)
            ],
            1);

        Assert.IsTrue(snapshot.EstimatedSizeBytes >= StructuredStringPool.MaximumRetainedBytes);
    }
}
