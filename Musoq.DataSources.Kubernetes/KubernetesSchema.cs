using System.Text;
using k8s;
using Musoq.DataSources.Kubernetes.Configmaps;
using Musoq.DataSources.Kubernetes.CronJobs;
using Musoq.DataSources.Kubernetes.DaemonSets;
using Musoq.DataSources.Kubernetes.Deployments;
using Musoq.DataSources.Kubernetes.Ingresses;
using Musoq.DataSources.Kubernetes.Jobs;
using Musoq.DataSources.Kubernetes.Nodes;
using Musoq.DataSources.Kubernetes.PersistentVolumeClaims;
using Musoq.DataSources.Kubernetes.PersistentVolumes;
using Musoq.DataSources.Kubernetes.PodContainers;
using Musoq.DataSources.Kubernetes.Pods;
using Musoq.DataSources.Kubernetes.ReplicaSets;
using Musoq.DataSources.Kubernetes.SecretData;
using Musoq.DataSources.Kubernetes.Secrets;
using Musoq.DataSources.Kubernetes.Services;
using Musoq.DataSources.Kubernetes.StatefulSets;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Kubernetes;

/// <description>
/// Provides schema to work with Kubernetes.
/// </description>
/// <short-description>
/// Provides schema to work with Kubernetes.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class KubernetesSchema : SchemaBase
{
    private const string SchemaName = "kubernetes";
    
    private const string PodsTableName = "pods";
    private const string ServicesTableName = "services";
    private const string DeploymentsTableName = "deployments";
    private const string ReplicaSetsTableName = "replicasets";
    private const string SecretsDataTableName = "secretsdata";
    private const string NodesTableName = "nodes";
    private const string SecretsTableName = "secrets";
    private const string ConfigMapsTableName = "configmaps";
    private const string IngressesTableName = "ingresses";
    private const string PersistentVolumesTableName = "persistentvolumes";
    private const string PersistentVolumeClaimsTableName = "persistentvolumeclaims";
    private const string JobsTableName = "jobs";
    private const string CronJobsTableName = "cronjobs";
    private const string StatefulSetsTableName = "statefulsets";
    private const string DaemonSetsTableName = "daemonsets";
    private const string PodContainersTableName = "podcontainers";
    
    private readonly Func<RuntimeContext, object[], IKubernetesApi> _clientFactory;
    
    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.deployments()
    ///                 </from>
    ///                 <description>Enumerate deployments</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="CreationTimestamp" type="DateTime?">CreationTimestamp of the DeploymentEntity</column>
    ///                     <column name="Generation" type="long?">Generation of the DeploymentEntity</column>
    ///                     <column name="ResourceVersion" type="string">ResourceVersion string</column>
    ///                     <column name="Images" type="string">Image used within deployment</column>
    ///                     <column name="ImagePullPolicies" type="string">ImagePullPolicies used within deployment</column>
    ///                     <column name="RestartPolicy" type="string">RestartPolicy string</column>
    ///                     <column name="ContainersNames" type="string">Names of containers used within deployment</column>
    ///                     <column name="Statuses" type="string">Statuses of depl</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                         #kubernetes.pods()
    ///                 </from>
    ///                 <description>Enumerate pods</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Type" type="string">Type string</column>
    ///                     <column name="PF" type="string">PF string</column>
    ///                     <column name="Ready" type="string">Ready string</column>
    ///                     <column name="Restarts" type="string">Restarts string</column>
    ///                     <column name="Status" type="string">Status string</column>
    ///                     <column name="Cpu" type="string">Cpu string</column>
    ///                     <column name="Memory" type="string">Memory string</column>
    ///                     <column name="IP" type="string">IP string</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.services()
    ///                 </from>
    ///                 <description>Enumerates services</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Type" type="string">Service type</column>
    ///                     <column name="ClusterIP" type="string">Cluster IP</column>
    ///                     <column name="ExternalIP" type="string">External IP</column>
    ///                     <column name="Ports" type="string">Ports used</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                         #kubernetes.nodes()
    ///                 </from>
    ///                 <description>Enumerates nodes</description>
    ///                 <columns>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Status" type="string">Status string</column>
    ///                     <column name="Roles" type="string">Roles string</column>
    ///                     <column name="Age" type="DateTime?">Age of the NodeEntity</column>
    ///                     <column name="Version" type="string">Version string</column>
    ///                     <column name="Kernel" type="string">Kernel string</column>
    ///                     <column name="OS" type="string">OS string</column>
    ///                     <column name="Architecture" type="string">Architecture string</column>
    ///                     <column name="ContainerRuntime" type="string">Container Runtime string</column>
    ///                     <column name="Cpu" type="string">CPU string</column>
    ///                     <column name="Memory" type="string">Memory string</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.configmaps()
    ///                 </from>
    ///                 <description>Enumerate configmaps</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Age" type="DateTime?">Age</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.cronJobs()
    ///                 </from>
    ///                 <description>Enumerate cron jobs</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Schedule" type="string">Dictionary of the schedule</column>
    ///                     <column name="Active" type="bool">Flag indicating if the job is active</column>
    ///                     <column name="LastScheduleTime" type="DateTime?">Last schedule time of the job</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.daemonSets()
    ///                 </from>
    ///                 <description>Enumerate daemonsets</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Desired" type="int">Desired number of entities</column>
    ///                     <column name="Current" type="int">Current number of entities</column>
    ///                     <column name="Ready" type="int">Number of ready entities</column>
    ///                     <column name="UpToDate" type="int?">Number of up-to-date entities</column>
    ///                     <column name="Available" type="int?">Number of available entities</column>
    ///                     <column name="Age" type="DateTime?">Age of the entity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.jobs()
    ///                 </from>
    ///                 <description>Enumerate jobs</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Completions" type="int">Number of completions</column>
    ///                     <column name="Duration" type="TimeSpan?">Duration of job execution</column>
    ///                     <column name="Images" type="string">Images string</column>
    ///                     <column name="Age" type="DateTime?">Age of the job</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.persistentVolumeClaims()
    ///                 </from>
    ///                 <description>Enumerate persistent volume claims</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Status" type="string">Status string</column>
    ///                     <column name="Volume" type="string">Volume string</column>
    ///                     <column name="Capacity" type="string">Capacity string</column>
    ///                     <column name="Age" type="DateTime?">Age of the PersistentVolumeClaimsEntity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.persistentVolumes()
    ///                 </from>
    ///                 <description>Enumerate persistent volumes</description>
    ///                 <columns>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Capacity" type="IDictionary&lt;string, ResourceQuantity&gt;">Dictionary of capacities</column>
    ///                     <column name="AccessModes" type="string">AccessModes string</column>
    ///                     <column name="ReclaimPolicy" type="string">Reclaim policy string</column>
    ///                     <column name="Status" type="string">Status string</column>
    ///                     <column name="Claim" type="string">Claim string</column>
    ///                     <column name="StorageClass" type="string">StorageClass string</column>
    ///                     <column name="Reason" type="string">Reason string</column>
    ///                     <column name="Age" type="DateTime?">Age of the PersistentVolumeEntity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.replicaSets()
    ///                 </from>
    ///                 <description>Enumerate replicasets</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Desired" type="int?">Desired number of replicas</column>
    ///                     <column name="Current" type="string">Current string</column>
    ///                     <column name="ReadyAge" type="DateTime?">Age when the ReplicaSetEntity is ready</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.secretsData()
    ///                 </from>
    ///                 <description>Enumerate secrets data</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Key" type="string">Key string</column>
    ///                     <column name="Value" type="byte[]">Value byte array</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.secrets()
    ///                 </from>
    ///                 <description>Enumerate secrets</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Type" type="string">Type string</column>
    ///                     <column name="Age" type="DateTime?">Age of the SecretEntity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.statefulsets()
    ///                 </from>
    ///                 <description>Enumerate statefulsets</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="Replicas" type="int?">Number of replicas</column>
    ///                     <column name="Age" type="DateTime?">Age of the StatefulSetEntity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="true">Airtable API key</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.podcontainers()
    ///                 </from>
    ///                 <description>Enumerate pod containers</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="ContainerName" type="string">Container name string</column>
    ///                     <column name="Image" type="string">Image string</column>
    ///                     <column name="ImagePullPolicy" type="string">Image pull policy string</column>
    ///                     <column name="Age" type="DateTime?">Container age</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public KubernetesSchema() 
        : base(SchemaName, CreateLibrary())
    {
        _clientFactory = (context, parameters) =>
        {
            switch (parameters.Length)
            {
                case 1 when parameters[0] is string config:
                {
                    var clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigFile(config);
                    var client = new k8s.Kubernetes(clientConfiguration);
                    return new KubernetesApi(client);
                }
                case 0 when context.EnvironmentVariables.ContainsKey("MUSOQ_KUBERNETES_CONFIG_FILE"):
                {
                    var fileContent = context.EnvironmentVariables["MUSOQ_KUBERNETES_CONFIG_FILE"];
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
                    var clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
                    var client = new k8s.Kubernetes(clientConfiguration);
            
                    return new KubernetesApi(client);
                }
                case 0:
                {
                    var clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    var client = new k8s.Kubernetes(clientConfiguration);
            
                    return new KubernetesApi(client);
                }
                default:
                    throw new NotSupportedException("Only one parameter is supported. It should be a path to Kubernetes config file.");
            }
        };
    }
    
    internal KubernetesSchema(IKubernetesApi client) 
        : base(SchemaName, CreateLibrary())
    {
        _clientFactory = (context, parameters) => client;
    }

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            PodsTableName => new PodsTable(),
            ServicesTableName => new ServicesTable(),
            DeploymentsTableName => new DeploymentsTable(),
            ReplicaSetsTableName => new ReplicaSetsTable(),
            SecretsDataTableName => new SecretsDataTable(),
            NodesTableName => new NodesTable(),
            SecretsTableName => new SecretsTable(),
            ConfigMapsTableName => new ConfigmapsTable(),
            IngressesTableName => new IngressesTable(),
            PersistentVolumesTableName => new PersistentVolumesTable(),
            PodContainersTableName => new PodContainersTable(),
            PersistentVolumeClaimsTableName => new PersistentVolumeClaimsTable(),
            JobsTableName => new JobsTable(),
            CronJobsTableName => new CronJobsTable(),
            DaemonSetsTableName => new DaemonSetsTable(),
            StatefulSetsTableName => new StatefulSetsTable(),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }
    
    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        var client = _clientFactory(runtimeContext, parameters);

        return name.ToLowerInvariant() switch
        {
            PodsTableName => new PodsSource(client),
            ServicesTableName => new ServicesSource(client),
            DeploymentsTableName => new DeploymentsSource(client),
            ReplicaSetsTableName => new ReplicaSetsSource(client),
            SecretsDataTableName => new SecretsDataSource(client),
            NodesTableName => new NodesSource(client),
            SecretsTableName => new SecretsSource(client),
            ConfigMapsTableName => new ConfigmapsSource(client),
            IngressesTableName => new IngressesSource(client),
            PersistentVolumesTableName => new PersistentVolumesSource(client),
            PodContainersTableName => new PodContainersSource(client),
            PersistentVolumeClaimsTableName => new PersistentVolumeClaimsSource(client),
            JobsTableName => new JobsSource(client),
            CronJobsTableName => new CronJobsSource(client),
            DaemonSetsTableName => new DaemonSetsSource(client),
            StatefulSetsTableName => new StatefulSetsSource(client),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new KubernetesLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}