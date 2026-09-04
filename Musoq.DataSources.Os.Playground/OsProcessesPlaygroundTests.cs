using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Playground.Components;

namespace Musoq.DataSources.Os.Playground;

[TestClass]
public sealed class OsProcessesPlaygroundTests : PlaygroundTestsBase
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BareSchemaName_ShouldExposeProcessName()
    {
        RunProbe(
            TestContext,
            "select ProcessName from os.processes()",
            "ProcessName");
    }

    [TestMethod]
    public void HashSchemaName_ShouldExposeProcessName()
    {
        RunProbe(
            TestContext,
            "select ProcessName from os.processes()",
            "ProcessName");
    }

    [TestMethod]
    public void HashSchemaName_ProcessNameControl_ShouldRun()
    {
        RunProbe(
            TestContext,
            "select ProcessName from os.processes()",
            "ProcessName");
    }
}
