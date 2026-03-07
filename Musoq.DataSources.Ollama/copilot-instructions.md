# Ollama plugin guide

## Purpose
- Exposes a single-row Ollama-backed LLM source plus SQL-callable helper methods for text and image tasks.

## Read first
- `OllamaSchema.cs`
- `OllamaLibrary.cs`
- `OllamaSingleRowSource.cs`
- `OllamaApi.cs`
- `IOllamaApi.cs`
- `OllamaSchemaHelper.cs`

## Patterns to preserve
- Keep schema/source behavior separate from helper-method behavior.
- The production source surface is `#ollama.llm(...)`; tests may use custom schemas for convenience, but production naming is the contract.
- Token counting, retries, and response-shape handling are easy places to introduce regressions.
- Keep provider-specific logic behind `IOllamaApi` and preserve current helper method names.

## Integrations
- `OllamaSharp`
- `Polly`
- `SharpToken`
- Optional environment variable: `OLLAMA_BASE_URL`

## Validate with
- `Musoq.DataSources.Ollama.Tests/OllamaQueryTests.cs`
- `Musoq.DataSources.Ollama.Tests/OllamaSingleRowSourceTests.cs`