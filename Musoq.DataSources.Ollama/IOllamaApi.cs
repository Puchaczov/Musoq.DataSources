using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace Musoq.DataSources.Ollama;

/// <summary>
/// Interface for OpenAI API
/// </summary>
public interface IOllamaApi
{
    
    /// <summary>
    /// Gets the completion from Ollama API
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="messages">Messages</param>
    /// <returns>CompletionResponse</returns>
    Task<CompletionResponse> GetCompletionAsync(OllamaEntityBase entity, IList<Message> messages);

    /// <summary>
    /// Gets the image completion from Ollama API
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="message">Message</param>
    /// <returns>CompletionResponse</returns>
    Task<CompletionResponse> GetImageCompletionAsync(OllamaEntityBase entity, Message message);
}