using OpenAI.Chat;

namespace Musoq.DataSources.OpenAI;

/// <summary>
/// Interface for OpenAI API
/// </summary>
public interface IOpenAiApi
{
    
    /// <summary>
    /// Gets the completion from OpenAI API
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="messages">Messages</param>
    /// <returns>ChatResult</returns>
    Task<CompletionResponse> GetCompletionAsync(OpenAiEntityBase entity, IList<ChatMessage> messages);
}