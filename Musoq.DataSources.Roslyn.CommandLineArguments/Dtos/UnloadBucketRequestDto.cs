namespace Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

public class UnloadBucketRequestDto
{
    public required string SchemaName { get; init; }

    public required string?[] Arguments { get; init; } = [];
}