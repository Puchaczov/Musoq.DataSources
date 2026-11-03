#nullable enable

using System;
using Musoq.Schema.Diagnostics;
using SharpCompress.Common;

namespace Musoq.DataSources.Archives;

internal static class ArchivesDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error is InvalidFormatException or IncompleteArchiveException
            ? new DataSourceException(new DataSourceDiagnostic("ARCHIVES.InvalidArchive", "format",
                "The archive format is unsupported, invalid, or incomplete.", new DataSourceDiagnosticOptions
                {
                    FailureKind = DataSourceFailureKind.InvalidInput,
                    SuggestedFixes = ["Supply a complete archive in a format supported by SharpCompress."]
                }), error)
            : null;
}
