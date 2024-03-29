using OpenAI_API.Chat;

namespace Musoq.DataSources.OpenAIHelpers;

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
    Task<ChatResult> GetCompletionAsync(OpenAiEntity entity, IList<ChatMessage> messages);
}