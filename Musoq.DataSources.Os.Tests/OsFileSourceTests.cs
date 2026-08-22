using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsFileSourceTests
{
    static OsFileSourceTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void File_ShouldReturnSingleMetadataRow()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name, f.Extension, f.Length from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("File1.txt", table[0][0]);
        Assert.AreEqual(".txt", table[0][1]);
        Assert.AreEqual(new FileInfo("./Files/File1.txt").Length, table[0][2]);
    }

    [TestMethod]
    public void File_ShouldUseExistingTextContentHelper()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.GetFileContent() from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("Example file 1.", table[0][0]);
    }

    [TestMethod]
    public void File_ShouldUseExistingByteContentHelpers()
    {
        var table = CreateAndRunVirtualMachine(
            "select ToHex(f.Head(2), '|'), f.Base64File() from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("EF|BB", table[0][0]);
        Assert.AreEqual("77u/RXhhbXBsZSBmaWxlIDEu", table[0][1]);
    }

    [TestMethod]
    public void File_ShouldApplyAcceptedNamePredicate()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Name = 'File1.txt'").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("File1.txt", table[0][0]);
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsWhenAcceptedNamePredicateDoesNotMatch()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Name = 'Other.txt'").Run();

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void File_ShouldApplyAcceptedExtensionPredicate()
    {
        var matching = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Extension = '.txt'").Run();
        var nonMatching = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Extension = '.bin'").Run();

        Assert.AreEqual(1, matching.Count);
        Assert.AreEqual("File1.txt", matching[0][0]);
        Assert.AreEqual(0, nonMatching.Count);
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsForMissingPath()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/MissingFileForSingularSource.txt') f").Run();

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsForDirectoryPath()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files') f").Run();

        Assert.AreEqual(0, table.Count);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
