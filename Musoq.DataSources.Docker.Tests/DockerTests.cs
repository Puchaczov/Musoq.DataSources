using System;
using System.Collections.Generic;
using Docker.DotNet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;
using Musoq.Tests.Common;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public class DockerTests
{
    [TestMethod]
    public void WhenContainersQueried_ShouldReturnValues()
    {
        var api = new Mock<IDockerApi>();
        
        api.Setup(f => f.ListContainersAsync())
            .ReturnsAsync(new List<ContainerListResponse>
            {
                new()
                {
                    ID = "1",
                    Names = new List<string> {"1"},
                    Image = "1",
                    ImageID = "1",
                    Command = "1",
                    Created = DateTime.MinValue,
                    State = "1",
                    Status = "1",
                    Ports = new List<Port> {new() {IP = "1", PrivatePort = 1, PublicPort = 1, Type = "1"}},
                    Labels = new Dictionary<string, string> {{"1", "1"}},
                    SizeRw = 1,
                    SizeRootFs = 1,
                    NetworkSettings = new SummaryNetworkSettings
                    {
                        Networks = new Dictionary<string, EndpointSettings>
                        {
                            {"1", new EndpointSettings {IPAddress = "1", MacAddress = "1"}}
                        }
                    },
                    Mounts = new List<MountPoint> {new() {Destination = "1", Driver = "1", Mode = "1", Name = "1", RW = true, Source = "1", Type = "1"}}
                }
            });
        
        var query = "select ID, Names, Image, ImageID, Command, Created, State, Status, Ports, Labels, SizeRw, SizeRootFs, NetworkSettings, Mounts, FlattenPorts from #docker.containers()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("1", table[0][0]);
        
        var names = (IList<string>) table[0][1];
        
        Assert.AreEqual(1, names.Count);
        Assert.AreEqual("1", names[0]);
        
        Assert.AreEqual("1", table[0][2]);
        Assert.AreEqual("1", table[0][3]);
        Assert.AreEqual("1", table[0][4]);
        Assert.AreEqual(DateTime.MinValue, table[0][5]);
        Assert.AreEqual("1", table[0][6]);
        Assert.AreEqual("1", table[0][7]);
        
        var ports = (IList<string>) table[0][8];
        
        Assert.AreEqual(1, ports.Count);
        Assert.AreEqual("1:1", ports[0]);
        
        var labels = (IDictionary<string, string>) table[0][9];
        
        Assert.AreEqual(1, labels.Count);
        Assert.AreEqual("1", labels["1"]);
        
        Assert.AreEqual(1L, table[0][10]);
        Assert.AreEqual(1L, table[0][11]);
        
        var networkSettings = (SummaryNetworkSettings) table[0][12];
        
        Assert.AreEqual(1, networkSettings.Networks.Count);
        Assert.AreEqual("1", networkSettings.Networks["1"].IPAddress);
        Assert.AreEqual("1", networkSettings.Networks["1"].MacAddress);
        
        var mounts = (IList<MountPoint>) table[0][13];
        
        Assert.AreEqual(1, mounts.Count);
        Assert.AreEqual("1", mounts[0].Destination);
        Assert.AreEqual("1", mounts[0].Driver);
        Assert.AreEqual("1", mounts[0].Mode);
        
        Assert.AreEqual("1", mounts[0].Name);
        Assert.AreEqual(true, mounts[0].RW);
        Assert.AreEqual("1", mounts[0].Source);
        Assert.AreEqual("1", mounts[0].Type);
        
        Assert.AreEqual("1:1", table[0][14]);
        
        api.Verify(f => f.ListContainersAsync(), Times.Once);
    }
    
    [TestMethod]
    public void WhenImagesQueried_ShouldReturnValues()
    {
        var api = new Mock<IDockerApi>();
        
        api.Setup(f => f.ListImagesAsync())
            .ReturnsAsync(new List<ImagesListResponse>
            {
                new()
                {
                    Created = DateTime.MinValue,
                    ID = "1",
                    ParentID = "1",
                    RepoDigests = new List<string> {"1"},
                    RepoTags = new List<string> {"1"},
                    SharedSize = 1,
                    Size = 1,
                    VirtualSize = 1
                }
            });
        
        var query = "select Created, ID, ParentID, RepoDigests, RepoTags, SharedSize, Size, VirtualSize from #docker.images()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(DateTime.MinValue, table[0][0]);
        Assert.AreEqual("1", table[0][1]);
        
        var repoDigests = (IList<string>) table[0][3];
        
        Assert.AreEqual(1, repoDigests.Count);
        Assert.AreEqual("1", repoDigests[0]);
        
        var repoTags = (IList<string>) table[0][4];
        
        Assert.AreEqual(1, repoTags.Count);
        Assert.AreEqual("1", repoTags[0]);
        
        Assert.AreEqual(1L, table[0][5]);
        Assert.AreEqual(1L, table[0][6]);
        Assert.AreEqual(1L, table[0][7]);
        
        api.Verify(f => f.ListImagesAsync(), Times.Once);
    }
    
    [TestMethod]
    public void WhenVolumesQueried_ShouldReturnValues()
    {
        var api = new Mock<IDockerApi>();
        
        api.Setup(f => f.ListVolumesAsync())
            .ReturnsAsync(new List<VolumeResponse>
            {
                new()
                {
                    CreatedAt = "now",
                    Driver = "1",
                    Labels = new Dictionary<string, string> {{"1", "1"}},
                    Mountpoint = "1",
                    Name = "1",
                    Options = new Dictionary<string, string> {{"1", "1"}},
                    Scope = "1"
                }
            });
        
        var query = "select CreatedAt, Driver, Labels, Mountpoint, Name, Options, Scope from #docker.volumes()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("now", table[0][0]);
        Assert.AreEqual("1", table[0][1]);
        
        var labels = (IDictionary<string, string>) table[0][2];
        
        Assert.AreEqual(1, labels.Count);
        Assert.AreEqual("1", labels["1"]);
        
        Assert.AreEqual("1", table[0][3]);
        Assert.AreEqual("1", table[0][4]);
        
        var options = (IDictionary<string, string>) table[0][5];
        
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("1", options["1"]);
        
        Assert.AreEqual("1", table[0][6]);
        
        api.Verify(f => f.ListVolumesAsync(), Times.Once);
    }
    
    [TestMethod]
    public void WhenNetworksQueries_ShouldReturnValues()
    {
        var api = new Mock<IDockerApi>();
        
        api.Setup(f => f.ListNetworksAsync())
            .ReturnsAsync(new List<NetworkResponse>
            {
                new()
                {
                    Created = DateTime.MinValue,
                    Driver = "1",
                    EnableIPv6 = true,
                    ID = "1",
                    Internal = true,
                    Labels = new Dictionary<string, string> {{"1", "1"}},
                    Name = "1",
                    Options = new Dictionary<string, string> {{"1", "1"}},
                    Scope = "1"
                }
            });
        
        var query = "select Created, Driver, EnableIPv6, ID, Internal, Labels, Name, Options, Scope from #docker.networks()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(DateTime.MinValue, table[0][0]);
        Assert.AreEqual("1", table[0][1]);
        Assert.AreEqual(true, table[0][2]);
        Assert.AreEqual("1", table[0][3]);
        Assert.AreEqual(true, table[0][4]);
        
        var labels = (IDictionary<string, string>) table[0][5];
        
        Assert.AreEqual(1, labels.Count);
        Assert.AreEqual("1", labels["1"]);
        
        Assert.AreEqual("1", table[0][6]);
        
        var options = (IDictionary<string, string>) table[0][7];
        
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("1", options["1"]);
        
        Assert.AreEqual("1", table[0][8]);
        
        api.Verify(f => f.ListNetworksAsync(), Times.Once);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, IDockerApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new DockerSchema(api));
        
        return InstanceCreator.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            mockSchemaProvider.Object, 
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>
                {
                    {"MUSOQ_AIRTABLE_API_KEY", "NOPE"},
                    {"MUSOQ_AIRTABLE_BASE_ID", "NOPE x2"}
                }}
            });
    }

    static DockerTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}