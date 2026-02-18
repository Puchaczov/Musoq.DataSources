namespace Musoq.DataSources.OpenAI;

/// <summary>
///     Represents an OpenAI entity with various configuration properties.
/// </summary>
public abstract class OpenAiEntityBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenAiEntityBase" /> class with the specified parameters.
    /// </summary>
    /// <param name="api">The Api</param>
    /// <param name="model">The model</param>
    /// <param name="frequencyPenalty">The frequency penalty</param>
    /// <param name="maxTokens">The maximum number of tokens</param>
    /// <param name="presencePenalty">The presence penalty</param>
    /// <param name="temperature">The temperature</param>
    /// <param name="cancellationToken">The cancellation token</param>
    protected OpenAiEntityBase(IOpenAiApi api, string? model, float frequencyPenalty, int maxTokens,
        float presencePenalty, float temperature, CancellationToken cancellationToken)
    {
        Api = api;
        Model = model;
        FrequencyPenalty = frequencyPenalty;
        MaxTokens = maxTokens;
        PresencePenalty = presencePenalty;
        Temperature = temperature;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Gets the IOpenAiApi object used to interact with the OpenAI API.
    /// </summary>
    public IOpenAiApi Api { get; }

    /// <summary>
    ///     Gets the optional model name to use for generating text.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    ///     Gets the frequency penalty to control the text generation's repetition rate.
    /// </summary>
    public float FrequencyPenalty { get; }

    /// <summary>
    ///     Gets the maximum number of tokens the generated text should have.
    /// </summary>
    public int MaxTokens { get; }

    /// <summary>
    ///     Gets the presence penalty to control the likelihood of generating tokens present in the input.
    /// </summary>
    public float PresencePenalty { get; }

    /// <summary>
    ///     Gets the temperature to control the randomness of the generated text.
    /// </summary>
    public float Temperature { get; }

    /// <summary>
    ///     Gets the cancellation token used to cancel the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}