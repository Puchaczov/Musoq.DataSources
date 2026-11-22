# Stable datasource patch releases for Musoq Core 17.0.22

Requires Musoq Core **17.0.22** and .NET 10. Upgrade the host Core packages
together before installing the new datasource versions.

- Align Converter, Evaluator, Parser, Plugins, and Schema at 17.0.22.
- Search returns complete lines from logs that remain open for append
  and defers an incomplete trailing line until a later read.
- Each of the 16 registered datasources is available as a NuGet package
  and as runtime plugin archives for four platforms.

## Versions

| Datasource | Version |
| --- | --- |
| Archives | 6.0.12 |
| CANBus | 4.0.11 |
| Docker | 5.0.11 |
| FlatFile | 8.0.11 |
| Git | 4.0.4 |
| GitHub | 2.0.11 |
| Jira | 2.0.11 |
| Json | 10.0.11 |
| Ollama | 5.0.11 |
| OpenAI | 6.0.11 |
| Os | 9.0.11 |
| Roslyn | 3.0.12 |
| Search | 1.0.8 |
| SeparatedValues | 11.0.8 |
| System | 8.0.11 |
| Time | 8.0.11 |
