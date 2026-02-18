using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.OpenAI;

/// <description>
///     Provides interface to work with OpenAI API.
/// </description>
/// <short-description>
///     Provides interface to work with OpenAI API.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class OpenAiSchema : SchemaBase
{
    private const string OpenAiSchemaName = "OpenAi";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt()
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Models to use</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt(string model)
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>
    ///             Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo,
    ///             gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002
    ///         </virtual-param>
    ///         <virtual-param>Max tokens to generate</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt(string model, int maxTokens)
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>
    ///             Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo,
    ///             gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002
    ///         </virtual-param>
    ///         <virtual-param>Max tokens to generate</virtual-param>
    ///         <virtual-param>Temperature</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt(string model, int maxTokens, decimal temperature)
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>
    ///             Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo,
    ///             gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002
    ///         </virtual-param>
    ///         <virtual-param>Max tokens to generate</virtual-param>
    ///         <virtual-param>Temperature</virtual-param>
    ///         <virtual-param>Frequency penalty</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt(string model, int maxTokens, decimal temperature, decimal frequencyPenalty)
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>
    ///             Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo,
    ///             gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002
    ///         </virtual-param>
    ///         <virtual-param>Max tokens to generate</virtual-param>
    ///         <virtual-param>Temperature</virtual-param>
    ///         <virtual-param>Frequency penalty</virtual-param>
    ///         <virtual-param>Presence penalty</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OPENAI_API_KEY" isRequired="true">Open AI api key</environmentVariable>
    ///                     </environmentVariables>
    ///                     #openai.gpt(string model, int maxTokens, decimal temperature, decimal frequencyPenalty, decimal
    ///                     presencePenalty)
    ///                 </from>
    ///                 <description>Gives the access to OpenAI api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public OpenAiSchema()
        : base(OpenAiSchemaName, CreateLibrary())
    {
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new OpenAiSingleRowTable();
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new OpenAiSingleRowSource(runtimeContext, new OpenAiRequestInfo
        {
            Model = parameters.Length > 0
                ? Convert.ToString(parameters[0]) ?? Defaults.DefaultModel
                : Defaults.DefaultModel,
            MaxTokens = parameters.Length > 1 ? Convert.ToInt32(parameters[1]) : 4000,
            Temperature = parameters.Length > 2 ? Convert.ToSingle(parameters[2]) : 0,
            FrequencyPenalty = parameters.Length > 3 ? Convert.ToSingle(parameters[3]) : 0,
            PresencePenalty = parameters.Length > 4 ? Convert.ToSingle(parameters[4]) : 0
        });
    }

    /// <summary>
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();

        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<OpenAiSingleRowSource>("gpt"));

        return constructors.ToArray();
    }

    /// <summary>
    ///     Gets raw information's about specific method in the schema.
    /// </summary>
    /// <param name="methodName">Method name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName switch
        {
            "gpt" => CreateGptMethodInfos(),
            _ => throw new NotSupportedException($"Method '{methodName}' is not supported. Available methods: gpt")
        };
    }

    /// <summary>
    ///     Gets raw information's about all tables in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return CreateGptMethodInfos();
    }

    private static SchemaMethodInfo[] CreateGptMethodInfos()
    {
        var gptInfo1 = new ConstructorInfo(
            null!,
            false,
            []);

        var gptInfo2 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string))
            ]);

        var gptInfo3 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string)),
                ("maxTokens", typeof(int))
            ]);

        var gptInfo4 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string)),
                ("maxTokens", typeof(int)),
                ("temperature", typeof(float))
            ]);

        var gptInfo5 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string)),
                ("maxTokens", typeof(int)),
                ("temperature", typeof(float)),
                ("frequencyPenalty", typeof(float)),
                ("presencePenalty", typeof(float))
            ]);

        return
        [
            new SchemaMethodInfo("gpt", gptInfo1),
            new SchemaMethodInfo("gpt", gptInfo2),
            new SchemaMethodInfo("gpt", gptInfo3),
            new SchemaMethodInfo("gpt", gptInfo4),
            new SchemaMethodInfo("gpt", gptInfo5)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new OpenAiLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}