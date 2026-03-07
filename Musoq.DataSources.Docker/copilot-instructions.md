# Docker plugin guide

## Purpose
- Exposes local Docker resources as `containers`, `images`, `networks`, and `volumes`.

## Read first
- `DockerSchema.cs`
- `DockerApi.cs`
- `IDockerApi.cs`
- `Containers/`
- `Images/`
- `Networks/`
- `Volumes/`

## Patterns to preserve
- Follow the folder-per-resource pattern when adding support for a new Docker surface.
- Keep Docker SDK calls behind `IDockerApi`; row shaping belongs in resource-specific source/table/entity types.
- The schema is static, so changes are usually additive and explicit.
- Default connection behavior comes from local Docker client configuration; do not hardcode machine-specific assumptions.

## Integrations
- `Docker.DotNet`

## Validate with
- `Musoq.DataSources.Docker.Tests/DockerTests.cs`
- `Musoq.DataSources.Docker.Tests/DockerSchemaDescribeTests.cs`
- `Musoq.DataSources.Docker.Tests/DockerPlaygroundTests.cs`