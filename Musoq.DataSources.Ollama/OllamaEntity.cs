namespace Musoq.DataSources.Ollama;

/// <summary>
///     Represents an OpenAI entity with various configuration properties.
/// </summary>
public class OllamaEntity : OllamaEntityBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OllamaEntity" /> class with the specified parameters.
    /// </summary>
    /// <param name="api">The IOpenAiApi object used to interact with the OpenAI API.</param>
    /// <param name="model">The optional model name to use for generating text.</param>
    /// <param name="temperature">The temperature to control the randomness of the generated text.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
    public OllamaEntity(IOllamaApi api, string model, float temperature, CancellationToken cancellationToken)
        : base(api, model, temperature, cancellationToken)
    {
    }
}