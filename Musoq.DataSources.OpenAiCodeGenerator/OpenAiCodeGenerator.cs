using Musoq.DataSources.CodeGenerator;
using Musoq.DataSources.OpenAIHelpers;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace Musoq.DataSources.OpenAiCodeGenerator;

public class OpenAiCodeGenerator : ICodeGenerator
{
    private readonly IOpenAiApi _openAiApi;

    public OpenAiCodeGenerator(IOpenAiApi openAiApi)
    {
        _openAiApi = openAiApi;
    }

    public Task<string> GenerateClassAsync(string[] usedColumns, string tool, string input)
    {
        var assemblyLocation = typeof(OpenAiCodeGenerator).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        var templatePath = Path.Combine(assemblyDirectory!, "Prompt.txt");
        
        var template = File.ReadAllText(templatePath);
        var prompt = template
            .Replace("[[TOOL]]", tool)
            .Replace("[[COLUMNS]]", string.Join(", ", usedColumns));
        var entity = new OpenAiEntity(_openAiApi, Model.GPT4.ModelID, 1, 2048, 0, 0);
        
        var messages = new List<ChatMessage>
        {
            new(ChatMessageRole.System, prompt),
            new(ChatMessageRole.User, input)
        };
        
        return _openAiApi.GetCompletionAsync(entity, messages)
            .ContinueWith(t =>
            {
                var result = t.Result;
                var completion = result.Choices[0].Message.Content;
                return completion;
            });
    }
}