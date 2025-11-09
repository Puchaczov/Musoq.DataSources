using Docker.DotNet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public class DockerSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var dockerApiMock = new Mock<IDockerApi>();
        
        dockerApiMock
            .Setup(api => api.ListContainersAsync())
            .ReturnsAsync(new List<ContainerListResponse>());
        
        dockerApiMock
            .Setup(api => api.ListImagesAsync())
            .ReturnsAsync(new List<ImagesListResponse>());
        
        dockerApiMock
            .Setup(api => api.ListNetworksAsync())
            .ReturnsAsync(new List<NetworkResponse>());
        
        dockerApiMock
            .Setup(api => api.ListVolumesAsync())
            .ReturnsAsync(new List<VolumeResponse>());

        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new DockerSchema(dockerApiMock.Object));

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static DockerSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #docker";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have 1 column: Name");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(4, table.Count, "Should have 4 rows (4 unique methods)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "containers"), "Should contain 'containers' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "images"), "Should contain 'images' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "networks"), "Should contain 'networks' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "volumes"), "Should contain 'volumes' method once");
    }

    [TestMethod]
    public void DescContainers_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #docker.containers";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("containers", (string)row[0]);
    }

    [TestMethod]
    public void DescImages_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #docker.images";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("images", (string)row[0]);
    }

    [TestMethod]
    public void DescNetworks_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #docker.networks";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("networks", (string)row[0]);
    }

    [TestMethod]
    public void DescVolumes_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #docker.volumes";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("volumes", (string)row[0]);
    }

    [TestMethod]
    public void DescContainersWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #docker.containers()";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(ContainerListResponse.ID),
            nameof(ContainerListResponse.Names),
            nameof(ContainerListResponse.Image),
            nameof(ContainerListResponse.ImageID),
            nameof(ContainerListResponse.Command),
            nameof(ContainerListResponse.Created),
            nameof(ContainerListResponse.Ports),
            nameof(ContainerListResponse.SizeRw),
            nameof(ContainerListResponse.SizeRootFs),
            nameof(ContainerListResponse.Labels),
            nameof(ContainerListResponse.State),
            nameof(ContainerListResponse.Status),
            nameof(ContainerListResponse.NetworkSettings),
            nameof(ContainerListResponse.Mounts),
            "FlattenPorts"
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescImagesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #docker.images()";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(ImagesListResponse.Containers),
            nameof(ImagesListResponse.Created),
            nameof(ImagesListResponse.ID),
            nameof(ImagesListResponse.Labels),
            nameof(ImagesListResponse.ParentID),
            nameof(ImagesListResponse.RepoDigests),
            nameof(ImagesListResponse.RepoTags),
            nameof(ImagesListResponse.SharedSize),
            nameof(ImagesListResponse.Size),
            nameof(ImagesListResponse.VirtualSize)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescNetworksWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #docker.networks()";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(NetworkResponse.Name),
            nameof(NetworkResponse.ID),
            nameof(NetworkResponse.Created),
            nameof(NetworkResponse.Scope),
            nameof(NetworkResponse.Driver),
            nameof(NetworkResponse.EnableIPv6),
            nameof(NetworkResponse.IPAM),
            nameof(NetworkResponse.Internal),
            nameof(NetworkResponse.Attachable),
            nameof(NetworkResponse.Ingress),
            nameof(NetworkResponse.ConfigFrom),
            nameof(NetworkResponse.ConfigOnly),
            nameof(NetworkResponse.Containers),
            nameof(NetworkResponse.Options),
            nameof(NetworkResponse.Labels),
            nameof(NetworkResponse.Peers),
            nameof(NetworkResponse.Services)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescVolumesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #docker.volumes()";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(VolumeResponse.CreatedAt),
            nameof(VolumeResponse.Driver),
            nameof(VolumeResponse.Labels),
            nameof(VolumeResponse.Mountpoint),
            nameof(VolumeResponse.Name),
            nameof(VolumeResponse.Options),
            nameof(VolumeResponse.Scope),
            nameof(VolumeResponse.Status),
            nameof(VolumeResponse.UsageData)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #docker.unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            Assert.Fail("Should have thrown an exception for unknown method");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.IsTrue(
                message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase),
                $"Error message should mention the unknown method. Got: {message}");
            Assert.IsTrue(
                message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase),
                $"Error message should be helpful. Got: {message}");
        }
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc #docker";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescContainersNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #docker.containers";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #docker.containers()";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("containers", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1);
        var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(columnNames.Contains(nameof(ContainerListResponse.ID)));
        Assert.IsTrue(columnNames.Contains(nameof(ContainerListResponse.Image)));
    }
}
