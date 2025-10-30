# Migration from Cocona to Spectre.Console.Cli

This document describes the migration from Cocona to Spectre.Console.Cli framework.

## Changes Made

### 1. Package Reference
- **Removed**: `Cocona` version 2.2.0
- **Added**: `Spectre.Console.Cli` version 0.49.1

### 2. Architecture Changes

The migration introduces a cleaner separation of concerns with the following structure:

#### Settings Classes (`Settings/`)
- `LoadSolutionSettings.cs` - Settings for loading a solution
- `UnloadSolutionSettings.cs` - Settings for unloading a solution
- `CacheBucketSettings.cs` - Settings for cache operations with bucket and optional cache path
- `BucketSettings.cs` - Settings for operations requiring only bucket identifier
- `SetResolveValueStrategySettings.cs` - Settings for setting resolve value strategy

#### Command Classes (`Commands/`)
- `LoadSolutionCommand.cs` - Command to load a solution
- `UnloadSolutionCommand.cs` - Command to unload a solution
- `ClearCacheCommand.cs` - Command to clear cache
- `GetCacheCommand.cs` - Command to get cache directory path
- `SetCacheCommand.cs` - Command to set cache directory path
- `GetResolveValueStrategyCommand.cs` - Command to get resolve value strategy
- `SetResolveValueStrategyCommand.cs` - Command to set resolve value strategy

#### Core Files
- `SpectreArguments.cs` - Main configuration file (replaces `CoconaArguments.cs`)
- `TypeRegistrar.cs` - Dependency injection container for Spectre.Console.Cli

### 3. API Changes

#### Before (Cocona):
```csharp
CoconaArguments.SetupArguments(builder, invokeAsync);
```

#### After (Spectre.Console.Cli):
```csharp
var app = new CommandApp(new TypeRegistrar());
app.Configure(config => 
{
    SpectreArguments.ConfigureCommands(config, invokeAsync);
});
await app.RunAsync(args);
```

### 4. Command Structure

The command hierarchy remains the same:
```
csharp
└── solution
    ├── load <path> <bucket> [--cache-directory-path]
    ├── unload <path> <bucket>
    ├── cache
    │   ├── clear <bucket> [--cache-directory-path]
    │   ├── get <bucket>
    │   └── set <bucket> [--cache-directory-path]
    └── resolve-value-strategy
        ├── get <bucket>
        └── set <bucket> [--value]
```

## Benefits of the Migration

1. **Better Separation of Concerns**: Settings and commands are now separate classes
2. **Stronger Typing**: Each command has its own strongly-typed settings class
3. **Easier Testing**: Commands can be unit tested independently
4. **Rich Console Experience**: Spectre.Console.Cli provides better console formatting and help text
5. **Industry Standard**: Spectre.Console is widely adopted in the .NET community

## Usage Example

```csharp
using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments;

var registrar = new TypeRegistrar();
registrar.RegisterLazy(typeof(Func<string, string?[], Task<int>>), () => YourInvokeAsyncMethod);

var app = new CommandApp(registrar);
app.Configure(config =>
{
    SpectreArguments.ConfigureCommands(config, YourInvokeAsyncMethod);
});

return await app.RunAsync(args);
```

## Backward Compatibility

The command-line interface remains the same. All existing command invocations will work without modification.
