namespace Musoq.DataSources.Ollama;

/// <summary>
/// Represents an Ollama entity with various configuration properties.
/// </summary>
public abstract class OllamaEntityBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="api"/> class with the specified parameters.
    /// </summary>
    protected OllamaEntityBase(IOllamaApi api, string model, float temperature, CancellationToken cancellationToken)
    {
        Api = api;
        Model = model;
        Temperature = temperature;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the IOpenAiApi object used to interact with the OpenAI API.
    /// </summary>
    public IOllamaApi Api { get; }
    
    /// <summary>
    /// Gets the optional model name to use for generating text.
    /// </summary>
    public string Model { get; }
    
    /// <summary>
    /// Gets the temperature to control the randomness of the generated text.
    /// </summary>
    public float Temperature { get; }
    
    /// <summary>
    /// Gets the cancellation token to cancel the request.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}