#nullable enable
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests;

[TestClass]
public class StructuredComputedProjectionTests
{
    [DataTestMethod]
    [DataRow(SourceScalarBinaryOperator.Add, 9)]
    [DataRow(SourceScalarBinaryOperator.Subtract, 5)]
    [DataRow(SourceScalarBinaryOperator.Multiply, 14)]
    [DataRow(SourceScalarBinaryOperator.Divide, 3)]
    [DataRow(SourceScalarBinaryOperator.Modulo, 1)]
    public void NumericOperators_ProduceExactTypedValues(SourceScalarBinaryOperator operation, int expected)
    {
        Assert.AreEqual(expected, Evaluate<int>(new SourceScalarBinary(operation,
            new SourceScalarLiteral(7, typeof(int)), new SourceScalarLiteral(2, typeof(int)), typeof(int))));
    }

    [DataTestMethod]
    [DataRow(SourceScalarUnaryOperator.Negate, -3)]
    [DataRow(SourceScalarUnaryOperator.Positive, 3)]
    [DataRow(SourceScalarUnaryOperator.BitwiseNot, -4)]
    public void UnaryOperators_ProduceExactTypedValues(SourceScalarUnaryOperator operation, int expected)
    {
        Assert.AreEqual(expected, Evaluate<int>(new SourceScalarUnary(operation,
            new SourceScalarLiteral(3, typeof(int)), typeof(int))));
    }

    [TestMethod]
    public void ShapeValidation_RejectsValuesNotAcceptedByThePlan()
    {
        var layout = Layout(new SourceScalarColumn("Id", typeof(int)));
        var plan = SourceExecutionPlan.Empty(new SourceIdentity("test", "test", "test", "test"));
        Assert.ThrowsExactly<InvalidOperationException>(() => StructuredComputedLayout.Validate(layout.Output, plan));
    }

    [TestMethod]
    public void ShapeValidation_RejectsMissingAcceptedValues()
    {
        var layout = Layout(new SourceScalarColumn("Id", typeof(int)));
        var plan = SourceExecutionPlan.Empty(new SourceIdentity("test", "test", "test", "test")) with
        { AcceptedComputedProjections = [layout.Output.Fields[0].ComputedProjection!] };
        Assert.ThrowsExactly<InvalidOperationException>(() => StructuredComputedLayout.Validate(new QueryRowShape([]), plan));
    }

    [TestMethod]
    public void TypedReader_ComputesFromHiddenInputs_WithoutOutputtingThem()
    {
        var expression = new SourceScalarBinary(SourceScalarBinaryOperator.Add,
            new SourceScalarCast(new SourceScalarColumn("Id", typeof(int)), typeof(long)),
            new SourceScalarLiteral(10L, typeof(long)), typeof(long));
        var layout = Layout(expression);
        Assert.AreEqual(1, layout.Output.Fields.Count);
        Assert.AreEqual("Id", layout.Input.Fields[0].Name);
        var plan = new StructuredComputedPlan<InputReader>(layout);
        var reader = new StructuredComputedReader<InputReader>(new InputReader(3, null), plan);
        Assert.AreEqual(13L, reader.Read<long>(0));
    }

    [TestMethod]
    public void NullableComparisons_UseSqlThreeValuedSemantics()
    {
        foreach (var op in new[] { SourceScalarBinaryOperator.Equal, SourceScalarBinaryOperator.NotEqual,
                     SourceScalarBinaryOperator.LessThan })
        {
            var expression = new SourceScalarBinary(op, new SourceScalarColumn("Optional", typeof(int?)),
                new SourceScalarLiteral(2, typeof(int?)), typeof(bool?)) { UsesSqlNullSemantics = true };
            var plan = new StructuredComputedPlan<InputReader>(Layout(expression));
            var reader = new StructuredComputedReader<InputReader>(new InputReader(0, null), plan);
            Assert.IsNull(reader.Read<bool?>(0));
        }
    }

