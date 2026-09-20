#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.Evaluator.Tables;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

/// <summary>
///     Assertions shared by datasource tests which expose a native enum as a
///     primitive carrier plus portable metadata.
/// </summary>
public static class PortableEnumAssertions
{
    public static void AssertEnumColumn(
        Table table,
        string columnName,
        Type carrierType,
        Type sourceReadType,
        Type enumType,
        EnumUnderlyingKind underlyingKind,
        bool isFlags,
        IEnumerable<string>? requiredMemberNames = null)
    {
        var column = table.Columns.SingleOrDefault(candidate =>
            string.Equals(candidate.ColumnName, columnName, StringComparison.Ordinal));
        if (column is null)
            throw new InvalidOperationException($"Enum column '{columnName}' is missing from the result.");

        if (column.ColumnType != carrierType)
            throw new InvalidOperationException(
                $"Enum column '{columnName}' carrier is {column.ColumnType}; expected {carrierType}.");
        if (column.SourceReadType != sourceReadType)
            throw new InvalidOperationException(
                $"Enum column '{columnName}' source-read type is {column.SourceReadType}; expected {sourceReadType}.");

        var descriptor = column.EnumType;
        if (descriptor is null)
            throw new InvalidOperationException($"Enum column '{columnName}' has no portable descriptor.");
        if (descriptor.Origin != EnumTypeOrigin.NativeClr)
            throw new InvalidOperationException(
                $"Enum column '{columnName}' origin is {descriptor.Origin}; expected NativeClr.");
        if (!string.Equals(descriptor.DisplayName, enumType.FullName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Enum column '{columnName}' display name is '{descriptor.DisplayName}'; " +
                $"expected '{enumType.FullName}'.");
        if (descriptor.UnderlyingKind != underlyingKind || descriptor.IsFlags != isFlags)
            throw new InvalidOperationException(
                $"Enum column '{columnName}' descriptor backing/flags are " +
                $"{descriptor.UnderlyingKind}/{descriptor.IsFlags}; expected {underlyingKind}/{isFlags}.");
        if (string.IsNullOrWhiteSpace(descriptor.Fingerprint) || descriptor.Members.Count == 0)
            throw new InvalidOperationException($"Enum column '{columnName}' descriptor is incomplete.");

        if (requiredMemberNames is not null)
        {
            var names = descriptor.Members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var requiredName in requiredMemberNames)
            {
                if (!names.Contains(requiredName))
                    throw new InvalidOperationException(
                        $"Enum column '{columnName}' does not declare member '{requiredName}'.");
            }
        }
    }

    public static void AssertNoClrEnumValues(Table table)
    {
        foreach (var row in table.Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                if (row[index] is Enum)
                {
                    throw new InvalidOperationException(
                        $"Result column {index} contains a CLR enum value '{row[index].GetType()}'.");
                }
            }
        }
    }
}
