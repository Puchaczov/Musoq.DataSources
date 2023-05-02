namespace Musoq.DataSources.OpenAI;

/// <summary>
/// Represents an OpenAI entity with various configuration properties.
/// </summary>
public class OpenAiEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiEntity"/> class with the specified parameters.
    /// </summary>
    /// <param name="api">The IOpenAiApi object used to interact with the OpenAI API.</param>
    /// <param name="model">The optional model name to use for generating text.</param>
    /// <param name="frequencyPenalty">The frequency penalty to control the text generation's repetition rate.</param>
    /// <param name="maxTokens">The maximum number of tokens the generated text should have.</param>
    /// <param name="presencePenalty">The presence penalty to control the likelihood of generating tokens present in the input.</param>
    /// <param name="temperature">The temperature to control the randomness of the generated text.</param>
    public OpenAiEntity(IOpenAiApi api, string? model, double frequencyPenalty, int maxTokens, double presencePenalty, double temperature)
    {
        Api = api;
        Model = model;
        FrequencyPenalty = frequencyPenalty;
        MaxTokens = maxTokens;
        PresencePenalty = presencePenalty;
        Temperature = temperature;
    }

    /// <summary>
    /// Gets the IOpenAiApi object used to interact with the OpenAI API.
    /// </summary>
    public IOpenAiApi Api { get; }
    
    /// <summary>
    /// Gets the optional model name to use for generating text.
    /// </summary>
    public string? Model { get; }
    
    /// <summary>
    /// Gets the frequency penalty to control the text generation's repetition rate.
    /// </summary>
    public double FrequencyPenalty { get; }
    
    /// <summary>
    /// Gets the maximum number of tokens the generated text should have.
    /// </summary>
    public int MaxTokens { get; }
    
    /// <summary>
    /// Gets the presence penalty to control the likelihood of generating tokens present in the input.
    /// </summary>
    public double PresencePenalty { get; }
    
    /// <summary>
    /// Gets the temperature to control the randomness of the generated text.
    /// </summary>
    public double Temperature { get; }
}