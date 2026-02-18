namespace Musoq.DataSources.OpenAI;

internal class OpenAiRequestInfo
{
    public string Model { get; init; } = string.Empty;

    public float FrequencyPenalty { get; init; } = 0;

    public int MaxTokens { get; init; } = 4000;

    public float PresencePenalty { get; init; } = 0;

    public float Temperature { get; init; } = 0;
}