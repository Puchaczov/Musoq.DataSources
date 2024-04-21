using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using ChatRequest = OllamaSharp.Models.Chat.ChatRequest;

namespace Musoq.DataSources.Ollama;

internal class OllamaApi : IOllamaApi
{
    private readonly OllamaApiClient _api;
    
    public const string DefaultAddress = "http://localhost:11434";

    public OllamaApi()
    {
        _api = new(new HttpClient
        {
            BaseAddress = new Uri(DefaultAddress),
            Timeout = TimeSpan.FromMinutes(5)
        });
    }
    
    public OllamaApi(string address)
    {
        _api = new(new HttpClient
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromMinutes(5)
        });
    }
    
    public async Task<ConversationContextWithResponse> GetImageCompletionAsync(OllamaEntityBase entity, Message message)
    {
        var completion = await _api.GetCompletion(new GenerateCompletionRequest()
        {
            Model = entity.Model,
            Context = Array.Empty<long>(),
            Images = message.Images,
            Prompt = message.Content,
            Stream = false,
            Options = new RequestOptions()
            {
                Temperature = entity.Temperature
            }
        });
        
        return completion;
    }

    public async Task<ConversationContextWithResponse> GetCompletionAsync(OllamaEntityBase entity, IList<Message> messages)
    {
        TaskCompletionSource<ConversationContextWithResponse> completionSource = new();
        var stringResponse = new StringBuilder();
        
        await _api.SendChat(new ChatRequest()
        {
            Model = entity.Model,
            Messages = messages,
            Options = new RequestOptions()
            {
                Temperature = entity.Temperature,
            },
            Stream = true
        }, stream =>
        {
            if (!stream.Done)
            {
                stringResponse.Append(stream.Message.Content);
                return;
            }
            
            completionSource.SetResult(new ConversationContextWithResponse(stringResponse.ToString(), Array.Empty<long>()));
        }, entity.CancellationToken);
        
        return completionSource.Task.Result;
    }
}