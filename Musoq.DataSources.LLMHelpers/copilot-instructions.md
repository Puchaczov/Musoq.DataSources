# LLMHelpers guide

## Purpose
- Small contract library that standardizes SQL-callable LLM helper methods across providers.
- The key file is `ILargeLanguageModelFunctions.cs`.

## Read first
- `ILargeLanguageModelFunctions.cs`

## Patterns to preserve
- Keep this project provider-agnostic.
- Method names and overload shapes are part of the Musoq-facing contract and should stay aligned between `OpenAI` and `Ollama` implementations.
- Do not move provider-specific networking, retries, or SDK code into this project.

## Integrations
- This helper project itself is lightweight, but it is implemented by `Musoq.DataSources.OpenAI/OpenAiLibrary.cs` and `Musoq.DataSources.Ollama/OllamaLibrary.cs`.
- Shared surfaces include `Sentiment(...)`, `SummarizeContent(...)`, `TranslateContent(...)`, `Entities(...)`, `LlmPerform(...)`, image methods, and token counting.

## Validate with
- `Musoq.DataSources.OpenAI.Tests/OpenAiQueryTests.cs`
- `Musoq.DataSources.Ollama.Tests/OllamaQueryTests.cs`