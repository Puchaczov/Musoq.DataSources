using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Playground.Components;

namespace Musoq.DataSources.Os.Playground;

[TestClass]
public sealed class OsProcessesPlaygroundTests : PlaygroundTestsBase
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BareSchemaName_ShouldExposeProcessNameAndProcessorAffinity()
    {
        RunProbe(
            TestContext,
            "select ProcessName, ProcessorAffinity from os.processes()",
            "ProcessName",
            "ProcessorAffinity");
    }

    [TestMethod]
    public void HashSchemaName_ShouldExposeProcessNameAndProcessorAffinity()
    {
        RunProbe(
            TestContext,
            "select ProcessName, ProcessorAffinity from #os.processes()",
            "ProcessName",
            "ProcessorAffinity");
    }

    [TestMethod]
    public void HashSchemaName_ProcessNameControl_ShouldRun()
    {
        RunProbe(
            TestContext,
            "select ProcessName from #os.processes()",
            "ProcessName");
    }
}
