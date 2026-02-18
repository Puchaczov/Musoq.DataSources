namespace Musoq.DataSources.OpenAI;

/// <summary>
///     Represents an OpenAI entity with various configuration properties.
/// </summary>
public class OpenAiEntity : OpenAiEntityBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenAiEntity" /> class with the specified parameters.
    /// </summary>
    /// <param name="api">The IOpenAiApi object used to interact with the OpenAI API.</param>
    /// <param name="model">The optional model name to use for generating text.</param>
    /// <param name="frequencyPenalty">The frequency penalty to control the text generation's repetition rate.</param>
    /// <param name="maxTokens">The maximum number of tokens the generated text should have.</param>
    /// <param name="presencePenalty">The presence penalty to control the likelihood of generating tokens present in the input.</param>
    /// <param name="temperature">The temperature to control the randomness of the generated text.</param>
    /// <param name="cancellationToken">The cancellation token used to cancel the text generation process.</param>
    public OpenAiEntity(IOpenAiApi api, string? model, float frequencyPenalty, int maxTokens, float presencePenalty,
        float temperature, CancellationToken cancellationToken)
        : base(api, model, frequencyPenalty, maxTokens, presencePenalty, temperature, cancellationToken)
    {
    }
}