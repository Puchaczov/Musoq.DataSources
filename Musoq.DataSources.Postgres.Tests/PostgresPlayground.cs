using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Postgres.Tests;

[TestClass]
public class PostgresPlayground
{
    [Ignore]
    [TestMethod]
    public void Playground()
    {
        var query = "select Id, Name, Description, Version, VersionHash, Path, UserId, Platform, ShortDescription, ProjectUrl, Architecture from #postgres.DataSources('toolbox') where Architecture > 1";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query);
        var table = vm.Run();
    }
    
    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new PostgresSchemaProvider(),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>
                {
                    { "NPGSQL_CONNECTION_STRING", System.Environment.GetEnvironmentVariable("PLAYGROUND_POSTGRES_CONNECTION_STRING") ?? throw new InvalidOperationException("No connection string provided.") }
                }}
            });
    }

    static PostgresPlayground()
    {
        Culture.ApplyWithDefaultCulture();
    }
}