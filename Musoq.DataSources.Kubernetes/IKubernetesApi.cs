using k8s.Models;

namespace Musoq.DataSources.Kubernetes;

internal interface IKubernetesApi
{
    V1PodList ListPodsForAllNamespaces();
    
    Stream ReadNamespacedPodLogs(string podName, string namespaceName, string containerName);
    
    V1ServiceList ListServicesForAllNamespaces();
    
    V1DeploymentList ListDeploymentsForAllNamespaces();
    
    V1ReplicaSetList ListReplicaSetsForAllNamespaces();
    
    V1NodeList ListNodes();
    
    V1SecretList ListSecretsForAllNamespaces();
    
    V1ConfigMapList ListConfigMapsForAllNamespaces();
    
    V1IngressList ListIngressesForAllNamespaces();
    
    V1PersistentVolumeList ListPersistentVolumes();
    
    V1PersistentVolumeClaimList ListPersistentVolumeClaimsForAllNamespaces();
    
    V1JobList ListJobsForAllNamespaces();
    
    V1CronJobList ListCronJobsForAllNamespaces();
    
    V1DaemonSetList ListDaemonSetsForAllNamespaces();
    
    V1StatefulSetList ListStatefulSetsForAllNamespaces();
    
    Corev1EventList ListEvents();
    
    Corev1EventList ListNamespacedEvents(string @namespace);
}