using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Converter;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetToSqlPlaygroundTests
{
    static NugetToSqlPlaygroundTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [Ignore]
    [TestMethod]
    public void Playground_WithTransitivePackages()
    {
        var query =
            "select p.Name, np.Id, np.Version, np.License, np.LicenseUrl, np.IsTransitive, np.TransitivityLevel from #csharp.solution('D:\\\\repos\\\\Musoq.Cloud\\\\src\\\\dotnet\\\\Musoq.Cloud.sln') sln cross apply sln.Projects p cross apply p.GetNugetPackages(true) np";

        var vm = CreateAndRunVirtualMachineWithResponse(query);
        var table = vm.Run();
    }

    [Ignore]
    [TestMethod]
    public void Playground_WithoutTransitivePackages()
    {
        var query =
            "select p.Name, np.Id, np.Version, np.License, np.LicenseUrl, np.IsTransitive, np.TransitivityLevel from #csharp.solution('D:\\\\repos\\\\Musoq.DataSources\\\\Musoq.DataSources.sln') sln cross apply sln.Projects p cross apply p.GetNugetPackages(false) np";

        var vm = CreateAndRunVirtualMachineWithResponse(query);
        var table = vm.Run();
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        LifecycleHooks.Initialize();
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new RoslynSchemaProvider((_, client) => new NuGetPropertiesResolver("https://localhost:7137", client)),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                {
                    0,
                    new Dictionary<string, string>
                    {
                        { "MUSOQ_SERVER_HTTP_ENDPOINT", "https://localhost:7137" }
                    }
                }
            },
            new DebuggerLoggerResolver());
    }

    private class DebuggerLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger()
        {
            var logger = new Mock<ILogger>();

            logger.Setup(f => f.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                    It.IsAny<Exception>(), It.IsAny<Func<object, Exception?, string>>()))
                .Callback((LogLevel _, EventId _, object state, Exception? exception,
                    Func<object, Exception?, string> formatter) =>
                {
                    var message = formatter(state, exception);
                    Debug.WriteLine(message);
                });

            return logger.Object;
        }

        public ILogger<T> ResolveLogger<T>()
        {
            var logger = new Mock<ILogger<T>>();

            logger.Setup(f => f.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                    It.IsAny<Exception>(), It.IsAny<Func<object, Exception?, string>>()))
                .Callback((LogLevel _, EventId _, object state, Exception? exception,
                    Func<object, Exception?, string> formatter) =>
                {
                    var message = formatter(state, exception);
                    Debug.WriteLine(message);
                });

            return logger.Object;
        }
    }
}