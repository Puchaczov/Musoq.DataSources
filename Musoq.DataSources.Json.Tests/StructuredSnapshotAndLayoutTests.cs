using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class StructuredSnapshotAndLayoutTests
{
    [TestMethod]
    public void Bind_WhenColumnsAreExplicit_ProducesDenseQueryOrder()
    {
        var snapshot = CreateSnapshot(
            Column("Name", 0, StructuredValueKind.String),
            Column("Age", 1, StructuredValueKind.Long),
            Column("Late", 2, StructuredValueKind.Boolean, true));

        var layout = StructuredExecutionLayout.Bind(snapshot, ["Late", "Name", "Late"], false);

        Assert.AreEqual(2, layout.OutputColumnCount);
        Assert.AreEqual("Late", layout.Bindings[0].Name);
        Assert.AreEqual(2, layout.Bindings[0].SourceOrdinal);
        Assert.AreEqual(0, layout.Bindings[0].OutputOrdinal);
        Assert.AreEqual(typeof(bool?), layout.Bindings[0].ClrType);
        Assert.AreEqual("Name", layout.Bindings[1].Name);
        Assert.AreEqual(0, layout.Bindings[1].SourceOrdinal);
        Assert.AreEqual(1, layout.Bindings[1].OutputOrdinal);
    }

    [TestMethod]
    public void Bind_WhenCompleteSchemaIsRequested_PreservesFirstSeenOrder()
    {
        var snapshot = CreateSnapshot(
            Column("Second", 0, StructuredValueKind.Long),
            Column("First", 1, StructuredValueKind.String));

        var layout = StructuredExecutionLayout.Bind(snapshot, ["First"], true);

        CollectionAssert.AreEqual(
            new[] { "Second", "First" },
            layout.Bindings.Select(binding => binding.Name).ToArray());
        Assert.IsTrue(layout.IncludesCompleteSchema);
    }

    [TestMethod]
    public void Bind_WhenColumnDoesNotExist_IsOrdinalAndCaseSensitive()
    {
        var snapshot = CreateSnapshot(Column("Name", 0, StructuredValueKind.String));

        try
        {
            _ = StructuredExecutionLayout.Bind(snapshot, ["name"], false);
            Assert.Fail("Binding should reject a differently-cased name.");
        }
        catch (StructuredUnknownColumnException exception)
        {
            Assert.AreEqual("name", exception.ColumnName);
        }
    }

    [TestMethod]
    public void EnsureCompatibleWith_WhenUnreferencedColumnIsAdded_AllowsExplicitLayout()
    {
        var original = CreateSnapshot(Column("Name", 0, StructuredValueKind.String));
        var changed = CreateSnapshot(
            Column("Name", 0, StructuredValueKind.String),
            Column("Added", 1, StructuredValueKind.Long));
        var layout = StructuredExecutionLayout.Bind(original, ["Name"], false);

        layout.EnsureCompatibleWith(changed);
    }

    [TestMethod]
    public void EnsureCompatibleWith_WhenCompleteSchemaChanges_ThrowsDrift()
    {
        var original = CreateSnapshot(Column("Name", 0, StructuredValueKind.String));
        var changed = CreateSnapshot(
            Column("Name", 0, StructuredValueKind.String),
            Column("Added", 1, StructuredValueKind.Long));
        var layout = StructuredExecutionLayout.Bind(original, null, true);

        AssertDrift(() => layout.EnsureCompatibleWith(changed), "column count changed");
    }

    [TestMethod]
    public void EnsureCompatibleWith_WhenBoundTypeChanges_ThrowsDrift()
    {
        var original = CreateSnapshot(Column("Value", 0, StructuredValueKind.Long));
        var changed = CreateSnapshot(Column("Value", 0, StructuredValueKind.Decimal));
        var layout = StructuredExecutionLayout.Bind(original, ["Value"], false);

        AssertDrift(() => layout.EnsureCompatibleWith(changed), "column 'Value' changed");
    }

    [TestMethod]
    public void Snapshot_WhenColumnsAreDuplicated_RejectsSchema()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateSnapshot(
            Column("Value", 0, StructuredValueKind.Long),
            Column("Value", 1, StructuredValueKind.Long)));
    }

    [TestMethod]
    public void Snapshot_WhenPartitionsOverlap_RejectsSchema()
    {
        var identity = Identity(length: 100);

        Assert.ThrowsExactly<ArgumentException>(() => new StructuredSchemaSnapshot(
            identity,
            [Column("Value", 0, StructuredValueKind.Long)],
            10,
            [new StructuredPartition(0, 60, 0, 6), new StructuredPartition(50, 100, 6, 4)]));
    }

    [TestMethod]
    public void SchemaBuilder_WhenColumnsAppearLate_PreservesFirstSeenOrderAndNullability()
    {
        var builder = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToObject);
        builder.BeginRow();
        builder.Observe("First", StructuredValueKind.Long);
        builder.BeginRow();
        builder.Observe("Late", StructuredValueKind.Boolean);
        builder.BeginRow();
        builder.Observe("First", StructuredValueKind.Long);
        builder.Observe("Late", StructuredValueKind.Null);

        var snapshot = builder.Build(Identity());

        CollectionAssert.AreEqual(
            new[] { "First", "Late" },
            snapshot.Columns.Select(column => column.Name).ToArray());
        Assert.AreEqual(2L, snapshot.Columns[0].PresentValueCount);
        Assert.AreEqual(typeof(long?), snapshot.Columns[0].ClrType);
        Assert.AreEqual(2L, snapshot.Columns[1].PresentValueCount);
        Assert.AreEqual(typeof(bool?), snapshot.Columns[1].ClrType);
    }

    [TestMethod]
    public void SchemaBuilder_WhenFieldRepeatsWithinRow_RejectsDuplicate()
    {
        var builder = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToObject);
        builder.BeginRow();
        builder.Observe("Value", StructuredValueKind.Long);

        var exception = Assert.ThrowsExactly<StructuredDuplicateFieldException>(() =>
            builder.Observe("Value", StructuredValueKind.Long));

        Assert.AreEqual("Value", exception.FieldName);
        Assert.AreEqual(0L, exception.RowIndex);
    }

    [TestMethod]
    public void SchemaBuilder_WhenKindsConflict_AppliesFormatPolicy()
    {
        var csv = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToString);
        csv.BeginRow();
        csv.Observe("Value", StructuredValueKind.Long);
        csv.BeginRow();
        csv.Observe("Value", StructuredValueKind.Boolean);

        var json = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToObject);
        json.BeginRow();
        json.Observe("Value", StructuredValueKind.Long);
        json.BeginRow();
        json.Observe("Value", StructuredValueKind.Boolean);

        Assert.AreEqual(StructuredValueKind.String, csv.Build(Identity()).Columns[0].TypeState.Kind);
        Assert.AreEqual(StructuredValueKind.Object, json.Build(Identity()).Columns[0].TypeState.Kind);
    }

    [TestMethod]
    public void SchemaBuilder_WhenJsonPropertyNameIsEmpty_PreservesExactName()
    {
        var builder = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToObject);
        builder.BeginRow();
        builder.Observe(string.Empty, StructuredValueKind.String);

        var snapshot = builder.Build(Identity());
        var layout = StructuredExecutionLayout.Bind(snapshot, [string.Empty], false);

        Assert.AreEqual(string.Empty, snapshot.Columns[0].Name);
        Assert.AreEqual(string.Empty, layout.Bindings[0].Name);
    }

    private static void AssertDrift(Action action, string expectedText)
    {
        try
        {
            action();
            Assert.Fail("Schema drift should have been rejected.");
        }
        catch (StructuredSchemaDriftException exception)
        {
            StringAssert.Contains(exception.Message, expectedText);
        }
    }

    private static StructuredSchemaSnapshot CreateSnapshot(params StructuredColumnSnapshot[] columns)
    {
        return new StructuredSchemaSnapshot(Identity(), columns, 10);
    }

    private static StructuredColumnSnapshot Column(
        string name,
        int ordinal,
        StructuredValueKind kind,
        bool nullable = false)
    {
        return new StructuredColumnSnapshot(name, ordinal, new StructuredTypeState(kind, nullable), 10);
    }

    private static StructuredFileIdentity Identity(long length = 10)
    {
        return new StructuredFileIdentity(
            "C:\\structured\\source.data",
            length,
            1,
            "test",
            new StructuredFileFingerprint(1, 2));
    }
}
