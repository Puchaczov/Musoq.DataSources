using System.Text;
using Musoq.DataSources.LLMHelpers;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Newtonsoft.Json;
using OpenAI_API.Chat;
using SharpToken;

namespace Musoq.DataSources.OpenAI;

/// <summary>
/// The OpenAiLibrary class provides an interface for interacting with the OpenAI API
/// to perform various natural language processing tasks such as sentiment analysis,
/// text summarization, translation, and entity extraction.
/// </summary>
public class OpenAiLibrary : LibraryBase, ILargeLanguageModelFunctions<OpenAiEntity>
{
    private const string SentimentPrompt = 
        @"You are skilled sentiment analyzer. Decide whether the sentiment of a text is POSITIVE, NEGATIVE or NEUTRAL. Responde by using only those three values.";

    private const string ContentAboutPrompt = 
        @"You are highly intelligent answering bot. For provided text respond with YES or NO whether the text is about the given question. Do not say anything but YES or NO.";

    private const string ExtractEntitiesPrompt = 
        @"You are entities extractor. Respond with the json object { entities: string[] }";

    private const string TranslateGivenTextPrompt = 
        @"You are {SOURCE_LANGUAGE} to {DESTINATION_LANGUAGE} translator. Translate provided message to destination language";

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
    public bool IsContentAbout([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content, string question)
    {
        var api = entity.Api;
        var isContentAboutResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {                    
            new(ChatMessageRole.System, ContentAboutPrompt),
            new(ChatMessageRole.User, content),
            new(ChatMessageRole.User, question),
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
    public string Sentiment([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content, bool throwOnUnknown = false)
    {
        var api = entity.Api;
        var sentimentResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {
            new(ChatMessageRole.System, SentimentPrompt),
            new(ChatMessageRole.User, content)
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
    public string SummarizeContent([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content)
    {
        var api = entity.Api;
        var summarizeResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {
            new(ChatMessageRole.System, SummarizerPrompt),
            new(ChatMessageRole.User, content)
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
    public string TranslateContent([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content, string from, string to)
    {
        var api = entity.Api;
        var translateResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {
            new(ChatMessageRole.System, TranslateGivenTextPrompt.Replace("{SOURCE_LANGUAGE}", from).Replace("{DESTINATION_LANGUAGE}", to)),
            new(ChatMessageRole.User, content)
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
    public string[] Entities([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content, bool throwOnException = false)
    {
        var api = entity.Api;
        var getEntitiesResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {
            new(ChatMessageRole.System, ExtractEntitiesPrompt),
            new(ChatMessageRole.User, content)
        }));
        
        var entitiesResult = getEntitiesResultTask.Result.ToLowerInvariant();

        try
        {
            var entities = JsonConvert.DeserializeObject<ExtractedEntities>(entitiesResult);

            return entities?.Entities ?? [];
        }
        catch (Exception)
        {
            if (throwOnException)
                throw;
            
            return [];
        }
    }

    /// <summary>
    /// Performs the specified action on the provided entity using the OpenAI API and returns the result.
    /// </summary>
    /// <param name="entity">OpenAI entity</param>
    /// <param name="whatToDo">Action to perform</param>
    /// <param name="column">Column</param>
    /// <typeparam name="TColumnType">Column type</typeparam>
    /// <returns>Result</returns>
    [BindableMethod]
    public string LlmPerform<TColumnType>([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string whatToDo, TColumnType column)
    {
        var api = entity.Api;
        var translateResultTask = DoAsynchronously(() => api.GetCompletionAsync(entity, new List<ChatMessage>()
        {
            new(ChatMessageRole.System, whatToDo),
            new(ChatMessageRole.User, column?.ToString())
        }));
        
        return translateResultTask.Result;
    }

    /// <summary>
    /// Describes the provided image using the OpenAI API and returns a description of the image.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="imageBase64">Base64 encoded image</param>
    /// <returns>Image description</returns>
    [BindableMethod]
    public string DescribeImage([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string imageBase64)
    {
        var api = entity.Api;
        var describeResultTask = DoAsynchronously(() =>
        {
            const string youAreImageDescriberDescribeTheImage = "You are image describer. Describe the image.";
            return api.GetCompletionAsync(entity, new List<ChatMessage>()
            {
                new(ChatMessageRole.System, youAreImageDescriberDescribeTheImage),
                new(ChatMessageRole.User, string.Empty, [new(Encoding.UTF8.GetBytes(imageBase64))])
            });
        });
        
        return describeResultTask.Result;
    }

    /// <summary>
    /// Asks a question about the provided image using the OpenAI API and returns the answer to the question.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="question">Question</param>
    /// <param name="imageBase64">Base64 encoded image</param>
    /// <returns>Answer to the question</returns>
    [BindableMethod]
    public string AskImage([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string question, string imageBase64)
    {
        var api = entity.Api;
        var askResultTask = DoAsynchronously(() =>
        {
            const string youAreImageQuestionerAskQuestionAboutImage = "You are image questioner. Ask a question about the image.";
            return api.GetCompletionAsync(entity, new List<ChatMessage>()
            {
                new(ChatMessageRole.System, youAreImageQuestionerAskQuestionAboutImage),
                new(ChatMessageRole.User, question, [new(Encoding.UTF8.GetBytes(imageBase64))])
            });
        });
        
        return askResultTask.Result;
    }

    /// <summary>
    /// Asks a question about the provided image using the OpenAI API and returns a boolean result.
    /// </summary>
    /// <param name="entity">The OpenAI entity</param>
    /// <param name="question">The question</param>
    /// <param name="imageBase64">The base64 encoded image</param>
    /// <returns>True if the image answers the question, otherwise false</returns>
    [BindableMethod]
    public bool IsQuestionApplicableToImage([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string question, string imageBase64)
    {
        var api = entity.Api;
        var askResultTask = DoAsynchronously(() =>
        {
            var youAreImageQuestionerAskQuestionAboutImage = $"You are image based question answerer. Return only answer for the following question: {question}. You must respond with json {{ result: boolean }}. Do not comment or explain anything.";
            return api.GetCompletionAsync(entity, new List<ChatMessage>
            {
                new(ChatMessageRole.System, youAreImageQuestionerAskQuestionAboutImage),
                new(ChatMessageRole.User, question, [new(Encoding.UTF8.GetBytes(imageBase64))])
            });
        });
        
        var response = askResultTask.Result;
        
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
    /// Counts the number of tokens in the provided content using the specified model.
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="content">Content</param>
    /// <returns>Number of tokens</returns>
    [BindableMethod]
    public int CountTokens([InjectSpecificSource(typeof(OpenAiEntity))] OpenAiEntity entity, string content)
    {
        var encoding = GptEncoding.GetEncodingForModel(entity.Model);

        return encoding.CountTokens(content);
    }

    /// <summary>
    /// Counts the number of tokens in the provided content using the specified model.
    /// </summary>
    /// <param name="content">Content</param>
    /// <returns>Number of tokens</returns>
    /// <exception cref="NotImplementedException"></exception>
    [BindableMethod]
    public int CountTokens(string content)
    {
        var encoding = GptEncoding.GetEncoding("r50k_base");
        
        return encoding.CountTokens(content);
    }

    /// <summary>
    /// Counts the number of tokens in the provided content using the specified model.
    /// </summary>
    /// <param name="encodingName">Encoding name: r50k_base, p50k_base, p50k_edit, cl100k_base</param>
    /// <param name="content">Content</param>
    /// <returns>Number of tokens</returns>
    [BindableMethod]
    public int CountTokens(string encodingName, string content)
    {
        var encoding = GptEncoding.GetEncoding(encodingName);

        return encoding.CountTokens(content);
    }
    
    private static async Task<string> DoAsynchronously(Func<Task<ChatResult>> func)
    {
        var result = func();
        var stringResult = string.Empty;

        try
        {
            var completion = await result;
            
            if (!completion.Choices.Any()) return stringResult;

            var choice = completion.Choices[0];
            var message = choice.Message;
                    
            if (message == null)
                return stringResult;
            
            return message.TextContent ?? string.Empty;
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