using Microsoft.Extensions.DependencyInjection;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Ollama;

/// <description>
/// Provides interface to work with Ollama API.
/// </description>
/// <short-description>
/// Provides interface to work with Ollama API.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class OllamaSchema : SchemaBase
{
    private const string OllamaSchemaName = "Ollama";
    
    private readonly ServiceProvider _serviceProvider;
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <virtual-param>Model to use: llama2, mistral, llava etc</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="OLLAMA_BASE_URL" isRequired="false">Ollama base url, default http://localhost:11434</environmentVariable>
    /// </environmentVariables>
    /// #ollama.llm(string model)
    /// </from>
    /// <description>Gives the access to Ollama api</description>
    /// <columns isDynamic="true"></columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Model to use: llama2, mistral, llava etc</virtual-param>
    /// <virtual-param>Max tokens to generate</virtual-param>
    /// <virtual-param>Temperature</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="OLLAMA_BASE_URL" isRequired="false">Ollama base url, default http://localhost:11434</environmentVariable>
    /// </environmentVariables>
    /// #ollama.llm(string model, float temperature)
    /// </from>
    /// <description>Gives the access to Ollama api</description>
    /// <columns isDynamic="true"></columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public OllamaSchema() 
        : base(OllamaSchemaName, CreateLibrary())
    {
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddHttpClient();
        
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new OllamaSingleRowTable();
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        runtimeContext.EnvironmentVariables.TryGetValue("OLLAMA_BASE_URL", out var ollamaBaseUrl);
        
        ollamaBaseUrl ??= OllamaApi.DefaultAddress;
        
        return new OllamaSingleRowSource(runtimeContext, new OllamaRequestInfo
        {
            Model = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? throw new Exception("Model name cannot be null.") : throw new Exception("Model name is required."),
            Temperature = parameters.Length > 1 ? MapParameter(parameters[1]) : 0,
            OllamaBaseUrl = ollamaBaseUrl
        }, _serviceProvider.GetRequiredService<IHttpClientFactory>());
    }

    /// <summary>
    /// Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return [];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new OllamaLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }

    private static float MapParameter(object parameter)
    {
        if (parameter is float f)
            return f;
        
        if (parameter is double d)
            return (float)d;

        if (parameter is decimal dec)
            return Convert.ToSingle(dec);
        
        throw new Exception("Temperature parameter must be float, double or decimal number.");
    }
}