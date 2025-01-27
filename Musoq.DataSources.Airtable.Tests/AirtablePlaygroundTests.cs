using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Airtable.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Airtable.Tests;

[Ignore]
[TestClass]
public class AirtablePlaygroundTests
{
    [TestMethod]
    public void BasesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #airtable.bases()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void BasesPlaygroundFields_ShouldBeIgnored()
    {
        const string query = "select Id, Name, PermissionLevel from #airtable.bases()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void BasePlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #airtable.base()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void BasePlaygroundFields_ShouldBeIgnored()
    {
        const string query = "select Id, Name, PrimaryFieldId from #airtable.base()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void TablePlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #airtable.records('Testy')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void TablePlaygroundFields_ShouldBeIgnored()
    {
        const string query = "select Name, SingleLineText, LongText, Checkbox, Currency, Percent, Email, Date from #airtable.records('Testy')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new PlaygroundSchemaProvider(), 
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>
                {
                    {"MUSOQ_AIRTABLE_API_KEY", System.Environment.GetEnvironmentVariable("AIRTABLE_API_KEY") ?? throw new InvalidOperationException()},
                    {"MUSOQ_AIRTABLE_BASE_ID", System.Environment.GetEnvironmentVariable("AIRTABLE_BASE_ID") ?? throw new InvalidOperationException()}
                }}
            });
    }

    static AirtablePlaygroundTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}