# Datasource beta release train

Requires Musoq Core **17.0.16-beta.1** and .NET 10.

## Changelog

- Align Converter, Evaluator, Parser, Plugins, and Schema dependencies at `17.0.16-beta.1`.
- Promote all 16 registered datasource packages to `beta.1`, preserving their numeric versions.
- Retain datasource diagnostics, execution-local warnings, and safe provider failure handling.
- Include the artifact-path and license-gatherer updates already present in the repository.

Public datasource signatures and constructor metadata remain unchanged. Upgrade the
host Core before loading these packages and recompile persisted queries after updating.
Hosts can collect runtime warnings with `TypedQueryRunOptions.DataSourceWarnings`.

## Versions

| Datasource | Version |
| --- | --- |
| Archives | `6.0.11-beta.1` |
| CANBus | `4.0.10-beta.1` |
| Docker | `5.0.10-beta.1` |
| FlatFile | `8.0.10-beta.1` |
| Git | `4.0.3-beta.1` |
| GitHub | `2.0.10-beta.1` |
| Jira | `2.0.10-beta.1` |
| Json | `10.0.10-beta.1` |
| Ollama | `5.0.10-beta.1` |
| OpenAI | `6.0.10-beta.1` |
| Os | `9.0.10-beta.1` |
| Roslyn | `3.0.11-beta.1` |
| Search | `1.0.7-beta.1` |
| SeparatedValues | `11.0.7-beta.1` |
| System | `8.0.10-beta.1` |
| Time | `8.0.10-beta.1` |

Production helper versions remain unchanged. Helpers are not part of the registered
datasource publication train.
