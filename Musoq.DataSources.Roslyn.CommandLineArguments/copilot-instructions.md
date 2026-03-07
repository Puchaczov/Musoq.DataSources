# Roslyn.CommandLineArguments guide

## Purpose
- Shared CLI command, DTO, and settings package for Roslyn command-driven solution-bucket flows.
- This project translates CLI input into transport-friendly command payloads.

## Read first
- `SpectreArguments.cs`
- `Commands/`
- `Settings/`
- `Dtos/`

## Patterns to preserve
- Keep this project transport-focused, not analysis-focused.
- `SpectreArguments.ConfigureCommands(...)` defines the public command tree; command names and parameters are part of the contract.
- `CliCommandBase<T>` and `CommandContext.Data` are the current path for passing runtime services into commands.
- Each command should translate settings into HTTP or DTO calls, not embed Roslyn analysis logic.

## Integrations
- `Spectre.Console.Cli`
- `System.Net.Http.Json`
- Main consumer is the `Musoq.DataSources.Roslyn` project, especially its lifecycle and CLI bootstrap code.

## Validate with
- Cross-check `Musoq.DataSources.Roslyn/LifecycleHooks.cs`
- Cross-check `Musoq.DataSources.Roslyn/CliCommands/SolutionOperationsCommand.cs`