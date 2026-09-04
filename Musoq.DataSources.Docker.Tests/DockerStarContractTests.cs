using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Docker;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public sealed class DockerStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "containers",
            [],
            [],
            "select * from docker.containers()",
            [
                Column("ID", typeof(string)), Column("Image", typeof(string)), Column("ImageID", typeof(string)),
                Column("Command", typeof(string)), Column("Created", typeof(DateTime)), Column("SizeRw", typeof(long)),
                Column("SizeRootFs", typeof(long)), Column("State", typeof(string)), Column("Status", typeof(string)),
                Column("NetworkSettings", typeof(string)), Column("FlattenPorts", typeof(string))
            ],
            ["Names", "Ports", "Labels", "Mounts"]),
        new(
            "images",
            [],
            [],
            "select * from docker.images()",
            [
                Column("Containers", typeof(long)), Column("Created", typeof(DateTime)), Column("ID", typeof(string)),
                Column("ParentID", typeof(string)), Column("SharedSize", typeof(long)), Column("Size", typeof(long)),
                Column("VirtualSize", typeof(long))
            ],
            ["Labels", "RepoDigests", "RepoTags"]),
        new(
            "networks",
            [],
            [],
            "select * from docker.networks()",
            [
                Column("Name", typeof(string)), Column("ID", typeof(string)), Column("Created", typeof(DateTime)),
                Column("Scope", typeof(string)), Column("Driver", typeof(string)), Column("EnableIPv6", typeof(bool)),
                Column("IPAM", typeof(string)), Column("Internal", typeof(bool)), Column("Attachable", typeof(bool)),
                Column("Ingress", typeof(bool)), Column("ConfigFrom", typeof(string)), Column("ConfigOnly", typeof(bool))
            ],
            ["Containers", "Options", "Labels", "Peers", "Services"]),
        new(
            "volumes",
            [],
            [],
            "select * from docker.volumes()",
            [
                Column("CreatedAt", typeof(string)), Column("Driver", typeof(string)), Column("Mountpoint", typeof(string)),
                Column("Name", typeof(string)), Column("Scope", typeof(string)), Column("UsageData", typeof(string))
            ],
            ["Labels", "Options", "Status"])
    ];

    [TestMethod]
    public void EveryDockerConstructor_HasOneExactStarContract()
    {
        var api = CreateApi();
        var schema = new DockerSchema(api.Object);
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        foreach (var contract in Cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query, api.Object).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    [TestMethod]
    public void EveryDockerCollection_IsCrossApplyAddressable()
    {
        var api = CreateApi();

        var queries = new Dictionary<string, string>
        {
            ["container names"] = "select n.Value from docker.containers() c cross apply c.Names n",
            ["container ports"] = "select p.Value from docker.containers() c cross apply c.Ports p",
            ["container labels"] = "select l.Key, l.Value from docker.containers() c cross apply c.Labels l",
            ["container mounts"] = "select m.Value from docker.containers() c cross apply c.Mounts m",
            ["image labels"] = "select l.Key, l.Value from docker.images() i cross apply i.Labels l",
            ["image digests"] = "select d.Value from docker.images() i cross apply i.RepoDigests d",
            ["image tags"] = "select t.Value from docker.images() i cross apply i.RepoTags t",
            ["network containers"] = "select c.Key, c.Value from docker.networks() n cross apply n.Containers c",
            ["network options"] = "select o.Key, o.Value from docker.networks() n cross apply n.Options o",
            ["network labels"] = "select l.Key, l.Value from docker.networks() n cross apply n.Labels l",
            ["network peers"] = "select p.Value from docker.networks() n cross apply n.Peers p",
            ["network services"] = "select s.Key, s.Value from docker.networks() n cross apply n.Services s",
            ["volume labels"] = "select l.Key, l.Value from docker.volumes() v cross apply v.Labels l",
            ["volume options"] = "select o.Key, o.Value from docker.volumes() v cross apply v.Options o",
            ["volume status"] = "select s.Key, s.Value from docker.volumes() v cross apply v.Status s"
        };

        foreach (var pair in queries)
        {
            var result = Compile(pair.Value, api.Object).Run();
            if (result.Count != 1)
                throw new AssertFailedException($"Docker apply '{pair.Key}' returned {result.Count} rows.");
        }
    }

    private static Mock<IDockerApi> CreateApi()
    {
        var api = new Mock<IDockerApi>();
        api.Setup(value => value.ListContainersAsync()).ReturnsAsync(
        [
            new ContainerListResponse
            {
                ID = "container-id", Names = ["/container"], Image = "image", ImageID = "image-id",
                Command = "command", Created = DateTime.UnixEpoch, State = "running", Status = "Up",
                Ports = [new Port { PrivatePort = 80, PublicPort = 8080, Type = "tcp" }],
                Labels = new Dictionary<string, string> { ["container-label"] = "container-value" },
                SizeRw = 1, SizeRootFs = 2,
                NetworkSettings = new SummaryNetworkSettings(),
                Mounts = [new MountPoint { Type = "bind", Source = "source", Destination = "destination" }]
            }
        ]);
        api.Setup(value => value.ListImagesAsync()).ReturnsAsync(
        [
            new ImagesListResponse
            {
                Containers = 1, Created = DateTime.UnixEpoch, ID = "image-id", ParentID = "parent-id",
                Labels = new Dictionary<string, string> { ["image-label"] = "image-value" },
                RepoDigests = ["digest"], RepoTags = ["tag"], SharedSize = 2, Size = 3, VirtualSize = 4
            }
        ]);
        api.Setup(value => value.ListNetworksAsync()).ReturnsAsync(
        [
            new NetworkResponse
            {
                Name = "network", ID = "network-id", Created = DateTime.UnixEpoch, Scope = "local", Driver = "bridge",
                EnableIPv6 = true, IPAM = new IPAM(), Internal = false, Attachable = true, Ingress = false,
                ConfigFrom = new ConfigReference(), ConfigOnly = false,
                Containers = new Dictionary<string, EndpointResource>
                {
                    ["container-id"] = new EndpointResource { Name = "container", EndpointID = "endpoint", IPv4Address = "4", IPv6Address = "6" }
                },
                Options = new Dictionary<string, string> { ["network-option"] = "network-value" },
                Labels = new Dictionary<string, string> { ["network-label"] = "network-value" },
                Peers = [new PeerInfo { Name = "peer", IP = "peer-ip" }],
                Services = new Dictionary<string, ServiceInfo> { ["service"] = new ServiceInfo { VIP = "service-vip" } }
            }
        ]);
        api.Setup(value => value.ListVolumesAsync()).ReturnsAsync(
        [
            new VolumeResponse
            {
                CreatedAt = "created", Driver = "local", Labels = new Dictionary<string, string> { ["volume-label"] = "volume-value" },
                Mountpoint = "mountpoint", Name = "volume", Options = new Dictionary<string, string> { ["volume-option"] = "volume-value" },
                Scope = "local", Status = new Dictionary<string, object> { ["volume-status"] = "volume-value" }
            }
        ]);
        return api;
    }

    private static CompiledQuery Compile(string query, IDockerApi api)
    {
        var provider = new Moq.Mock<ISchemaProvider>();
        provider.Setup(value => value.GetSchema(It.IsAny<string>())).Returns(new DockerSchema(api));

        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            provider.Object,
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                [0] = new Dictionary<string, string>
                {
                    ["MUSOQ_AIRTABLE_API_KEY"] = "NOPE",
                    ["MUSOQ_AIRTABLE_BASE_ID"] = "NOPE x2"
                }
            });
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "docker-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}
