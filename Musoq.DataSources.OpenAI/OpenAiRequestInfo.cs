namespace Musoq.DataSources.OpenAI;

internal class OpenAiRequestInfo
{
    public string Model { get; init; } = string.Empty;
    
    public double FrequencyPenalty { get; init; } = 0.0;
    
    public int MaxTokens { get; init; } = 4000;
    
    public double PresencePenalty { get; init; } = 0.0;
    
    public double Temperature { get; init; } = 0.0;
}