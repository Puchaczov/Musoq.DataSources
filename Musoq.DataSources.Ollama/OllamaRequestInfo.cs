namespace Musoq.DataSources.Ollama;

internal class OllamaRequestInfo
{
    public string Model { get; init; } = string.Empty;
    
    public string OllamaBaseUrl { get; init; } = string.Empty;
    
    public float Temperature { get; init; }
}