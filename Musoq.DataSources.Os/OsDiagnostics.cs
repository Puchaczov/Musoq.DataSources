#nullable enable

using System;
using MetadataExtractor;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Os;

internal static class OsDiagnostics
{
    internal static DataSourceException? Map(Exception error)
    {
        var problem = error switch
        {
            ImageProcessingException => ("InvalidMetadata", "The file metadata format could not be read.", "Use a supported metadata file; set throwOnMetadataReadError to false only when skipped files are acceptable."),
            BadImageFormatException => ("UnsupportedAssembly", "The file is not a loadable managed assembly.", "Select a compatible managed assembly for DLL inspection."),
            _ => default
        };
        if (problem.Item1 is null) return null;
        return new DataSourceException(new DataSourceDiagnostic("OS." + problem.Item1, "file-format", problem.Item2,
            new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.InvalidInput,
                SuggestedFixes = [problem.Item3]
            }), error);
    }
}
