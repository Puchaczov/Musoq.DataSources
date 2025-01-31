using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using Polly;
using Polly.Retry;
using ChatRequest = OllamaSharp.Models.Chat.ChatRequest;

namespace Musoq.DataSources.Ollama;

internal class OllamaApi : IOllamaApi
{
    private readonly string _address;
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);
    private readonly AsyncRetryPolicy<CompletionResponse> _retryPolicy;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public const string DefaultAddress = "http://localhost:11434";

    public OllamaApi(IHttpClientFactory httpClientFactory)
        : this(DefaultAddress, httpClientFactory)
    {
    }
    
    public OllamaApi(string address, IHttpClientFactory httpClientFactory)
    {
        _address = address;
        _httpClientFactory = httpClientFactory;
        _retryPolicy = Policy<CompletionResponse>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
    
    public async Task<CompletionResponse> GetImageCompletionAsync(OllamaEntityBase entity, Message message)
    {   
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            entity.CancellationToken.ThrowIfCancellationRequested();
            
            var chatRequest = CreateChatRequest(entity, new List<Message> { message });
            
            return await ProcessChatRequestAsync(chatRequest, entity.CancellationToken);
        });
    }

    public Task<CompletionResponse> GetImageCompletionAsync(OllamaEntityBase entity, IList<Message> messages)
    {
        return GetCompletionAsync(entity, messages);
    }

    public async Task<CompletionResponse> GetCompletionAsync(OllamaEntityBase entity, IList<Message> messages)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            entity.CancellationToken.ThrowIfCancellationRequested();
            
            var chatRequest = CreateChatRequest(entity, messages);
            
            return await ProcessChatRequestAsync(chatRequest, entity.CancellationToken);
        });
    }

    private ChatRequest CreateChatRequest(OllamaEntityBase entity, IList<Message> messages)
    {
        return new ChatRequest
        {
            Model = entity.Model,
            Messages = messages,
            Options = new RequestOptions
            {
                Temperature = entity.Temperature,
                NumCtx = 16384
            },
            Stream = true
        };
    }

    private async Task<CompletionResponse> ProcessChatRequestAsync(ChatRequest chatRequest, CancellationToken cancellationToken)
    {
        StringBuilder modelResponse = new();
        
        var client = _httpClientFactory.CreateClient();
        
        client.BaseAddress = new Uri(_address);
        client.Timeout = _timeout;
    
        var api = new OllamaApiClient(client);
    
        await foreach (var token in api.ChatAsync(chatRequest, cancellationToken))
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