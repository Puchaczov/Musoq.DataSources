using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class ImagesTests
{
    [TestMethod]
    public void WhenDirectoryPointed_ShouldReturnItsMetadata()
    {
        var query = "select f.AllMetadataJson() from #os.files('./Images', false) f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
    }
    
    [TestMethod]
    public void WhenImagePointed_ShouldReturnItsMetadata()
    {
        var query = "select FullName, DirectoryName, TagName, Description from #os.metadata('./Images/IMG_2426.JPG') f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(154, table.Count);
    }
    
    [TestMethod]
    public void WhenImagePointedWrongFile_ShouldNotThrow()
    {
        var query = "select FullName, DirectoryName, TagName, Description from #os.metadata('./Files/File1.txt') f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(0, table.Count);
    }
    
    [TestMethod]
    public void WhenGetMetadataWithDirectoryCalled_ShouldReturnMetadata()
    {
        var query = "select f.GetMetadata('Exif IFD0', 'Resolution Unit') from #os.files('./Images', false) f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual("Inch", table[0][0]);
        Assert.AreEqual("Inch", table[1][0]);
    }
    
    [TestMethod]
    public void WhenGetMetadataCalled_ShouldReturnMetadata()
    {
        var query = "select f.GetMetadata('Resolution Unit') from #os.files('./Images', false) f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual("Inch", table[0][0]);
        Assert.AreEqual("Inch", table[1][0]);
    }
    
    [TestMethod]
    public void WhenHasMetadataCalled_ShouldReturnTrue()
    {
        var query = "select 1 from #os.files('./Images', false) f where f.HasMetadataDirectory('Exif IFD0')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(1, table[0][0]);
        Assert.AreEqual(1, table[1][0]);
    }
    
    [TestMethod]
    public void WhenHasMetadataTagCalled_ShouldReturnTrue()
    {
        var query = "select 1 from #os.files('./Images', false) f where f.HasMetadataTag('Y Resolution')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(1, table[0][0]);
        Assert.AreEqual(1, table[1][0]);
    }
    
    [TestMethod]
    public void WhenAllMetadataJsonCalled_ShouldReturnJson()
    {
        var query = "select f.AllMetadataJson() from #os.files('./Images', false) f";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.IsTrue(IsValidJson((string)table[0][0]));
        Assert.IsTrue(IsValidJson((string)table[1][0]));
    }

    private static bool IsValidJson(string s)
    {
        try
        {
            JToken.Parse(s);
            return true;
        }
        catch (JsonReaderException e)
        {
            return false;
        }
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new OsSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static ImagesTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}