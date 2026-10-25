using Docker.DotNet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Docker.Images;
using Musoq.DataSources.Docker.Networks;
using Musoq.DataSources.Docker.Volumes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void AllDaemonStringColumns_MatchCore()
    {
        var api = new Mock<IDockerApi>(MockBehavior.Strict);
        api.Setup(a => a.ListContainersAsync()).ReturnsAsync(new[] { new ContainerListResponse { ID = "alpha", Image = "Alpha", State = "running" }, new ContainerListResponse { ID = "beta", Image = "Beta", State = "stopped" } });
        api.Setup(a => a.ListImagesAsync()).ReturnsAsync(new[] { new ImagesListResponse { ID = "alpha", ParentID = "beta" } });
        api.Setup(a => a.ListNetworksAsync()).ReturnsAsync(new[] { new NetworkResponse { ID = "alpha", Name = "Alpha", Driver = "bridge" } });
        api.Setup(a => a.ListVolumesAsync()).ReturnsAsync(new[] { new VolumeResponse { Name = "Alpha", Driver = "local", Mountpoint = "/alpha" } });
        var schema = new DockerSchema(api.Object);
        var columns = PatternContractVerifier.Verify<ContainerEntity, EmptySourceArguments>(schema, "containers", SourceArguments.Empty, []);
        columns += PatternContractVerifier.Verify<ImageEntity, EmptySourceArguments>(schema, "images", SourceArguments.Empty, []);
        columns += PatternContractVerifier.Verify<NetworkEntity, EmptySourceArguments>(schema, "networks", SourceArguments.Empty, []);
        columns += PatternContractVerifier.Verify<VolumeEntity, EmptySourceArguments>(schema, "volumes", SourceArguments.Empty, []);
        PatternContractVerifier.VerifySource(schema, "containers", [], context => new ContainersSource(api.Object, context));
        PatternContractVerifier.VerifySource(schema, "images", [], context => new ImagesSource(api.Object, context));
        PatternContractVerifier.VerifySource(schema, "networks", [], context => new NetworksSource(api.Object, context));
        PatternContractVerifier.VerifySource(schema, "volumes", [], context => new VolumesSource(api.Object, context));
        Assert.AreEqual(8 + 2 * PatternContractVerifier.CasesPerColumn * columns, api.Invocations.Count);
    }
}
