namespace Musoq.DataSources.Roslyn.Dtos;

/// <summary>
/// Represents a request to load solution
/// </summary>
public class CSharpSolutionRequestDto
{
    /// <summary>
    /// Solution file path
    /// </summary>
    public string? SolutionFilePath { get; set; }
}