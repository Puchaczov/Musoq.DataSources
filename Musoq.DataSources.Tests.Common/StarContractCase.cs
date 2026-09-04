#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Tests.Common;

/// <summary>
///     Describes the executable and metadata contract for one concrete datasource constructor.
/// </summary>
public sealed record StarContractCase(
    string MethodName,
    IReadOnlyList<Type> ParameterTypes,
    IReadOnlyList<object> Arguments,
    string Query,
    IReadOnlyList<StarContractColumn> ExpectedColumns,
    IReadOnlyList<string> ExcludedSchemaColumns)
{
    public string Signature =>
        $"{MethodName}({string.Join(", ", ParameterTypes.Select(type => type.FullName ?? type.Name))})";
}

/// <summary>Describes one column in the exact result shape of a star query.</summary>
public sealed record StarContractColumn(
    string Name,
    Type Type,
    Type? SchemaSourceReadType = null,
    EnumTypeOrigin? EnumOrigin = null,
    string? EnumDisplayName = null,
    EnumUnderlyingKind? EnumUnderlyingKind = null,
    bool? EnumIsFlags = null);

/// <summary>Shared assertions for datasource star contracts.</summary>
public static class StarContractAssertions
{
    public static void AssertResult(Table result, StarContractCase contract)
    {
        var actualColumns = result.Columns.ToArray();

        if (actualColumns.Length != contract.ExpectedColumns.Count)
        {
            throw new InvalidOperationException(
                $"{contract.Signature} returned {actualColumns.Length} columns; " +
                $"expected {contract.ExpectedColumns.Count}. " +
                $"Actual: {Format(actualColumns.Select(column => $"{column.ColumnName}:{column.ColumnType}"))}");
        }

        for (var index = 0; index < actualColumns.Length; index++)
        {
            var expected = contract.ExpectedColumns[index];
            var actual = actualColumns[index];

            if (!string.Equals(expected.Name, actual.ColumnName, StringComparison.Ordinal) ||
                expected.Type != actual.ColumnType)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} result column {index} is " +
                    $"{actual.ColumnName}:{actual.ColumnType}; expected {expected.Name}:{expected.Type}.");
            }

            var expectedResultSourceReadType = expected.EnumOrigin.HasValue
                ? expected.Type
                : expected.SchemaSourceReadType ?? expected.Type;
            if (actual.SourceReadType != expectedResultSourceReadType)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} result column {index} source read type is " +
                    $"{actual.SourceReadType}; expected {expectedResultSourceReadType}.");
            }

            if (expected.EnumOrigin is { } expectedOrigin)
            {
                if (actual.EnumType is null || actual.EnumType.Origin != expectedOrigin)
                {
                    throw new InvalidOperationException(
                        $"{contract.Signature} result column {index} must carry enum metadata from " +
                        $"{expectedOrigin}.");
                }

                AssertEnumDescriptor(actual.EnumType, expected, contract.Signature, "result", index);
            }
            else if (actual.EnumType is not null)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} ordinary result column {index} unexpectedly carries enum metadata.");
            }
        }

        // Restrict runtime inspection to enum-bearing columns. Enumerating an
        // ordinary star result can force intentionally lazy/environment-bound
        // source properties (for example process handles), so those contracts
        // remain metadata-only here. Ordinary runtime leakage is covered by
        // provider-specific scalar fixtures and the repository inventory test.
        var enumIndexes = contract.ExpectedColumns
            .Select((column, index) => (column, index))
            .Where(pair => pair.column.EnumOrigin.HasValue)
            .Select(pair => pair.index)
            .ToArray();
        if (enumIndexes.Length > 0)
        {
            foreach (var row in result.Rows)
            {
                foreach (var index in enumIndexes)
                {
                    if (row[index] is Enum)
                    {
                        throw new InvalidOperationException(
                            $"{contract.Signature} returned System.Enum in result column {index}.");
                    }
                }
            }
        }
    }

    public static void AssertExcludedColumnsRemainInSchema(
        ISchemaTable table,
        StarContractCase contract)
    {
        var schemaColumns = table.Columns.ToDictionary(column => column.ColumnName, StringComparer.Ordinal);

        foreach (var excludedColumn in contract.ExcludedSchemaColumns)
        {
            if (!schemaColumns.ContainsKey(excludedColumn))
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} excluded column '{excludedColumn}' is missing from ISchemaTable.Columns.");
            }
        }

        foreach (var expected in contract.ExpectedColumns)
        {
            if (!schemaColumns.TryGetValue(expected.Name, out var actual))
                continue;

            if (actual.ColumnType != expected.Type)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} schema column '{expected.Name}' carrier type is " +
                    $"{actual.ColumnType}; expected {expected.Type}.");
            }

            var expectedSourceReadType = expected.SchemaSourceReadType ?? expected.Type;
            if (actual.SourceReadType != expectedSourceReadType)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} schema column '{expected.Name}' source read type is " +
                    $"{actual.SourceReadType}; expected {expectedSourceReadType}.");
            }

            if (expected.EnumOrigin is { } expectedOrigin)
            {
                if (actual.EnumType is null || actual.EnumType.Origin != expectedOrigin)
                {
                    throw new InvalidOperationException(
                        $"{contract.Signature} schema column '{expected.Name}' must carry enum metadata from " +
                        $"{expectedOrigin}.");
                }

                AssertEnumDescriptor(actual.EnumType, expected, contract.Signature, "schema", index: -1);
            }
            else if (actual.EnumType is not null)
            {
                throw new InvalidOperationException(
                    $"{contract.Signature} ordinary schema column '{expected.Name}' unexpectedly carries enum metadata.");
            }
        }
    }

    public static void AssertConstructors(
        IEnumerable<SchemaMethodInfo> constructors,
        IReadOnlyCollection<StarContractCase> contracts)
    {
        var actual = constructors
            .Select(constructor => FormatSignature(
                constructor.MethodName,
                constructor.ConstructorInfo.Arguments.Select(argument => argument.Type)))
            .ToArray();

        var expected = contracts
            .Select(contract => FormatSignature(contract.MethodName, contract.ParameterTypes))
            .ToArray();

        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException(
                $"Constructor inventory has {actual.Length} entries; expected {expected.Length}. " +
                $"Actual: {Format(actual)}");
        }

        var actualCounts = actual.GroupBy(signature => signature).ToDictionary(group => group.Key, group => group.Count());
        var expectedCounts = expected.GroupBy(signature => signature).ToDictionary(group => group.Key, group => group.Count());

        if (!actualCounts.OrderBy(pair => pair.Key).SequenceEqual(expectedCounts.OrderBy(pair => pair.Key)))
        {
            throw new InvalidOperationException(
                $"Constructor inventory does not match star contracts. " +
                $"Actual: {Format(actual)}; Expected: {Format(expected)}");
        }
    }

    private static string Format(IEnumerable<string> values) => string.Join(", ", values);

    private static void AssertEnumDescriptor(
        EnumTypeDescriptor descriptor,
        StarContractColumn expected,
        string signature,
        string boundary,
        int index)
    {
        var location = index >= 0 ? $" column {index}" : $" column '{expected.Name}'";
        if (expected.EnumDisplayName is not null &&
            !string.Equals(descriptor.DisplayName, expected.EnumDisplayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{signature} {boundary}{location} enum display name is '{descriptor.DisplayName}'; " +
                $"expected '{expected.EnumDisplayName}'.");
        }

        if (expected.EnumUnderlyingKind is { } underlyingKind && descriptor.UnderlyingKind != underlyingKind)
        {
            throw new InvalidOperationException(
                $"{signature} {boundary}{location} enum backing is '{descriptor.UnderlyingKind}'; " +
                $"expected '{underlyingKind}'.");
        }

        if (expected.EnumIsFlags is { } isFlags && descriptor.IsFlags != isFlags)
        {
            throw new InvalidOperationException(
                $"{signature} {boundary}{location} enum flags marker is '{descriptor.IsFlags}'; " +
                $"expected '{isFlags}'.");
        }

        if (descriptor.Members.Count == 0 || string.IsNullOrWhiteSpace(descriptor.Fingerprint))
        {
            throw new InvalidOperationException(
                $"{signature} {boundary}{location} enum descriptor is missing members or fingerprint.");
        }
    }

    private static string FormatSignature(string methodName, IEnumerable<Type> parameterTypes) =>
        $"{methodName}({string.Join(", ", parameterTypes.Select(type => type.FullName ?? type.Name))})";
}
