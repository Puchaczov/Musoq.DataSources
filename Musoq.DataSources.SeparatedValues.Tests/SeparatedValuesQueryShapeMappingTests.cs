#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public sealed class SeparatedValuesQueryShapeMappingTests
{
    [TestMethod]
    public void SequentialProjectorSeam_RequiresAValueTypeProjector()
    {
        var projectorParameter = typeof(SeparatedValuesProjectedRowProcessor<,>)
            .GetGenericArguments()[1];

        Assert.IsTrue(
            (projectorParameter.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0);
        Assert.IsTrue(Array.Exists(
            projectorParameter.GetGenericParameterConstraints(),
            constraint => constraint.IsGenericType &&
                          constraint.GetGenericTypeDefinition() == typeof(ISeparatedValuesRowProjector<>)));
        Assert.IsTrue(typeof(SeparatedValuesQueryRowProjector<,>).IsValueType);
    }

    [TestMethod]
    public void Mapping_WhenProjectionIsReordered_UsesDenseMetadataAndPhysicalSnapshotOrdinals()
    {
        var contract = CreateContract(
            ("A", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("B", typeof(long), new StructuredTypeState(StructuredValueKind.Long, false)),
            ("C", typeof(bool), new StructuredTypeState(StructuredValueKind.Boolean, false)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["C", "A"], false);
        ISchemaColumn[] plannedColumns =
        [
            new SchemaColumn("C", 0, typeof(bool)),
            new SchemaColumn("A", 1, typeof(string))
        ];
        var shape = new QueryRowShape(
        [
            new QueryRowField(0, 0, "C", typeof(bool), false),
            new QueryRowField(1, 1, "A", typeof(string), true)
        ]);

        var success = SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out var mapping,
            out var reason);

        Assert.IsTrue(success, reason);
        Assert.IsNotNull(mapping);
        Assert.AreEqual(2, mapping.Fields.Length);
        Assert.AreEqual(0, mapping.Fields[0].DenseSourceColumnIndex);
        Assert.AreEqual(2, mapping.Fields[0].PhysicalSourceOrdinal);
        Assert.AreEqual(1, mapping.Fields[1].DenseSourceColumnIndex);
        Assert.AreEqual(0, mapping.Fields[1].PhysicalSourceOrdinal);

        var projection = SeparatedValuesQueryProjectionPlan.Create(contract, mapping);
        Assert.AreEqual(1, projection.GetSlotBinding(0).CaptureIndex);
        Assert.AreEqual(2, projection.GetSlotBinding(0).PhysicalSourceOrdinal);
        Assert.AreEqual(0, projection.GetSlotBinding(1).CaptureIndex);
        Assert.AreEqual(0, projection.GetSlotBinding(1).PhysicalSourceOrdinal);
        Assert.AreEqual("C", projection.GetDiagnostic(0).Name);
        Assert.AreEqual("A", projection.GetDiagnostic(1).Name);
    }

    [TestMethod]
    public void QuerySlotBinding_HotStateContainsOnlyValueFields()
    {
        var referenceFields = typeof(SeparatedValuesQuerySlotBinding)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(static field => !field.FieldType.IsValueType)
            .Select(static field => $"{field.Name}: {field.FieldType}")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), referenceFields);
    }

    [TestMethod]
    public void Mapping_WhenQueryDenseOrderDiffersFromExecutionLayoutOrder_MatchesByName()
    {
        var contract = CreateContract(
            ("Name", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("Team", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("Age", typeof(long?), new StructuredTypeState(StructuredValueKind.Long, true)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["Name", "Age"], false);
        ISchemaColumn[] plannedColumns =
        [
            new SchemaColumn("Age", 0, typeof(long?)),
            new SchemaColumn("Name", 1, typeof(string))
        ];
        var shape = new QueryRowShape(
        [
            new QueryRowField(0, 0, "Age", typeof(long?), true),
            new QueryRowField(1, 1, "Name", typeof(string), true)
        ]);

        Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out var mapping,
            out var reason), reason);
        Assert.AreEqual(2, mapping!.Fields[0].PhysicalSourceOrdinal);
        Assert.AreEqual(0, mapping.Fields[1].PhysicalSourceOrdinal);
    }

    [TestMethod]
    public void Mapping_WhenHeaderlessColumnIsProjected_UsesColumnNameAndPhysicalOrdinal()
    {
        var contract = CreateContract(
            ("Column1", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("Column2", typeof(long?), new StructuredTypeState(StructuredValueKind.Long, true)),
            ("Column3", typeof(Guid), new StructuredTypeState(StructuredValueKind.String, false)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["Column3"], false);
        ISchemaColumn[] plannedColumns = [new SchemaColumn("Column3", 0, typeof(Guid))];
        var shape = new QueryRowShape([new QueryRowField(0, 0, "Column3", typeof(Guid), false)]);

        Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out var mapping,
            out var reason), reason);
        Assert.AreEqual(2, mapping!.Fields[0].PhysicalSourceOrdinal);
    }

    [TestMethod]
    public void Mapping_WhenShapeNameIsQualified_MatchesThePlannedUnqualifiedName()
    {
        var contract = CreateContract(
            ("A", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("json.path/value", typeof(bool), new StructuredTypeState(StructuredValueKind.Boolean, false)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["json.path/value"], false);
        ISchemaColumn[] plannedColumns = [new SchemaColumn("source.json.path/value", 0, typeof(bool))];
        var shape = new QueryRowShape(
            [new QueryRowField(0, 0, "source.json.path/value", typeof(bool), false)]);

        Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out var mapping,
            out var reason), reason);
        Assert.AreEqual("json.path/value", mapping!.Fields[0].Name);
        Assert.AreEqual(1, mapping.Fields[0].PhysicalSourceOrdinal);
    }

    [TestMethod]
    public void Mapping_WhenShapeTypeDiffersFromPlannedType_IsRejected()
    {
        var contract = CreateContract(
            ("Value", typeof(long?), new StructuredTypeState(StructuredValueKind.Long, true)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["Value"], false);
        ISchemaColumn[] plannedColumns = [new SchemaColumn("Value", 0, typeof(long?))];
        var shape = new QueryRowShape([new QueryRowField(0, 0, "Value", typeof(long), false)]);

        Assert.IsFalse(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out _,
            out var reason));
        StringAssert.Contains(reason, "does not match planned type");
    }

    [TestMethod]
    public void Mapping_WhenShapeNullabilityDiffersFromPlannedNullability_IsRejected()
    {
        var contract = CreateContract(
            ("Value", typeof(long?), new StructuredTypeState(StructuredValueKind.Long, true)));
        var layout = StructuredExecutionLayout.Bind(contract.Snapshot, ["Value"], false);
        ISchemaColumn[] plannedColumns = [new SchemaColumn("Value", 0, typeof(long?))];
        var shape = new QueryRowShape([new QueryRowField(0, 0, "Value", typeof(long?), false)]);

        Assert.IsFalse(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            plannedColumns,
            shape,
            out _,
            out var reason));
        StringAssert.Contains(reason, "nullability");
    }

    [TestMethod]
    public void Metadata_WhenNamesAreCaseInsensitivelyDuplicated_IsRejected()
    {
        var contract = CreateContract(
            ("Name", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)),
            ("name", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)));
        ISchemaColumn[] columns =
        [
            new SchemaColumn("Name", 0, typeof(string)),
            new SchemaColumn("name", 1, typeof(string))
        ];

        Assert.IsFalse(SeparatedValuesQueryMetadata.TryCreateForDescriptor(
            contract,
            columns,
            out _,
            out var reason));
        StringAssert.Contains(reason, "duplicate column name");
    }

    [TestMethod]
    public void Metadata_WhenReadModifierIsPresent_IsRejected()
    {
        var contract = CreateContract(
            ("Name", typeof(string), new StructuredTypeState(StructuredValueKind.String, true)));
        ISchemaColumn[] columns =
        [
            new SchemaColumn(
                "Name",
                0,
                typeof(string),
                new Dictionary<string, string> { ["unsupported"] = "value" })
        ];

        Assert.IsFalse(SeparatedValuesQueryMetadata.TryCreateForDescriptor(
            contract,
            columns,
            out _,
            out var reason));
        StringAssert.Contains(reason, "unsupported read modifiers");
    }

    [TestMethod]
    public void Metadata_WhenTypeIsObject_IsRejected()
    {
        var contract = CreateContract(
            ("Value", typeof(object), new StructuredTypeState(StructuredValueKind.Object, true)));
        ISchemaColumn[] columns = [new SchemaColumn("Value", 0, typeof(object))];

        Assert.IsFalse(SeparatedValuesQueryMetadata.TryCreateForDescriptor(
            contract,
            columns,
            out _,
            out var reason));
        StringAssert.Contains(reason, "unsupported exact type");
    }

    [TestMethod]
    public void Metadata_WhenShapeHasNoFields_IsEligibleAndImmutable()
    {
        var contract = CreateContract();

        Assert.IsTrue(SeparatedValuesQueryMetadata.TryCreateForDescriptor(
            contract,
            [],
            out var metadata,
            out var reason), reason);
        Assert.IsTrue(metadata!.Columns.IsEmpty);
    }

    private static SeparatedValuesSourceContract CreateContract(
        params (string Name, Type ExactType, StructuredTypeState TypeState)[] columns)
    {
        var snapshots = new StructuredColumnSnapshot[columns.Length];
        var types = new Type[columns.Length];
        for (var index = 0; index < columns.Length; index++)
        {
            snapshots[index] = new StructuredColumnSnapshot(
                columns[index].Name,
                index,
                columns[index].TypeState,
                0);
            types[index] = columns[index].ExactType;
        }

        var snapshot = new StructuredSchemaSnapshot(
            new StructuredFileIdentity("query-shape.csv", 0, 0, "query-shape", default),
            snapshots,
            0);
        return new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Declared,
            false,
            0,
            0,
            TimeSpan.Zero,
            columnTypes: types);
    }
}
