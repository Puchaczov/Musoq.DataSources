using OpenAI;
using OpenAI.Chat;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiApi(string apiKey) : IOpenAiApi
{
    private readonly OpenAIClient _api = new(apiKey);

    public async Task<CompletionResponse> GetCompletionAsync(OpenAiEntityBase entity, IList<ChatMessage> messages)
    {
        entity.CancellationToken.ThrowIfCancellationRequested();

        var clientChat = _api.GetChatClient(entity.Model);
        var clientResult = await clientChat.CompleteChatAsync(
            messages,
            new ChatCompletionOptions
            {
                Temperature = entity.Temperature,
                MaxOutputTokenCount = entity.MaxTokens,
                FrequencyPenalty = entity.FrequencyPenalty,
                PresencePenalty = entity.PresencePenalty
            }, entity.CancellationToken);

        return new CompletionResponse(clientResult.Value.Content.First().Text);
    }
}