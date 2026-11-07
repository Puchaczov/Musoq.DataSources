# Datasource diagnostics integration alpha release train

Requires Musoq Core **17.0.16-alpha.1** and .NET 10.

## Changelog

- Integrate the previously published diagnostics implementation into the default branch
  and advance all registered datasource packages to fresh patch alpha versions.
- Recognized datasource failures expose safe provider codes, categories, input locations,
  suggested fixes and retry guidance through Core diagnostics.
- Supported nonfatal fallbacks report execution-local warnings alongside query results.
- Filesystem arguments reject control characters and platform-invalid path characters;
  paths are never repaired. Specific malformed-input diagnostics remain intact.
- SDK response bodies, credentials and arbitrary exception text stay private. LLM
  completion fallbacks return controlled public text. Caller cancellation propagates.

Public datasource signatures and constructor metadata remain unchanged. Upgrade the
host Core before loading these packages and recompile persisted queries after updating.
Hosts can collect runtime warnings with `TypedQueryRunOptions.DataSourceWarnings`.
Cloud presentation remains separate follow-up work.

## Versions

| Datasource | Version |
| --- | --- |
| Archives | `6.0.11-alpha.1` |
| CANBus | `4.0.10-alpha.1` |
| Docker | `5.0.10-alpha.1` |
| FlatFile | `8.0.10-alpha.1` |
| Git | `4.0.3-alpha.1` |
| GitHub | `2.0.10-alpha.1` |
| Jira | `2.0.10-alpha.1` |
| Json | `10.0.10-alpha.1` |
| Ollama | `5.0.10-alpha.1` |
| OpenAI | `6.0.10-alpha.1` |
| Os | `9.0.10-alpha.1` |
| Roslyn | `3.0.11-alpha.1` |
| Search | `1.0.7-alpha.1` |
| SeparatedValues | `11.0.7-alpha.1` |
| System | `8.0.10-alpha.1` |
| Time | `8.0.10-alpha.1` |

Production helper versions also advance to AsyncRowsSource `3.0.4-alpha.1` and
Roslyn.CommandLineArguments `1.0.4-alpha.1`; neither helper is part of the registered
datasource publication train.
