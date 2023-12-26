using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
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
        const string script = "select Key, IsDirectory, (case when IsDirectory = false then TextContent else '' end) from #archives.file('./Files/Example1/archives.zip')";
        
        var vm = CreateAndRunVirtualMachine(script);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Count);
        Assert.AreEqual("others/", table[0].Values[0]);
        Assert.AreEqual(true, table[0].Values[1]);
        Assert.AreEqual("", table[0].Values[2]);
        Assert.AreEqual("others/text3.txt", table[1].Values[0]);
        Assert.AreEqual(false, table[1].Values[1]);
        Assert.AreEqual("Text 3", table[1].Values[2]);
        Assert.AreEqual("text1.txt", table[2].Values[0]);
        Assert.AreEqual(false, table[2].Values[1]);
        Assert.AreEqual("Text 1", table[2].Values[2]);
        Assert.AreEqual("text2.txt", table[3].Values[0]);
        Assert.AreEqual(false, table[3].Values[1]);
        Assert.AreEqual("Text 2", table[3].Values[2]);
    }
    
    [TestMethod]
    public void WhenWorkingWithTarArchive_ShouldRead()
    {
        var script = "select Key, IsDirectory, (case when IsDirectory = false then TextContent else '' end) from #archives.file('./Files/Example1/archives.tar')";
        
        var vm = CreateAndRunVirtualMachine(script);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Count);
        Assert.AreEqual("others/", table[0].Values[0]);
        Assert.AreEqual(true, table[0].Values[1]);
        Assert.AreEqual("", table[0].Values[2]);
        Assert.AreEqual("others/text3.txt", table[1].Values[0]);
        Assert.AreEqual(false, table[1].Values[1]);
        Assert.AreEqual("Text 3", table[1].Values[2]);
        Assert.AreEqual("text1.txt", table[2].Values[0]);
        Assert.AreEqual(false, table[2].Values[1]);
        Assert.AreEqual("Text 1", table[2].Values[2]);
        Assert.AreEqual("text2.txt", table[3].Values[0]);
        Assert.AreEqual(false, table[3].Values[1]);
        Assert.AreEqual("Text 2", table[3].Values[2]);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new ArchivesSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static ArchivesTests()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}