    [TestMethod]
    public void CoalesceAndNullChecks_PreserveNullableValues()
    {
        var coalesce = new SourceScalarCoalesce([
            new SourceScalarColumn("Optional", typeof(int?)), new SourceScalarLiteral(7, typeof(int))], typeof(int));
        var plan = new StructuredComputedPlan<InputReader>(Layout(coalesce));
        var missing = new StructuredComputedReader<InputReader>(new InputReader(0, null), plan);
        var present = new StructuredComputedReader<InputReader>(new InputReader(0, 4), plan);
        Assert.AreEqual(7, missing.Read<int>(0));
        Assert.AreEqual(4, present.Read<int>(0));
        var check = new StructuredComputedPlan<InputReader>(Layout(new SourceScalarNullCheck(new SourceScalarColumn("Optional", typeof(int?)))));
        var reader = new StructuredComputedReader<InputReader>(new InputReader(0, null), check);
        Assert.IsTrue(reader.Read<bool>(0));
    }

    [TestMethod]
    public void BooleanShortCircuit_DoesNotEvaluateUnneededOperand()
    {
        var comparison = new SourceScalarBinary(SourceScalarBinaryOperator.Equal,
            new SourceScalarBinary(SourceScalarBinaryOperator.Divide,
                new SourceScalarLiteral(1, typeof(int)), new SourceScalarLiteral(0, typeof(int)), typeof(int)),
            new SourceScalarLiteral(0, typeof(int)), typeof(bool));
        var expression = new SourceScalarBinary(SourceScalarBinaryOperator.And,
            new SourceScalarLiteral(false, typeof(bool)), comparison, typeof(bool));
        var plan = new StructuredComputedPlan<InputReader>(Layout(expression));
        var reader = new StructuredComputedReader<InputReader>(new InputReader(0, null), plan);
        Assert.IsFalse(reader.Read<bool>(0));
    }

    [TestMethod]
    public void CheckedCastAndArithmetic_PreserveFailures()
    {
        Assert.ThrowsExactly<OverflowException>(() => Evaluate<int>(new SourceScalarCast(
            new SourceScalarLiteral(long.MaxValue, typeof(long)), typeof(int))));
        Assert.ThrowsExactly<DivideByZeroException>(() => Evaluate<int>(new SourceScalarBinary(
            SourceScalarBinaryOperator.Divide, new SourceScalarLiteral(1, typeof(int)),
            new SourceScalarLiteral(0, typeof(int)), typeof(int))));
    }

    [TestMethod]
    public void Partition_RejectsVolatileUnknownAndUnsupportedTypes_AndKeepsRequestedExpressions()
    {
        var accepted = new SourceComputedProjection("ok", new SourceScalarColumn("Id", typeof(int)), typeof(int));
        var unknown = new SourceComputedProjection("unknown", new SourceScalarColumn("Missing", typeof(int)), typeof(int));
        var volatileValue = accepted with { Name = "volatile", Stability = ColumnStability.Volatile };
        var request = new SourcePlanRequest { Identity = new SourceIdentity("test", "test", "test", "test"),
            RequestedComputedProjections = [accepted, unknown, volatileValue] };
        var partition = StructuredComputedLayout.Partition(request, name => name == "Id" ? typeof(int) : null);
        CollectionAssert.AreEqual(new[] { accepted }, System.Linq.Enumerable.ToArray(partition.Accepted));
        CollectionAssert.AreEqual(new[] { unknown, volatileValue }, System.Linq.Enumerable.ToArray(partition.Residual));
    }

    private static T Evaluate<T>(SourceScalarExpression expression)
    {
        var plan = new StructuredComputedPlan<InputReader>(Layout(expression));
        var reader = new StructuredComputedReader<InputReader>(new InputReader(0, null), plan);
        return reader.Read<T>(0);
    }

    private static StructuredComputedLayout Layout(SourceScalarExpression expression)
    {
        var projection = new SourceComputedProjection("computed", expression, expression.ResultType);
        return new StructuredComputedLayout(new QueryRowShape([QueryRowField.ForComputedProjection(0, projection)]), [
            new QueryRowField(0, 0, "Id", typeof(int), false),
            new QueryRowField(1, 1, "Optional", typeof(int?), true)]);
    }

    private readonly ref struct InputReader(int id, int? optional) : IQuerySourceFieldReader
    {
        public T Read<T>(int slot) => slot == 0 && typeof(T) == typeof(int) ? (T)(object)id
            : typeof(T) == typeof(int?) ? (T)(object?)optional! : throw new InvalidOperationException();
    }
}
