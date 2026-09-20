using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class StructuredTypeStateTests
{
    [TestMethod]
    public void Observe_WhenNumericKindsWiden_UsesLongDecimalDoubleLattice()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.Long, StructuredTypeConflictBehavior.WidenToString)
            .Observe(StructuredValueKind.Decimal, StructuredTypeConflictBehavior.WidenToString);

        Assert.AreEqual(StructuredValueKind.Decimal, state.Kind);
        Assert.AreEqual(typeof(decimal), state.ToClrType());

        state = state.Observe(StructuredValueKind.Double, StructuredTypeConflictBehavior.WidenToString);

        Assert.AreEqual(StructuredValueKind.Double, state.Kind);
        Assert.AreEqual(typeof(double), state.ToClrType());
    }

    [TestMethod]
    public void Observe_WhenCsvKindsConflict_WidensToString()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.Boolean, StructuredTypeConflictBehavior.WidenToString)
            .Observe(StructuredValueKind.Long, StructuredTypeConflictBehavior.WidenToString);

        Assert.AreEqual(StructuredValueKind.String, state.Kind);
        Assert.AreEqual(typeof(string), state.ToClrType());
    }

    [TestMethod]
    public void Observe_WhenJsonKindsConflict_WidensToObject()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.String, StructuredTypeConflictBehavior.WidenToObject)
            .Observe(StructuredValueKind.Boolean, StructuredTypeConflictBehavior.WidenToObject);

        Assert.AreEqual(StructuredValueKind.Object, state.Kind);
        Assert.AreEqual(typeof(object), state.ToClrType());
    }

    [TestMethod]
    public void Observe_WhenValueIsNull_MakesValueTypeNullable()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.Long, StructuredTypeConflictBehavior.WidenToString)
            .Observe(StructuredValueKind.Null, StructuredTypeConflictBehavior.WidenToString);

        Assert.IsTrue(state.IsNullable);
        Assert.AreEqual(typeof(long?), state.ToClrType());
    }

    [TestMethod]
    public void WithMissingValue_WhenColumnIsSparse_MakesValueTypeNullable()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.Boolean, StructuredTypeConflictBehavior.WidenToObject)
            .WithMissingValue();

        Assert.IsTrue(state.IsNullable);
        Assert.AreEqual(typeof(bool?), state.ToClrType());
    }

    [TestMethod]
    public void Observe_WhenOnlyNullsAreSeen_UsesNullableObjectState()
    {
        var state = StructuredTypeState.Empty
            .Observe(StructuredValueKind.Null, StructuredTypeConflictBehavior.WidenToObject);

        Assert.AreEqual(StructuredValueKind.Unknown, state.Kind);
        Assert.IsTrue(state.IsNullable);
        Assert.AreEqual(typeof(object), state.ToClrType());
    }
}
