#nullable enable

using System;
using System.IO;

namespace Musoq.DataSources.Structured;

internal sealed class StructuredUnknownColumnException(string columnName, string path)
    : InvalidOperationException($"Column '{columnName}' does not exist in structured source '{path}'.")
{
    public string ColumnName { get; } = columnName;

    public string Path { get; } = path;
}

internal sealed class StructuredSchemaDriftException(string path, string difference)
    : InvalidOperationException($"Structured source '{path}' changed incompatibly after binding: {difference}")
{
    public string Path { get; } = path;

    public string Difference { get; } = difference;
}

internal sealed class StructuredSourceChangedException(string path)
    : IOException($"Structured source '{path}' changed while its identity was being captured.")
{
    public string Path { get; } = path;
}

internal sealed class StructuredDuplicateFieldException(string fieldName, long rowIndex)
    : FormatException($"Structured field '{fieldName}' occurs more than once in row {rowIndex}.")
{
    public string FieldName { get; } = fieldName;

    public long RowIndex { get; } = rowIndex;
}
