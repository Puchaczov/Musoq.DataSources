using Musoq.DataSources.LLMHelpers;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Newtonsoft.Json;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using SharpToken;

namespace Musoq.DataSources.Ollama;

/// <summary>
/// The OllamaLibrary class provides an interface for interacting with the Ollama API
/// to perform various natural language processing tasks such as sentiment analysis,
/// text summarization, translation, and entity extraction.
/// </summary>
public class OllamaLibrary : LibraryBase, ILargeLanguageModelFunctions<OllamaEntity>
{
    private const string SentimentPrompt = 
        @"You are skilled sentiment analyzer. Decide whether the sentiment of a text is POSITIVE, NEGATIVE or NEUTRAL. Responde by using only those three values.";

    private const string ContentAboutPrompt = 
        @"You are highly intelligent answering bot. For provided text respond with YES or NO whether the text is about the given question. Do not say anything but YES or NO.";

    private const string ExtractEntitiesPrompt = 
        @"You are entities extractor. Respond with the json object { entities: string[] }";

    private const string TranslateGivenTextPrompt = 
        @"You are {SOURCE_LANGUAGE} to {DESTINATION_LANGUAGE} translator. Translate provided message to destination language. Do not describe anything. Be clear and concise. Repond with only translated text.";

    private const string SummarizerPrompt = 
        @"You are text summarizer. Summarize provided text";

    /// <summary>
    /// Determines whether the provided content is related to the given question.
    /// Returns true if the content is related, otherwise returns false.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <param name="question">Question</param>
    /// <returns>True is content is about given question, otherwise false</returns> 
    [BindableMethod]
    public bool IsContentAbout([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content, string question)
    {
        var api = entity.Api;
        var isContentAboutResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>
        {                    
            new(ChatRole.System, ContentAboutPrompt),
            new(ChatRole.User, content),
            new(ChatRole.User, question),
        }));
        var sentimentResult = isContentAboutResultTask.Result.ToLowerInvariant();
        
