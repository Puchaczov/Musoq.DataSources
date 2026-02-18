namespace Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

public class SetBucketRequestDto
{
    public required string SchemaName { get; init; }

    public required string?[] Arguments { get; init; } = [];
}