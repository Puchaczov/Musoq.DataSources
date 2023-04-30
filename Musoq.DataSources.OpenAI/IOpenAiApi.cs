using OpenAI_API.Chat;

namespace Musoq.DataSources.OpenAI;

public interface IOpenAiApi
{
    Task<ChatResult> GetCompletionAsync(OpenAiEntity entity, IList<ChatMessage> messages);
}