#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SeparatedValuesEnumQualificationTests
{
    public static IEnumerable<object[]> BackingKinds
    {
        get
        {
            yield return [typeof(byte?), EnumUnderlyingKind.Byte, EnumScalarValue.FromByte(200), "200", "256"];
            yield return [typeof(sbyte?), EnumUnderlyingKind.SByte, EnumScalarValue.FromSByte(-100), "-100", "128"];
            yield return [typeof(short?), EnumUnderlyingKind.Int16, EnumScalarValue.FromInt16(-30_000), "-30000", "32768"];
            yield return [typeof(ushort?), EnumUnderlyingKind.UInt16, EnumScalarValue.FromUInt16(60_000), "60000", "65536"];
            yield return [typeof(int?), EnumUnderlyingKind.Int32, EnumScalarValue.FromInt32(-2_000_000_000), "-2000000000", "2147483648"];
            yield return [typeof(uint?), EnumUnderlyingKind.UInt32, EnumScalarValue.FromUInt32(4_000_000_000), "4000000000", "-1"];
            yield return [typeof(long?), EnumUnderlyingKind.Int64, EnumScalarValue.FromInt64(-8_000_000_000_000_000_000), "-8000000000000000000", "9223372036854775808"];
            yield return [typeof(ulong?), EnumUnderlyingKind.UInt64, EnumScalarValue.FromUInt64(18_000_000_000_000_000_000), "18000000000000000000", "-1"];
        }
    }

    [TestMethod]
    [DynamicData(nameof(BackingKinds))]
    public void EnumPlans_AllBackingKinds_PreserveNumericValuesAndAliases(
        Type carrierType,
        EnumUnderlyingKind kind,
        EnumScalarValue value,
        string numericText,
        string overflowText)
    {
        var descriptor = Descriptor("Status", kind, value, false);
        var plan = SeparatedValuesEnumPlan.Create(0, carrierType, descriptor);

        var numericBytes = Encoding.UTF8.GetBytes(numericText);
        var numericField = new SeparatedValuesUtf8Field(numericBytes, 0, false, false);
        Assert.IsTrue(plan.TryDecode(numericField, out var numeric));
        Assert.AreEqual(value, ToScalar(numeric, plan.PrimitiveConversion));

        var aliasBytes = Encoding.UTF8.GetBytes("Alias");
        var aliasField = new SeparatedValuesUtf8Field(aliasBytes, 0, false, false);
        Assert.IsTrue(plan.TryDecode(aliasField, out var alias));
        Assert.AreEqual(value, ToScalar(alias, plan.PrimitiveConversion));
        Assert.IsTrue(plan.IsNullable);

        var unknownBytes = Encoding.UTF8.GetBytes(overflowText);
        var unknownField = new SeparatedValuesUtf8Field(unknownBytes, 0, false, false);
        Assert.IsFalse(plan.TryDecode(unknownField, out _), "Overflow and signedness violations must be rejected.");
    }

    [TestMethod]
    public void EnumPlans_PreserveUnknownRepresentableNumbers_AndRequireExactMemberCasing()
    {
        var descriptor = Descriptor(
            "Status",
            EnumUnderlyingKind.Int32,
            EnumScalarValue.FromInt32(7),
            false);
        var plan = SeparatedValuesEnumPlan.Create(0, typeof(int), descriptor);

        var unknownBytes = "99"u8;
        var unknownField = new SeparatedValuesUtf8Field(unknownBytes, 0, false, false);
        Assert.IsTrue(plan.TryDecode(unknownField, out var unknown));
        Assert.AreEqual(EnumScalarValue.FromInt32(99), ToScalar(unknown, plan.PrimitiveConversion));

        var wrongCaseBytes = "alias"u8;
        var wrongCaseField = new SeparatedValuesUtf8Field(wrongCaseBytes, 0, false, false);
        Assert.IsFalse(plan.TryDecode(wrongCaseField, out _));

        var symbolicBytes = "Canonical"u8;
        var symbolicField = new SeparatedValuesUtf8Field(symbolicBytes, 0, false, false);
        Assert.IsTrue(plan.TryDecode(symbolicField, out var symbolic));
        Assert.AreEqual(EnumScalarValue.FromInt32(7), ToScalar(symbolic, plan.PrimitiveConversion));
    }

    [TestMethod]
    public void EnumPlans_HashLookupVerifiesEscapedSymbolicBytes()
    {
        var descriptor = Descriptor(
            "Access",
            EnumUnderlyingKind.UInt32,
            EnumScalarValue.FromUInt32(1),
            true,
            "Read\"Write");
        var plan = SeparatedValuesEnumPlan.Create(0, typeof(uint?), descriptor);
        var bytes = "Read\"\"Write"u8;
        var field = new SeparatedValuesUtf8Field(
            bytes,
            0,
            wasQuoted: true,
            needsUnescaping: true,
            escapeMode: SeparatedValuesEscapeMode.Double,
            quote: (byte)'"');

        Assert.IsTrue(plan.TryDecode(field, out var parsed));
        Assert.AreEqual(EnumScalarValue.FromUInt32(1), ToScalar(parsed, plan.PrimitiveConversion));
    }

    [TestMethod]
    public void EnumPlanner_PreservesAcceptedLeavesAndKeepsUnsupportedShapesResidual()
    {
        var descriptor = Descriptor("Status", EnumUnderlyingKind.Int32, EnumScalarValue.FromInt32(1), false);
        var contract = CreateContract(descriptor);
        var literal = new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), descriptor.Fingerprint);
        var equality = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef("Status")),
            literal);
        var mismatched = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef("Status")),
            new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), new string('A', 64)));
        var residualOr = new SourcePredicateLogical(SourcePredicateLogicalOperator.Or, equality, mismatched);
        var mixed = new SourcePredicateLogical(SourcePredicateLogicalOperator.And, equality, residualOr);

        var result = SeparatedValuesSourcePlanner.Plan(contract, Request(mixed));

        Assert.AreSame(equality, result.AcceptedPredicate);
        Assert.AreSame(residualOr, result.ResidualPredicate);

        var ordinaryLiteral = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef("Status")),
            new SourcePredicateLiteral(1));
        Assert.IsNull(SeparatedValuesSourcePlanner.Plan(contract, Request(ordinaryLiteral)).AcceptedPredicate);

        var ordering = equality with { Operator = SourcePredicateComparisonOperator.GreaterThan };
        Assert.IsNull(SeparatedValuesSourcePlanner.Plan(contract, Request(ordering)).AcceptedPredicate);

        var nullMembership = new SourcePredicateIn(
            new SourcePredicateColumn(new SourceColumnRef("Status")),
            [literal, new SourcePredicateLiteral(null)]);
        Assert.IsNull(SeparatedValuesSourcePlanner.Plan(contract, Request(nullMembership)).AcceptedPredicate);
    }

    [TestMethod]
    public void EnumPlanner_AcceptsNullChecksMembershipAndMatchingFlagsMasks()
    {
        var descriptor = Descriptor("Access", EnumUnderlyingKind.UInt32, EnumScalarValue.FromUInt32(1), true);
        var contract = CreateContract(descriptor, "Access");
        var column = new SourcePredicateColumn(new SourceColumnRef("Access"));
        var read = new SourcePredicateEnumLiteral(EnumScalarValue.FromUInt32(1), descriptor.Fingerprint);
        var membership = new SourcePredicateIn(column, [read, read], IsNegated: true);
        var nullCheck = new SourcePredicateNullCheck(column, IsNegated: true);
        var flags = new SourcePredicateFlags(
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromUInt32(0), descriptor.Fingerprint),
            SourcePredicateFlagsMatchMode.All);

        Assert.AreSame(membership, SeparatedValuesSourcePlanner.Plan(contract, Request(membership)).AcceptedPredicate);
        Assert.AreSame(nullCheck, SeparatedValuesSourcePlanner.Plan(contract, Request(nullCheck)).AcceptedPredicate);
        Assert.AreSame(flags, SeparatedValuesSourcePlanner.Plan(contract, Request(flags)).AcceptedPredicate);
    }

    [TestMethod]
    public void EnumPlanner_AcceptsBothOperandOrdersAndPreservesSupportedAndLeafOrder()
    {
        var descriptor = Descriptor("Status", EnumUnderlyingKind.Int32, EnumScalarValue.FromInt32(1), false);
        var contract = CreateContract(descriptor);
        var column = new SourcePredicateColumn(new SourceColumnRef("Status"));
        var literal = new SourcePredicateEnumLiteral(
            EnumScalarValue.FromInt32(1),
            descriptor.Fingerprint);
        var equality = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            column,
            literal);
        var reverseInequality = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.NotEqual,
            literal,
            column);

        Assert.AreSame(
            equality,
            SeparatedValuesSourcePlanner.Plan(contract, Request(equality)).AcceptedPredicate);
        Assert.AreSame(
            reverseInequality,
            SeparatedValuesSourcePlanner.Plan(contract, Request(reverseInequality)).AcceptedPredicate);

        var combined = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            equality,
            reverseInequality);
        var result = SeparatedValuesSourcePlanner.Plan(contract, Request(combined));
        var accepted = result.AcceptedPredicate as SourcePredicateLogical;
        Assert.IsNotNull(accepted);
        Assert.AreSame(equality, accepted!.Left);
        Assert.AreSame(reverseInequality, accepted.Right);
        Assert.IsNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void EnumPlanner_RejectsCrossEnumKindsFingerprintsAndUnsupportedShapes()
    {
        var descriptor = Descriptor("Status", EnumUnderlyingKind.Int32, EnumScalarValue.FromInt32(1), false);
        var contract = CreateContract(descriptor);
        var column = new SourcePredicateColumn(new SourceColumnRef("Status"));

        var wrongFingerprint = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), new string('a', 64)));
        var wrongKind = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromUInt32(1), descriptor.Fingerprint));
        var ordering = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterThan,
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), descriptor.Fingerprint));
        var ordinaryLiteral = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            column,
            new SourcePredicateLiteral(1));
        var nullMembership = new SourcePredicateIn(
            column,
            [
                new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), descriptor.Fingerprint),
                new SourcePredicateLiteral(null)
            ]);
        var unsupportedOr = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            wrongFingerprint,
            wrongKind);

        foreach (var expression in new SourcePredicateExpression[]
        {
            wrongFingerprint,
            wrongKind,
            ordering,
            ordinaryLiteral,
            nullMembership,
            unsupportedOr
        })
        {
            var result = SeparatedValuesSourcePlanner.Plan(contract, Request(expression));
            Assert.IsNull(result.AcceptedPredicate, expression.GetType().Name);
            Assert.AreSame(expression, result.ResidualPredicate, expression.GetType().Name);
        }

        var nonFlags = new SourcePredicateFlags(
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), descriptor.Fingerprint),
            SourcePredicateFlagsMatchMode.Any);
        Assert.IsNull(SeparatedValuesSourcePlanner.Plan(contract, Request(nonFlags)).AcceptedPredicate);
    }

    [TestMethod]
    public void EnumPredicateEvaluator_AppliesSqlNullAndFlagMaskSemantics()
    {
        var descriptor = new EnumTypeDescriptor(
            "Access",
            EnumTypeOrigin.QueryLocal,
            EnumUnderlyingKind.Int32,
            true,
            [
                new EnumMemberDescriptor("Read", EnumScalarValue.FromInt32(1)),
                new EnumMemberDescriptor("Write", EnumScalarValue.FromInt32(2)),
                new EnumMemberDescriptor("ReadWrite", EnumScalarValue.FromInt32(3))
            ]);
        var column = new SourcePredicateColumn(new SourceColumnRef("Status"));
        var one = new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(1), descriptor.Fingerprint);
        var two = new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(2), descriptor.Fingerprint);
        var zero = new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(0), descriptor.Fingerprint);

        var equal = new SourcePredicateComparison(SourcePredicateComparisonOperator.Equal, column, one);
        var notEqual = new SourcePredicateComparison(SourcePredicateComparisonOperator.NotEqual, column, one);
        var membership = new SourcePredicateIn(column, [one, two, one]);
        var negatedMembership = new SourcePredicateIn(column, [one, two], IsNegated: true);
        var isNull = new SourcePredicateNullCheck(column, IsNegated: false);
        var isNotNull = new SourcePredicateNullCheck(column, IsNegated: true);
        var anyZero = new SourcePredicateFlags(column, zero, SourcePredicateFlagsMatchMode.Any);
        var allZero = new SourcePredicateFlags(column, zero, SourcePredicateFlagsMatchMode.All);
        var anyRead = new SourcePredicateFlags(column, one, SourcePredicateFlagsMatchMode.Any);
        var allReadWrite = new SourcePredicateFlags(
            column,
            new SourcePredicateEnumLiteral(EnumScalarValue.FromInt32(3), descriptor.Fingerprint),
            SourcePredicateFlagsMatchMode.All);

        Assert.IsTrue(EvaluateEnum(descriptor, equal, "1"));
        Assert.IsFalse(EvaluateEnum(descriptor, equal, "2"));
        Assert.IsFalse(EvaluateEnum(descriptor, equal, string.Empty, isNullToken: true));
        Assert.IsTrue(EvaluateEnum(descriptor, notEqual, "2"));
        Assert.IsFalse(EvaluateEnum(descriptor, notEqual, string.Empty, isNullToken: true));
        Assert.IsTrue(EvaluateEnum(descriptor, membership, "2"));
        Assert.IsFalse(EvaluateEnum(descriptor, membership, "3"));
        Assert.IsFalse(EvaluateEnum(descriptor, membership, string.Empty, isNullToken: true));
        Assert.IsTrue(EvaluateEnum(descriptor, negatedMembership, "3"));
        Assert.IsFalse(EvaluateEnum(descriptor, negatedMembership, "1"));
        Assert.IsFalse(EvaluateEnum(descriptor, negatedMembership, string.Empty, isNullToken: true));
        Assert.IsTrue(EvaluateEnum(descriptor, isNull, string.Empty, isNullToken: true));
        Assert.IsFalse(EvaluateEnum(descriptor, isNull, "1"));
        Assert.IsTrue(EvaluateEnum(descriptor, isNotNull, "1"));
        Assert.IsFalse(EvaluateEnum(descriptor, isNotNull, string.Empty, isNullToken: true));
        Assert.IsFalse(EvaluateEnum(descriptor, anyZero, "3"));
        Assert.IsTrue(EvaluateEnum(descriptor, allZero, "3"));
        Assert.IsFalse(EvaluateEnum(descriptor, anyZero, string.Empty, isNullToken: true));
        Assert.IsTrue(EvaluateEnum(descriptor, anyRead, "3"));
        Assert.IsFalse(EvaluateEnum(descriptor, anyRead, "2"));
        Assert.IsTrue(EvaluateEnum(descriptor, allReadWrite, "3"));
        Assert.IsFalse(EvaluateEnum(descriptor, allReadWrite, "1"));
    }

    [TestMethod]
    public void EnumPredicateEvaluator_UsesTheEightValueBoundaryForMembershipLookup()
    {
        var members = Enumerable.Range(0, 12)
            .Select(index => new EnumMemberDescriptor(
                $"Value{index}",
                EnumScalarValue.FromInt32(index)))
            .ToArray();
        var descriptor = new EnumTypeDescriptor(
            "Status",
            EnumTypeOrigin.QueryLocal,
            EnumUnderlyingKind.Int32,
            false,
            members);
        var column = new SourcePredicateColumn(new SourceColumnRef("Status"));
        var values = members
            .Take(8)
            .Select(member => new SourcePredicateEnumLiteral(member.Value, descriptor.Fingerprint))
            .ToArray();
        var largeValues = members
            .Select(member => new SourcePredicateEnumLiteral(member.Value, descriptor.Fingerprint))
            .ToArray();
        var eight = new SourcePredicateIn(column, values);
        var moreThanEight = new SourcePredicateIn(column, largeValues);

        Assert.AreSame(eight, SeparatedValuesSourcePlanner.Plan(CreateContract(descriptor), Request(eight)).AcceptedPredicate);
        Assert.AreSame(
            moreThanEight,
            SeparatedValuesSourcePlanner.Plan(CreateContract(descriptor), Request(moreThanEight)).AcceptedPredicate);
        Assert.IsTrue(EvaluateEnum(descriptor, eight, "7"));
        Assert.IsFalse(EvaluateEnum(descriptor, eight, "8"));
        Assert.IsTrue(EvaluateEnum(descriptor, moreThanEight, "11"));
        Assert.IsFalse(EvaluateEnum(descriptor, moreThanEight, "99"));
    }

    private static EnumTypeDescriptor Descriptor(
        string name,
        EnumUnderlyingKind kind,
        EnumScalarValue value,
        bool flags,
        string aliasName = "Alias")
    {
        return new EnumTypeDescriptor(
            name,
            EnumTypeOrigin.QueryLocal,
            kind,
            flags,
            [
                new EnumMemberDescriptor("Canonical", value),
                new EnumMemberDescriptor(aliasName, value)
            ]);
    }

    private static EnumScalarValue ToScalar(
        SeparatedValuesParsedValue parsed,
        SeparatedValuesConversion conversion)
    {
        return conversion switch
        {
            SeparatedValuesConversion.Byte => EnumScalarValue.FromByte(parsed.Byte),
            SeparatedValuesConversion.SByte => EnumScalarValue.FromSByte(parsed.SByte),
            SeparatedValuesConversion.Int16 => EnumScalarValue.FromInt16(parsed.Int16),
            SeparatedValuesConversion.UInt16 => EnumScalarValue.FromUInt16(parsed.UInt16),
            SeparatedValuesConversion.Int32 => EnumScalarValue.FromInt32(parsed.Int32),
            SeparatedValuesConversion.UInt32 => EnumScalarValue.FromUInt32(parsed.UInt32),
            SeparatedValuesConversion.Int64 => EnumScalarValue.FromInt64(parsed.Int64),
            SeparatedValuesConversion.UInt64 => EnumScalarValue.FromUInt64(parsed.UInt64),
            _ => throw new AssertFailedException($"Unexpected enum conversion {conversion}.")
        };
    }

    private static bool EvaluateEnum(
        EnumTypeDescriptor descriptor,
        SourcePredicateExpression predicate,
        string input,
        bool isNullToken = false)
    {
        var contract = CreateContract(descriptor);
        var planned = SeparatedValuesSourcePlanner.Plan(contract, Request(predicate));
        Assert.IsNotNull(planned.AcceptedPredicate, "The test predicate must be accepted by the source planner.");
        var evaluator = SeparatedValuesPredicateEvaluator.Create(contract, planned.AcceptedPredicate);
        var carrier = EnumScalarTypeFacts.GetCarrierType(descriptor.UnderlyingKind);
        var plan = SeparatedValuesEnumPlan.Create(0, typeof(Nullable<>).MakeGenericType(carrier), descriptor);
        var field = new SeparatedValuesUtf8Field(
            Encoding.UTF8.GetBytes(input),
            0,
            wasQuoted: false,
            needsUnescaping: false,
            isNullToken: isNullToken);
        var parsed = default(SeparatedValuesParsedValue);
        if (!isNullToken)
            Assert.IsTrue(plan.TryDecode(field, out parsed), $"Could not decode '{input}'.");

        var termIndex = 0;
        var result = evaluator.EvaluateField(0, field, parsed, 1, ref termIndex);
        if (result)
            Assert.IsTrue(evaluator.IsComplete(termIndex));
        return result;
    }

    private static SeparatedValuesSourceContract CreateContract(
        EnumTypeDescriptor descriptor,
        string columnName = "Status")
    {
        var carrier = EnumScalarTypeFacts.GetCarrierType(descriptor.UnderlyingKind);
        var nullableCarrier = typeof(Nullable<>).MakeGenericType(carrier);
        var identity = new StructuredFileIdentity(
            "enum-qualification.csv",
            0,
            0,
            "enum-qualification",
            new StructuredFileFingerprint(1, 2));
        var snapshot = new StructuredSchemaSnapshot(
            identity,
            [new StructuredColumnSnapshot(
                columnName,
                0,
                new StructuredTypeState(StructuredValueKind.Long, true),
                0,
                nullableCarrier,
                nullableCarrier,
                descriptor)],
            0);
        return new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Declared,
            false,
            0,
            0,
            TimeSpan.Zero,
            dialect: SeparatedValuesDialect.Strict((byte)','));
    }

    private static SourcePlanRequest Request(SourcePredicateExpression predicate)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("separatedvalues", "comma", "enum-qualification", "comma"),
            RequiredColumns = [],
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = null
        };
    }
}
