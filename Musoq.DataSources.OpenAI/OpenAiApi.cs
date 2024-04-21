using OpenAI_API;
using OpenAI_API.Chat;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiApi : IOpenAiApi
{
    private readonly OpenAIAPI _api;

    public OpenAiApi(string apiKey)
    {
        _api = new OpenAIAPI(new APIAuthentication(apiKey));
    }

    public OpenAiApi(string apiKey, string apiUrlFormat)
    {
        _api = new OpenAIAPI(new APIAuthentication(apiKey))
        {
            ApiUrlFormat = apiUrlFormat
        };
    }

    public Task<ChatResult> GetCompletionAsync(OpenAiEntityBase entity, IList<ChatMessage> messages)
    {
        return _api.Chat.CreateChatCompletionAsync(
            messages: messages, 
            model: entity.Model, 
            temperature: entity.Temperature,
            numOutputs: 1, 
            max_tokens:entity.MaxTokens,
            frequencyPenalty: entity.FrequencyPenalty,
            presencePenalty: entity.PresencePenalty);
    }
}