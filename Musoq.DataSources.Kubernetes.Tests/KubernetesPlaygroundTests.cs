using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Kubernetes.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Kubernetes.Tests;

[Ignore]
[TestClass]
public class KubernetesPlaygroundTests
{
    [TestMethod]
    public void DeploymentsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.deployments()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void DeploymentsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.deployments()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.pods()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodsPlayground_ShouldBeIgnored()
    {
        const string query = "select *, GetLabelOrDefault('name') from #kubernetes.pods()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ServicesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.services()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ServicesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.services()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void SecretsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.secrets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void SecretsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.secrets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ConfigMapsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.configmaps()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ConfigMapsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.configmaps()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void IngressesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.ingresses()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void IngressesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.ingresses()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PersistentVolumesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.persistentvolumes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PersistentVolumesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.persistentvolumes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PersistentVolumeClaimsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.persistentvolumeclaims()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PersistentVolumeClaimsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.persistentvolumeclaims()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void JobsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.jobs()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void JobsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.jobs()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void CronJobsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.cronjobs()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void CronJobsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.cronjobs()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ReplicaSetsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.replicasets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void ReplicaSetsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.replicasets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void DaemonSetsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.daemonsets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void DaemonSetsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.daemonsets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void StatefulSetsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.statefulsets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void StatefulSetsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.statefulsets()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void NodesPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.nodes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void NodesPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.nodes()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodContainersPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.podcontainers()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodContainersPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.podcontainers()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void DeploymentPodsAndContainersJoined_ShouldBeIgnored()
    {
        const string query = @"
select 
    deployments.Name, 
    deployments.Namespace, 
    pods.GetLabelOrDefault('name') as PodName, 
    containers.GetLabelOrDefault('name') as ContainerName
from #kubernetes.deployments() deployments 
    inner join #kubernetes.pods() pods on deployments.Name = pods.GetLabelOrDefault('name') 
    inner join #kubernetes.podcontainers() containers on pods.GetLabelOrDefault('name') = containers.GetLabelOrDefault('name')
    where deployments.Namespace = 'musoq'";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void DeploymentPodsAndContainersJoinedWithWhere_ShouldBeIgnored()
    {
        const string query = @"
select 
    deployments.Name, 
    deployments.Namespace, 
    containers.SecurityContext.RunAsUser
from #kubernetes.deployments() deployments 
    inner join #kubernetes.pods() pods on deployments.Name = pods.GetLabelOrDefault('name') 
    inner join #kubernetes.podcontainers() containers on pods.GetLabelOrDefault('name') = containers.GetLabelOrDefault('name')
    where deployments.Namespace = 'musoq' and containers.SecurityContext is not null";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query);
        
        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodLogsPlaygroundDesc_ShouldBeIgnored()
    {
        const string query = "desc #kubernetes.podlogs()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void PodLogsPlayground_ShouldBeIgnored()
    {
        const string query = "select Name, ContainerName, Namespace from #kubernetes.podcontainers() where Namespace = 'musoq' and ContainerName = 'rabbitmq'";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
        
        var query2 = $"select * from #kubernetes.podlogs('{table[0][0]}', '{table[0][1]}', '{table[0][2]}')";
        
        var vm2 = CreateAndRunVirtualMachineWithResponse(query2);
        
        var table2 = vm2.Run();
    }
    
    [TestMethod]
    public void EventsPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.events()";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }
    
    [TestMethod]
    public void EventsNamespacedPlayground_ShouldBeIgnored()
    {
        const string query = "select * from #kubernetes.events('musoq')";

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
                {0, new Dictionary<string, string>()},
                {1, new Dictionary<string, string>()},
                {2, new Dictionary<string, string>()}
            });
    }

    static KubernetesPlaygroundTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}