#nullable enable
using System;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Roslyn;

internal static class RoslynDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error is System.Xml.XmlException or System.Xml.XPath.XPathException or System.Text.Json.JsonException or Newtonsoft.Json.JsonException
            ? new DataSourceException(new DataSourceDiagnostic("ROSLYN.InvalidPackageMetadata", "metadata",
                "Package metadata could not be parsed in the expected format.", new DataSourceDiagnosticOptions
                {
                    FailureKind = DataSourceFailureKind.InvalidInput,
                    SuggestedFixes = ["Check package metadata and the configured metadata service response format."]
                }), error) : null;
}
