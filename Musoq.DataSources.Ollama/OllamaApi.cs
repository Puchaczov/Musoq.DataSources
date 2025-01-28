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
    
    public async Task<CompletionResponse> GetImageCompletionAsync(OllamaEntityBase entity, Message message)
    {
        StringBuilder modelResponse = new();
        var chatRequest = new ChatRequest
        {
            Model = entity.Model,
            Messages = new List<Message>
            {
                message
            },
            Options = new RequestOptions
            {
                Temperature = entity.Temperature
            },
            Stream = true
        };
        
        await foreach (var token in _api.ChatAsync(chatRequest, entity.CancellationToken))
        {
            if (token is null)
                continue;
            
            if (token.Done)
                break;
            
            modelResponse.Append(token.Message.Content);
        }
        
        return new CompletionResponse(modelResponse.ToString());
    }

    public async Task<CompletionResponse> GetCompletionAsync(OllamaEntityBase entity, IList<Message> messages)
    {
        StringBuilder modelResponse = new();
        var chatRequest = new ChatRequest
        {
            Model = entity.Model,
            Messages = messages,
            Options = new RequestOptions
            {
                Temperature = entity.Temperature
            },
            Stream = true
        };
        
        await foreach (var token in _api.ChatAsync(chatRequest, entity.CancellationToken))
        {
            if (token is null)
                continue;
            
            if (token.Done)
                break;
            
            modelResponse.Append(token.Message.Content);
        }
        
        return new CompletionResponse(modelResponse.ToString());
    }
}