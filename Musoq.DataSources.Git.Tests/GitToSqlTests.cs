using System.Globalization;
using Musoq.Converter;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitToSqlTests
{
    [TestMethod]
    public void TestMethod1()
    {
        var query = """
            select 1
            from #git.repository('D:\repos\Musoq')
        """;

        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.IsTrue(result.Count > 0);
    }

    static GitToSqlTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new GitSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static bool ValidateIsValidPathFor(string? toString, string extension, bool checkFileExists = true)
    {
        if (string.IsNullOrEmpty(toString))
            return false;
        
        if (!toString.EndsWith(extension))
            return false;
        
        if (checkFileExists && !File.Exists(toString))
            return false;
        
        return true;
    }

    private static string Solution1SolutionPath => Path.Combine(StartDirectory, "TestsSolutions", "Solution1", "Solution1.sln");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(GitToSqlTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);
            
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");
            
            return directory;
        }
    }
}