#nullable enable
using System;
using Musoq.DataSources.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.OpenAI;

internal static class OpenAiDiagnostics
{
    internal static DataSourceException InvalidOutput(Exception? privateError = null)
        => new(new DataSourceDiagnostic("OPENAI.InvalidModelOutput", "response",
            "The model response does not match the requested structured output.", new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.ProviderFailure,
                SuggestedFixes = ["Adjust the prompt or model to return the required output structure; enable strict output handling when a fallback is unacceptable."]
            }), privateError);

    internal static void OutputFallback(OpenAiEntityBase entity, Exception? privateError = null)
        => entity.WarningReporter?.Invoke(ProviderDiagnostics.AsWarning(InvalidOutput(privateError).Report,
            "The explicit tolerant-output fallback was used."));

    internal static DataSourceException? Map(Exception error)
        => error is System.ClientModel.ClientResultException api && ProviderDiagnostics.HttpFailureKind((System.Net.HttpStatusCode)api.Status) is { } kind
            ? OpenAiSchema.Reports.Failure(kind, error) : null;
}
