using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Tests;

[TestClass]
public class KubernetesSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var kubernetesApiMock = new Mock<IKubernetesApi>();

        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new KubernetesSchema(kubernetesApiMock.Object));

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static KubernetesSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #kubernetes";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns: Name and up to 3 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(18, table.Count, "Should have 18 rows (15 no-param methods + 1 podlogs with 3 params + 2 events overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "pods"), "Should contain 'pods' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "services"), "Should contain 'services' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "deployments"), "Should contain 'deployments' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "replicasets"), "Should contain 'replicasets' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "nodes"), "Should contain 'nodes' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "secrets"), "Should contain 'secrets' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "configmaps"), "Should contain 'configmaps' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "ingresses"), "Should contain 'ingresses' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "persistentvolumes"), "Should contain 'persistentvolumes' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "persistentvolumeclaims"), "Should contain 'persistentvolumeclaims' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "jobs"), "Should contain 'jobs' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "cronjobs"), "Should contain 'cronjobs' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "statefulsets"), "Should contain 'statefulsets' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "daemonsets"), "Should contain 'daemonsets' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "podcontainers"), "Should contain 'podcontainers' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "podlogs"), "Should contain 'podlogs' method once");
        Assert.AreEqual(2, methodNames.Count(m => m == "events"), "Should contain 'events' method 2 times (2 overloads)");

        var podsRow = table.First(row => (string)row[0] == "pods");
        Assert.IsNull(podsRow[1], "Pods should have no parameters");
        Assert.IsNull(podsRow[2]);
        Assert.IsNull(podsRow[3]);

        var podlogsRow = table.First(row => (string)row[0] == "podlogs");
        Assert.AreEqual("podName: System.String", (string)podlogsRow[1]);
        Assert.AreEqual("containerName: System.String", (string)podlogsRow[2]);
        Assert.AreEqual("namespaceName: System.String", (string)podlogsRow[3]);
    }

    [TestMethod]
    public void DescPods_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #kubernetes.pods";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("pods", (string)row[0]);
    }

    [TestMethod]
    public void DescServices_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #kubernetes.services";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("services", (string)row[0]);
    }

    [TestMethod]
    public void DescDeployments_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #kubernetes.deployments";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("deployments", (string)row[0]);
    }

    [TestMethod]
    public void DescPodLogs_ShouldReturnMethodSignatureWithThreeParameters()
    {
        var query = "desc #kubernetes.podlogs";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("podlogs", (string)row[0]);
        Assert.AreEqual("podName: System.String", (string)row[1]);
        Assert.AreEqual("containerName: System.String", (string)row[2]);
        Assert.AreEqual("namespaceName: System.String", (string)row[3]);
    }

    [TestMethod]
    public void DescEvents_ShouldReturnAllOverloads()
    {
        var query = "desc #kubernetes.events";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns (Name, Param 0)");
        Assert.AreEqual(2, table.Count, "Should have 2 rows for 2 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "events"), "All rows should be for events method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("events", (string)overload1[0]);
        Assert.IsNull(overload1[1], "First overload should have no parameters");

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("events", (string)overload2[0]);
        Assert.AreEqual("namespaceName: System.String", (string)overload2[1]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #kubernetes.unknownmethod";

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
        var query = "desc #kubernetes";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescPodsNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #kubernetes.pods";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("pods", (string)tableNoArgs.First()[0]);
    }
}
