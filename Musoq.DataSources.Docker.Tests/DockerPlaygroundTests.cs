using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Docker.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Docker.Tests;

[Ignore]
[TestClass]
public class DockerPlaygroundTests
{
    static DockerPlaygroundTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void ContainersPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #docker.containers()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void ContainersPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #docker.containers()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void ImagesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #docker.images()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void ImagesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #docker.images()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void NetworksPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #docker.networks()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void NetworksPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #docker.networks()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void VolumesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #docker.volumes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void VolumesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #docker.volumes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void JoinContainersWithImages_ShouldPass()
    {
        const string query =
            "select containers.ID, containers.Command, containers.Status, images.ID, images.Size from #docker.containers() containers inner join #docker.images() images on containers.ImageID = images.ID";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new PlaygroundSchemaProvider(),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                { 0, new Dictionary<string, string>() },
                { 1, new Dictionary<string, string>() }
            });
    }
}