# OpenAI plugin guide

## Purpose
- Exposes a single-row OpenAI-backed source plus SQL-callable helper methods for text, token counting, and image prompts.

## Read first
- `OpenAiSchema.cs`
- `OpenAiLibrary.cs`
- `OpenAiSingleRowSource.cs`
- `OpenAiApi.cs`
- `IOpenAiApi.cs`
- `OpenAiSchemaHelper.cs`
- `Defaults.cs`

## Data flow
- `OpenAiSchema.cs` exposes a single source family: `#openai.gpt(...)`.
- `OpenAiSingleRowSource.cs` yields exactly one `OpenAiEntity` that carries model and request settings.
- `OpenAiLibrary.cs` is where queries actually become useful: helper methods are `[BindableMethod]` functions that inject `OpenAiEntity` and call `IOpenAiApi`.
- The row source itself has no meaningful schema columns today because `OpenAiSchemaHelper.cs` is effectively empty; user-visible output comes from helper methods, not table fields.

## Overloads and defaults
- `#openai.gpt(...)` uses progressive overloads to add `model`, `maxTokens`, `temperature`, and penalty settings.
- Default model comes from `Defaults.cs` and is part of the runtime contract.
- Default request tuning also lives in the schema layer through `OpenAiRequestInfo` construction.
- The runtime/described overload surface must stay aligned with describe tests; treat overload count changes as public-surface changes.

## Patterns to preserve
- The source surface is `#openai.gpt(...)` with several overloads that progressively add model and tuning parameters.
- Keep provider-specific API logic behind `IOpenAiApi` and shared query-facing method names aligned with `LLMHelpers`.
- Default model behavior from `Defaults.cs` is part of the runtime contract.
- Preserve current request/response mapping and token-count assumptions used by library methods.

## API and helper boundaries
- Keep all OpenAI SDK usage inside `OpenAiApi.cs`.
- `OpenAiLibrary.cs` should depend on `IOpenAiApi`, not on concrete SDK types.
- Shared method names and semantics should stay compatible with `LLMHelpers` where applicable.
- If actual row fields are ever exposed, update `OpenAiSchemaHelper.cs`, `OpenAiSingleRowTable.cs`, and schema docs together.

## Integrations
- `OpenAI` SDK
- `SharpToken`
- Required environment variable: `OPENAI_API_KEY`

## Token and image specifics
- Token counting uses `SharpToken`; overloads differ between model-aware counting and explicit encoding-name counting.
- Image helpers accept raw base64 or `data:` URLs and infer media type through `Base64MediaTypeDetector.cs`.
- Unknown media types currently throw rather than silently guessing.
- `OpenAiApi.cs` currently reads only the first returned text item and does not implement streaming or richer response handling.

## Common pitfalls
- `OPENAI_API_KEY` is read directly from runtime environment variables and missing config fails early.
- Many library calls intentionally convert exceptions into fallback text results through asynchronous wrapper helpers; do not accidentally turn those into hard failures.
- `Entities()` and some image helper methods are intentionally tolerant of malformed model output unless explicit throwing is requested.
- XML docs and `CreateGptMethodInfos()` can drift if overloads are changed in only one place.
- Because the source has no useful schema columns today, adding row fields is a broader contract change than it first appears.

## Safe extension points
- Add new SQL-callable functions in `OpenAiLibrary.cs` first.
- Add request-tuning fields through `OpenAiRequestInfo.cs` plus `OpenAiSchema.cs` together.
- Change provider behavior in `OpenAiApi.cs` without breaking the `IOpenAiApi` seam used by tests.
- Preserve method naming/semantics shared with `LLMHelpers` unless the user-visible surface is intentionally changing.

## Validate with
- `Musoq.DataSources.OpenAI.Tests/OpenAiQueryTests.cs`
- `Musoq.DataSources.OpenAI.Tests/OpenAiSingleRowSourceTests.cs`
- `Musoq.DataSources.OpenAI.Tests/OpenAISchemaDescribeTests.cs`

## Most representative tests
- `OpenAiQueryTests.cs` is the main end-to-end contract for helper outputs, image methods, and token counting.
- `OpenAISchemaDescribeTests.cs` is the authority for `desc #openai` / `desc #openai.gpt` output and overload inventory.
- `OpenAiSingleRowSourceTests.cs` is the best focused unit coverage for fallback semantics and single-row behavior.
- `OpenAiApiPlayground.cs` is exploratory and should not be treated as the formal contract.