#nullable enable
using System;
using Musoq.DataSources.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Ollama;

internal static class OllamaDiagnostics
{
    internal static DataSourceException InvalidOutput(Exception? privateError = null)
        => new(new DataSourceDiagnostic("OLLAMA.InvalidModelOutput", "response",
            "The model response does not match the requested structured output.", new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.ProviderFailure,
                SuggestedFixes = ["Adjust the prompt or model to return the required output structure; enable strict output handling when a fallback is unacceptable."]
            }), privateError);

    internal static void OutputFallback(OllamaEntityBase entity, Exception? privateError = null)
        => entity.WarningReporter?.Invoke(ProviderDiagnostics.AsWarning(InvalidOutput(privateError).Report,
            "The explicit tolerant-output fallback was used."));

    internal static DataSourceException? Map(Exception error)
        => error is OllamaSharp.Models.Exceptions.ModelDoesNotSupportToolsException
            ? OllamaSchema.Reports.Failure(DataSourceFailureKind.InvalidInput, error) : null;
}