        return sentimentResult.Contains("yes");
    }
    
    /// <summary>
    /// Performs sentiment analysis on the provided content and returns the sentiment
    /// as one of the following strings: "POSITIVE", "NEGATIVE", "NEUTRAL", or "UNKNOWN".
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <param name="throwOnUnknown">Whether to throw an exception if the sentiment is unknown</param>
    /// <returns>Sentiment</returns>
    [BindableMethod]
    public string Sentiment([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content, bool throwOnUnknown = false)
    {
        var api = entity.Api;
        var sentimentResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>()
        {
            new(ChatRole.System, SentimentPrompt),
            new(ChatRole.User, content)
        }));
        var sentimentResult = sentimentResultTask.Result.ToLowerInvariant();
        
        return sentimentResult.Contains("positive") 
            ? "POSITIVE" : sentimentResult.Contains("negative") 
                ? "NEGATIVE" : sentimentResult.Contains("neutral") 
                    ? "NEUTRAL" : throwOnUnknown 
                        ? throw new InvalidOperationException($"Unknown sentiment result: {sentimentResult}") 
                        : "UNKNOWN";
    }
    
    /// <summary>
    /// Summarizes the provided content using the OpenAI API and returns the summarized text.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <returns>Summarized content</returns>
    [BindableMethod]
    public string SummarizeContent([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content)
    {
        var api = entity.Api;
        var summarizeResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>()
        {
            new(ChatRole.System, SummarizerPrompt),
            new(ChatRole.User, content)
        }));
        
        return summarizeResultTask.Result;
    }
    
    /// <summary>
    /// Translates the provided content from the source language to the destination language using the OpenAI API.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <param name="from">Source language</param>
    /// <param name="to">Destination language</param>
    /// <returns>Translated content</returns>
    [BindableMethod]
    public string TranslateContent([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content, string from, string to)
    {
        var api = entity.Api;
        var translateResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>()
        {
            new(ChatRole.System, TranslateGivenTextPrompt.Replace("{SOURCE_LANGUAGE}", from).Replace("{DESTINATION_LANGUAGE}", to)),
            new(ChatRole.User, content)
        }));
        
        return translateResultTask.Result;
    }
    
    /// <summary>
    /// Extracts entities from the provided content using the OpenAI API and returns an array of entity strings.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <param name="throwOnException">Whether to throw an exception if an exception occurs</param>
    /// <returns>Array of entities</returns>
    [BindableMethod]
    public string[] Entities([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content, bool throwOnException = false)
    {
        var api = entity.Api;
        var getEntitiesResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>()
        {
            new(ChatRole.System, ExtractEntitiesPrompt),
            new(ChatRole.User, content)
        }));
        
        var entitiesResult = getEntitiesResultTask.Result.ToLowerInvariant();

        try
        {
            var entities = JsonConvert.DeserializeObject<ExtractedEntities>(entitiesResult);

            return entities?.Entities ?? Array.Empty<string>();
        }
        catch (Exception)
        {
            if (throwOnException)
                throw;
            
            return Array.Empty<string>();
        }
    }
    
    /// <summary>
    /// Describes the provided image using the Ollama API and returns the description.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="imageBase64">Base64-encoded image</param>
    /// <returns>Image description</returns>
    [BindableMethod]
    public string DescribeImage([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string imageBase64)
    {
        var api = entity.Api;
        var describeImageResultTask = DoAsynchronously(() =>
        {
            const string youAreImageDescriberDescribeTheImage = "You are image describer. Describe the image.";
            return api.GetImageCompletionAsync(
                entity, 
                new(ChatRole.User, youAreImageDescriberDescribeTheImage, new[]{ imageBase64 }));
        });
        
        return describeImageResultTask.Result;
    }

    /// <summary>
    /// Ask the provided image using the Ollama API and returns the response.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="question">Question</param>
    /// <param name="imageBase64">Base64-encoded image</param>
    /// <returns>Image description</returns>
    [BindableMethod]
    public string AskImage([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string question, string imageBase64)
    {
        var api = entity.Api;
        var describeImageResultTask = DoAsynchronously(() =>
        {
            var youAreImageDescriberDescribeTheImage = $"You are image based question answerer. Return only answer for the following question: {question}.";
            return api.GetImageCompletionAsync(
                entity, 
                new(ChatRole.User, youAreImageDescriberDescribeTheImage, new[]{ imageBase64 }));
        });
        
        return describeImageResultTask.Result;
    }

    /// <summary>
    /// Ask the provided image using the Ollama API and returns the response.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="question">Question</param>
    /// <param name="imageBase64">Base64-encoded image</param>
    /// <returns>True if the image conforms to the filter, otherwise false</returns>
    [BindableMethod]
    public bool IsQuestionApplicableToImage([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string question, string imageBase64)
    {
        var api = entity.Api;
        var describeImageResultTask = DoAsynchronously(() =>
        {
            var youAreImageDescriberDescribeTheImage = $"You are image based question answerer. Return only answer for the following question: {question}. You must respond with json {{ result: boolean }}. Do not comment or explain anything.";
            return api.GetImageCompletionAsync(
                entity, 
                new(ChatRole.User, youAreImageDescriberDescribeTheImage, new[]{ imageBase64 }));
        });
        
        var response = describeImageResultTask.Result;
        
        try
        {
            var result = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response);
            
            if (result == null)
                return false;

            return result["result"];
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Ask the provided image using the Ollama API and returns the response.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="whatToDo">Question</param>
    /// <param name="column">Base64-encoded image</param>
    /// <returns>True if the image conforms to the filter, otherwise false</returns>
    [BindableMethod]
    public string LlmPerform<TColumnType>([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string whatToDo, TColumnType column)
    {
        var api = entity.Api;
        var translateResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<Message>()
        {
            new(ChatRole.System, whatToDo),
            new(ChatRole.User, column.ToString())
        }));
        
        return translateResultTask.Result;
    }
    
    /// <summary>
    /// Counts the number of tokens in the provided content using the specified model.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <returns>Number of tokens</returns>
    [BindableMethod]
    public int CountTokens([InjectSpecificSource(typeof(OllamaEntity))] OllamaEntity entity, string content)
    {
        var encoding = GptEncoding.GetEncodingForModel("gpt-3.5-turbo");

        return encoding.CountTokens(content);
    }

    /// <summary>
    /// Counts the number of tokens in the provided content using the specified model.
    /// </summary>
    /// <param name="content">Content</param>
    /// <returns>Number of tokens</returns>
    [BindableMethod]
    public int CountTokens(string content)
    {
        var encoding = GptEncoding.GetEncodingForModel("gpt-3.5-turbo");

        return encoding.CountTokens(content);
    }
    
    private static async Task<string> DoAsynchronously(Func<Task<ConversationContextWithResponse>> func)
    {
        var result = func();

        try
        {
            var completion = await result;
            
            return completion.Response ?? string.Empty;
        }
        catch (Exception)
        {
            return "ERROR";
        }
    }
    
    private class ExtractedEntities
    {
        public ExtractedEntities(string[] entities)
        {
            Entities = entities;
        }

        public string[] Entities { get; set; }
    }
}