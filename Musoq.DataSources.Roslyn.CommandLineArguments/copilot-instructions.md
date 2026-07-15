# Roslyn.CommandLineArguments guide

## Purpose
- Parameterless `Musoq.CommandLine` module for Roslyn command-driven solution-bucket flows.
- This project translates CLI input into transport-friendly command payloads.

## Read first
- `RoslynCommandLineModule.cs`
- `Dtos/`

## Patterns to preserve
- Keep this project transport-focused, not analysis-focused.
- `RoslynCommandLineModule.Configure(...)` defines the public command tree; command names and parameters are part of the contract.
- The host supplies `musoq.datasource.http-request.v1` as a typed invocation item only after validation.
- Each command should translate settings into HTTP or DTO calls, not embed Roslyn analysis logic.

## Integrations
- Exact `Musoq.CommandLine` package version 0.0.1, with runtime assets excluded because the host owns the ABI.
- BCL HTTP and JSON types only.
- Keep the module limited to BCL types and the published `Musoq.CommandLine` contract; host implementation services and presentation libraries stay outside this project.

## Validate with
- Run `Musoq.DataSources.Roslyn.CommandLineArguments.Tests`.
- Cross-check `Musoq.DataSources.Roslyn/LifecycleHooks.cs` and `Musoq.DataSources.Roslyn/CliCommands/SolutionOperationsCommand.cs` when request payloads change.
