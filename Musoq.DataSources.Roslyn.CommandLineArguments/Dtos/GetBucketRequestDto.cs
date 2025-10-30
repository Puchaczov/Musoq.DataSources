namespace Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

public class GetBucketRequestDto
{
    public required string SchemaName { get; init; }
    
    public required string?[] Arguments { get; init; } = [];
}
