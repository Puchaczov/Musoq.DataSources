# Stable datasource release train

Requires Musoq Core **17.0.18** and .NET 10.

## Changelog

- Align Converter, Evaluator, Parser, Plugins, and Schema dependencies at `17.0.18`.
- Promote all 16 registered datasource packages to stable versions, preserving their numeric versions.
- Add opt-in typed computed projections to JSON and SeparatedValues, with validated accepted/residual partitions and provider-only input columns omitted from output row carriers.
- Preserve engine fallback for unsupported expressions and conditional query regions.
- Consume the Core 17.0.18 correction that preserves enum descriptors in ordered and window results.
- Bundle private helper assemblies in datasource NuGet packages, removing unresolved dependencies on unpublished AsyncRowsSource and LLMHelpers packages.
- Retain datasource diagnostics, execution-local warnings, and safe provider failure handling.
- Include the artifact-path and license-gatherer updates already present in the repository.

Public datasource signatures and constructor metadata remain unchanged. Upgrade the
host Core before loading these packages and recompile persisted queries after updating.
Hosts can collect runtime warnings with `TypedQueryRunOptions.DataSourceWarnings`.

## Versions

| Datasource | Version |
| --- | --- |
| Archives | `6.0.11` |
| CANBus | `4.0.10` |
| Docker | `5.0.10` |
| FlatFile | `8.0.10` |
| Git | `4.0.3` |
| GitHub | `2.0.10` |
| Jira | `2.0.10` |
| Json | `10.0.10` |
| Ollama | `5.0.10` |
| OpenAI | `6.0.10` |
| Os | `9.0.10` |
| Roslyn | `3.0.11` |
| Search | `1.0.7` |
| SeparatedValues | `11.0.7` |
| System | `8.0.10` |
| Time | `8.0.10` |

Production helpers AsyncRowsSource (`3.0.4`) and Roslyn.CommandLineArguments (`1.0.4`)
are stable. Helpers are not part of the registered datasource publication train.
