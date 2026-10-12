using System;
using System.Text.Json;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonNativePredicateAllocationTests
{
    [TestMethod]
    [DataRow((int)StructuredValueKind.Long)]
    [DataRow((int)StructuredValueKind.Decimal)]
    [DataRow((int)StructuredValueKind.Double)]
    [DataRow((int)StructuredValueKind.Boolean)]
    public void BoundPredicate_WhenRepeatedlyEvaluated_AllocatesNoPerValueStorage(int valueKind)
    {
        var kind = (StructuredValueKind)valueKind;
        var term = kind == StructuredValueKind.Boolean
            ? JsonPredicateTerm.Create("Value", kind, true, SourcePredicateComparisonOperator.Equal)
            : JsonPredicateTerm.Create("Value", kind, 1L, SourcePredicateComparisonOperator.GreaterThan);
        long allocated = -1;
        var matches = 0;
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                for (var warmup = 0; warmup < 1000; warmup++)
                    _ = Evaluate(term, kind);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < 20_000; index++)
                    if (Evaluate(term, kind))
                        matches++;
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        Assert.IsNull(failure);
        Assert.AreEqual(20_000, matches);
        Assert.AreEqual(0L, allocated);
    }

    private static bool Evaluate(JsonPredicateTerm term, StructuredValueKind kind)
    {
        var reader = new Utf8JsonReader(kind == StructuredValueKind.Boolean ? "true"u8 : "2"u8);
        if (!reader.Read())
            throw new InvalidOperationException("The fixed predicate input must contain one scalar.");
        return term.Evaluate(ref reader);
    }
}
