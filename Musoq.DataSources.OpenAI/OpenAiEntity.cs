namespace Musoq.DataSources.OpenAI;

public class OpenAiEntity
{
    public OpenAiEntity(IOpenAiApi api, string? model, double frequencyPenalty, int maxTokens, double presencePenalty, double temperature)
    {
        Api = api;
        Model = model;
        FrequencyPenalty = frequencyPenalty;
        MaxTokens = maxTokens;
        PresencePenalty = presencePenalty;
        Temperature = temperature;
    }

    public IOpenAiApi Api { get; }
    
    public string? Model { get; }
    
    public double FrequencyPenalty { get; }
    
    public int MaxTokens { get; }
    
    public double PresencePenalty { get; }
    
    public double Temperature { get; }
}