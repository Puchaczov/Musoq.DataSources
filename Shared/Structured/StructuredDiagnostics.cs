#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Structured;

internal static class StructuredDiagnostics
{
    internal static DataSourceException? Map(string provider, Exception error)
    {
        var problem = error switch
        {
            StructuredUnknownColumnException => ("UnknownColumn", "The requested column is not present in the structured source.", "Select columns from the discovered schema or correct the TABLE contract."),
            StructuredSchemaDriftException => ("SchemaChanged", "The structured input schema changed after query binding.", "Recompile the query after changing the input schema or correct the TABLE contract."),
            StructuredSourceChangedException => ("SourceChanged", "The structured input changed while it was being read.", "Keep the input unchanged during execution and recompile after updating it."),
            StructuredDuplicateFieldException => ("DuplicateField", "The structured input contains a duplicate field.", "Use unique field names within each input record."),
            _ => default
        };
        if (problem.Item1 is null) return null;
        var details = new Dictionary<string, string>();
        if (error is StructuredDuplicateFieldException duplicate)
            details["rowIndex"] = duplicate.RowIndex.ToString(CultureInfo.InvariantCulture);
        return new DataSourceException(new DataSourceDiagnostic(provider + "." + problem.Item1, "structured-input", problem.Item2,
            new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.InvalidInput,
                SuggestedFixes = [problem.Item3],
                Details = details,
                CanRetry = error is StructuredSourceChangedException
            }), error);
    }
}
