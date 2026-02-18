namespace Musoq.DataSources.Ollama;

/// <summary>
///     Completion response
/// </summary>
public class CompletionResponse
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CompletionResponse" /> class.
    /// </summary>
    /// <param name="text">The text</param>
    public CompletionResponse(string text)
    {
        Text = text;
    }

    /// <summary>
    ///     Gets or sets the text
    /// </summary>
    public string Text { get; }
}