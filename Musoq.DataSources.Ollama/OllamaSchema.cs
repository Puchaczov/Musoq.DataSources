using Musoq.DataSources.Common;
using Microsoft.Extensions.DependencyInjection;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Ollama;

/// <description>
///     Recognized service failures provide structured diagnostics; supported fallbacks report warnings alongside results.
///     Provides interface to work with Ollama API.
/// </description>
/// <short-description>
///     Provides interface to work with Ollama API.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class OllamaSchema : SchemaBase
{
    internal static readonly ProviderDiagnostics Reports = new("OLLAMA", classify: OllamaDiagnostics.Map);

    private const string OllamaSchemaName = "Ollama";

    private readonly ServiceProvider _serviceProvider;

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param name="model">Model to use: llama2, mistral, llava etc</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OLLAMA_BASE_URL" isRequired="false">
    ///                             Ollama base url, default
    ///                             http://localhost:11434
    ///                         </environmentVariable>
    ///                     </environmentVariables>
    ///                     ollama.llm(string model)
    ///                 </from>
    ///                 <description>Gives the access to Ollama api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param name="model">Model to use: llama2, mistral, llava etc</virtual-param>
    ///         <virtual-param name="temperature">Temperature</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="OLLAMA_BASE_URL" isRequired="false">
    ///                             Ollama base url, default
    ///                             http://localhost:11434
    ///                         </environmentVariable>
    ///                     </environmentVariables>
    ///                     ollama.llm(string model, decimal temperature)
    ///                 </from>
    ///                 <description>Gives the access to Ollama api</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public OllamaSchema()
        : base(OllamaSchemaName, CreateLibrary())
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddHttpClient();

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="metadataContext">Metadata context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return new OllamaSingleRowTable();
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="executionContext">Execution context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource<T> GetRowSource<T, TArguments>(
        string name,
        SourceExecutionContext executionContext,
        TArguments parameters)
        => Reports.Run(() => Reports.Wrap(GetRowSourceCore<T, TArguments>(name, executionContext, parameters), executionContext.EndWorkToken), executionContext.EndWorkToken);

    private RowSource<T> GetRowSourceCore<T, TArguments>(string name, SourceExecutionContext executionContext, TArguments parameters)
        where TArguments : struct, ISourceArguments
    {
        executionContext.SourceRuntimeSettings.TryGetValue("OLLAMA_BASE_URL", out var ollamaBaseUrl);

        ollamaBaseUrl ??= OllamaApi.DefaultAddress;
        if (!Uri.TryCreate(ollamaBaseUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
            throw Reports.Configuration("OLLAMA_BASE_URL");

        return EnsureSourceType<T, OllamaEntity>(
            name,
            new OllamaSingleRowSource(executionContext, new OllamaRequestInfo
            {
                Model = parameters.Count > 0
                    ? parameters.GetValue<string>(0) ?? throw Reports.Failure(Musoq.Schema.Diagnostics.DataSourceFailureKind.InvalidInput)
                    : throw Reports.Failure(Musoq.Schema.Diagnostics.DataSourceFailureKind.InvalidInput),
                Temperature = parameters.Count > 1 ? (float)parameters.GetValue<decimal>(1) : 0,
                OllamaBaseUrl = ollamaBaseUrl
            }, _serviceProvider.GetRequiredService<IHttpClientFactory>()));
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return
        [
            new SourceRuntimeSettingRequirement(
                "OLLAMA_BASE_URL",
                false,
                false,
                SourceRuntimeSettingPhase.Execution,
                "Ollama base URL.")
        ];
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return SourcePlanResult.RejectAll(request);
    }

    /// <summary>
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return [];
    }

    /// <summary>
    ///     Gets raw constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">Name of the data source method</param>
    /// <param name="metadataContext">Metadata context</param>
    /// <returns>Array of constructor information for the specified method</returns>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            "llm" => CreateLlmMethodInfos(),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {OllamaSchemaName} schema. " +
                $"Available data sources: llm")
        };
    }

    /// <summary>
    ///     Gets raw constructor information for all data source methods in the schema.
    /// </summary>
    /// <param name="metadataContext">Metadata context</param>
    /// <returns>Array of constructor information for all methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return CreateLlmMethodInfos();
    }

    private static SchemaMethodInfo[] CreateLlmMethodInfos()
    {
        var llmInfo1 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string))
            ]);

        var llmInfo2 = new ConstructorInfo(
            null!,
            false,
            [
                ("model", typeof(string)),
                ("temperature", typeof(decimal))
            ]);

        return
        [
            new SchemaMethodInfo("llm", llmInfo1),
            new SchemaMethodInfo("llm", llmInfo2)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new OllamaLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }

}
