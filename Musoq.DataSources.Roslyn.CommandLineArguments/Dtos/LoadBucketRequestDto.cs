namespace Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

public class LoadBucketRequestDto
{
    public required string SchemaName { get; init; }
    
    public required string?[] Arguments { get; init; } = [];
}
