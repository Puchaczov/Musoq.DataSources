using k8s;
using k8s.Models;

namespace Musoq.DataSources.Kubernetes;

internal class KubernetesApi : IKubernetesApi
{
    private readonly k8s.Kubernetes _client;
    
    public KubernetesApi(k8s.Kubernetes client)
    {
        _client = client;
    }
    
    public V1PodList ListPodsForAllNamespaces()
    {
        return _client.ListPodForAllNamespaces();
    }

    public Stream ReadNamespacedPodLogs(string podName, string namespaceName, string containerName)
    {
        return _client.ReadNamespacedPodLog(podName, namespaceName, containerName, false);
    }

    public V1ServiceList ListServicesForAllNamespaces()
    {
        return _client.ListServiceForAllNamespaces();
    }
    
    public V1DeploymentList ListDeploymentsForAllNamespaces()
    {
        return _client.ListDeploymentForAllNamespaces();
    }
    
    public V1ReplicaSetList ListReplicaSetsForAllNamespaces()
    {
        return _client.ListReplicaSetForAllNamespaces();
    }
    
    public V1NodeList ListNodes()
    {
        return _client.ListNode();
    }

    public V1SecretList ListSecretsForAllNamespaces()
    {
        return _client.ListSecretForAllNamespaces();
    }

    public V1ConfigMapList ListConfigMapsForAllNamespaces()
    {
        return _client.ListConfigMapForAllNamespaces();
    }

    public V1IngressList ListIngressesForAllNamespaces()
    {
        return _client.ListIngressForAllNamespaces();
    }

    public V1PersistentVolumeList ListPersistentVolumes()
    {
        return _client.ListPersistentVolume();
    }

    public V1PersistentVolumeClaimList ListPersistentVolumeClaimsForAllNamespaces()
    {
        return _client.ListPersistentVolumeClaimForAllNamespaces();
    }

    public V1JobList ListJobsForAllNamespaces()
    {
        return _client.ListJobForAllNamespaces();
    }

    public V1CronJobList ListCronJobsForAllNamespaces()
    {
        return _client.ListCronJobForAllNamespaces();
    }

    public V1DaemonSetList ListDaemonSetsForAllNamespaces()
    {
        return _client.ListDaemonSetForAllNamespaces();
    }

    public V1StatefulSetList ListStatefulSetsForAllNamespaces()
    {
        return _client.ListStatefulSetForAllNamespaces();
    }

    public Corev1EventList ListEvents()
    {
        return CoreV1OperationsExtensions.ListEventForAllNamespaces(_client);
    }

    public Corev1EventList ListNamespacedEvents(string @namespace)
    {
        return CoreV1OperationsExtensions.ListNamespacedEvent(_client, @namespace);
    }
}