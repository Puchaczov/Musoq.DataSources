using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public class ArchivesTests
{
    [TestMethod]
    public void WhenWorkingWithZipArchive_ShouldRead()
    {
        const string script = "select Key, IsDirectory, (case when IsDirectory = false then GetTextContent() else '' end) from #archives.file('./Files/Example1/archives.zip')";
        
        var vm = CreateAndRunVirtualMachine(script);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "others/" && 
            (bool)row.Values[1] == true && 
            (string)row.Values[2] == ""
        ), "First row should match others/, true, empty string");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "others/text3.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 3"
        ), "Second row should match others/text3.txt, false, Text 3");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "text1.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 1"
        ), "Third row should match text1.txt, false, Text 1");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "text2.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 2"
        ), "Fourth row should match text2.txt, false, Text 2");
    }
    
    [TestMethod]
    public void WhenWorkingWithTarArchive_ShouldRead()
    {
        var script = "select Key, IsDirectory, (case when IsDirectory = false then GetTextContent() else '' end) from #archives.file('./Files/Example1/archives.tar')";
        
        var vm = CreateAndRunVirtualMachine(script);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "others/" && 
            (bool)row.Values[1] == true && 
            (string)row.Values[2] == ""
        ), "First row should match others/, true, empty string");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "others/text3.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 3"
        ), "Second row should match others/text3.txt, false, Text 3");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "text1.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 1"
        ), "Third row should match text1.txt, false, Text 1");

        Assert.IsTrue(table.Any(row => 
            (string)row.Values[0] == "text2.txt" && 
            (bool)row.Values[1] == false && 
            (string)row.Values[2] == "Text 2"
        ), "Fourth row should match text2.txt, false, Text 2");
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new ArchivesSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static ArchivesTests()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}