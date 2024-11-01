using System.Collections.Concurrent;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Git;

/// <summary>
/// Provides schema to work with Git data source
/// </summary>
public class GitSchema : SchemaBase
{
    private const string SchemaName = "Git";
    
    internal static readonly ConcurrentDictionary<string, RepositoryEntity> Repositories = new();
    
    public GitSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
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
        switch (name.ToLowerInvariant())
        {
            case "repository":
                return new RepositoryTable();
        }

        return base.GetTableByName(name, runtimeContext, parameters);
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
        switch (name.ToLowerInvariant())
        {
            case "repository":
                return new RepositoryRowsSource((string) parameters[0], runtimeContext.EndWorkToken);
        }

        return base.GetRowSource(name, runtimeContext, parameters);
    }
    
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new GitLibrary();
        
        methodsManager.RegisterLibraries(library);
        
        return new MethodsAggregator(methodsManager);
    }
}