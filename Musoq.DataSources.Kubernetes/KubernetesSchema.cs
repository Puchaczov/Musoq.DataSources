using System.Text;
using k8s;
using Musoq.DataSources.Kubernetes.Configmaps;
using Musoq.DataSources.Kubernetes.CronJobs;
using Musoq.DataSources.Kubernetes.DaemonSets;
using Musoq.DataSources.Kubernetes.Deployments;
using Musoq.DataSources.Kubernetes.Events;
using Musoq.DataSources.Kubernetes.Ingresses;
using Musoq.DataSources.Kubernetes.Jobs;
using Musoq.DataSources.Kubernetes.Nodes;
using Musoq.DataSources.Kubernetes.PersistentVolumeClaims;
using Musoq.DataSources.Kubernetes.PersistentVolumes;
using Musoq.DataSources.Kubernetes.PodContainers;
using Musoq.DataSources.Kubernetes.PodLogs;
using Musoq.DataSources.Kubernetes.Pods;
using Musoq.DataSources.Kubernetes.ReplicaSets;
using Musoq.DataSources.Kubernetes.Secrets;
using Musoq.DataSources.Kubernetes.Services;
using Musoq.DataSources.Kubernetes.StatefulSets;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

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
    private const string PodLogsTableName = "podlogs";
    private const string EventsTableName = "events";
    
    private readonly Func<RuntimeContext, object[], IKubernetesApi> _clientFactory;
    
    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.services()
    ///                 </from>
    ///                 <description>Enumerates services</description>
    ///                 <columns>
    ///                     <column name="Metadata" type="V1ObjectMeta">Metadata of the ServiceEntity</column>
    ///                     <column name="Spec" type="V1ServiceSpec">Spec of the ServiceEntity</column>
    ///                     <column name="Kind" type="string">Kind string</column>
    ///                     <column name="Status" type="V1ServiceStatus">Status of the ServiceEntity</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
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
    ///     <virtual-constructor>
    ///         <virtual-param>Pod Name</virtual-param>
    ///         <virtual-param>Container name</virtual-param>
    ///         <virtual-param>Namespace</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.podlogs(string podName, string string containerName, string namespace)
    ///                 </from>
    ///                 <description>Enumerate pod containers</description>
    ///                 <columns>
    ///                     <column name="Namespace" type="string">Namespace string</column>
    ///                     <column name="Name" type="string">Name string</column>
    ///                     <column name="ContainerName" type="string">Container name string</column>
    ///                     <column name="Line" type="string">Line string</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.events()
    ///                 </from>
    ///                 <description>Enumerate events</description>
    ///                 <columns>
    ///                     <column name="Action" type="string">Action string</column>
    ///                     <column name="ApiVersion" type="string">ApiVersion string</column>
    ///                     <column name="Count" type="int?">Count of the event</column>
    ///                     <column name="EventTime" type="DateTime?">EventTime of the event</column>
    ///                     <column name="FirstTimestamp" type="DateTime?">FirstTimestamp of the event</column>
    ///                     <column name="InvolvedObject" type="V1ObjectReference">InvolvedObject of the event</column>
    ///                     <column name="Kind" type="string">Kind string</column>
    ///                     <column name="LastTimestamp" type="DateTime?">LastTimestamp of the event</column>
    ///                     <column name="Message" type="string">Message string</column>
    ///                     <column name="Reason" type="string">Reason string</column>
    ///                     <column name="Related" type="V1ObjectReference">Related of the event</column>
    ///                     <column name="ReportingComponent" type="string">ReportingComponent string</column>
    ///                     <column name="ReportingInstance" type="string">ReportingInstance string</column>
    ///                     <column name="Series" type="Corev1EventSeries">Series of the event</column>
    ///                     <column name="Source" type="V1EventSource">Source of the event</column>
    ///                     <column name="Type" type="string">Type string</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="MUSOQ_KUBERNETES_CONFIG_FILE" isRequired="false">Kubernetes config file</environmentVariable>
    ///                     </environmentVariables>
    ///                        #kubernetes.events(string namespace)
    ///                 </from>
    ///                 <description>Enumerate events</description>
    ///                 <columns>
    ///                     <column name="Action" type="string">Action string</column>
    ///                     <column name="ApiVersion" type="string">ApiVersion string</column>
    ///                     <column name="Count" type="int?">Count of the event</column>
    ///                     <column name="EventTime" type="DateTime?">EventTime of the event</column>
    ///                     <column name="FirstTimestamp" type="DateTime?">FirstTimestamp of the event</column>
    ///                     <column name="InvolvedObject" type="V1ObjectReference">InvolvedObject of the event</column>
    ///                     <column name="Kind" type="string">Kind string</column>
    ///                     <column name="LastTimestamp" type="DateTime?">LastTimestamp of the event</column>
    ///                     <column name="Message" type="string">Message string</column>
    ///                     <column name="Reason" type="string">Reason string</column>
    ///                     <column name="Related" type="V1ObjectReference">Related of the event</column>
    ///                     <column name="ReportingComponent" type="string">ReportingComponent string</column>
    ///                     <column name="ReportingInstance" type="string">ReportingInstance string</column>
    ///                     <column name="Series" type="Corev1EventSeries">Series of the event</column>
    ///                     <column name="Source" type="V1EventSource">Source of the event</column>
    ///                     <column name="Type" type="string">Type string</column>
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
            KubernetesClientConfiguration clientConfiguration;
            k8s.Kubernetes client;
            
            if (context.EnvironmentVariables.ContainsKey("MUSOQ_KUBERNETES_CONFIG_FILE"))
            {
                var fileContent = context.EnvironmentVariables["MUSOQ_KUBERNETES_CONFIG_FILE"];
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
                clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
                client = new k8s.Kubernetes(clientConfiguration);
            
                return new KubernetesApi(client);
            }
            
            clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            client = new k8s.Kubernetes(clientConfiguration);
                    
            return new KubernetesApi(client);
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
            PodLogsTableName => new PodLogsTable(),
            EventsTableName => new EventsTable(),
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
            PodLogsTableName => new PodLogsSource(client, (string)parameters[0], (string)parameters[1], (string)parameters[2]),
            EventsTableName => new EventsSource(client, 
                parameters.Length == 0 ? 
                    api => api.ListEvents() : api => api.ListNamespacedEvents((string)parameters[0])),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    /// <summary>
    /// Gets raw constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">Name of the data source method</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for the specified method</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            PodsTableName => [CreatePodsMethodInfo()],
            ServicesTableName => [CreateServicesMethodInfo()],
            DeploymentsTableName => [CreateDeploymentsMethodInfo()],
            ReplicaSetsTableName => [CreateReplicaSetsMethodInfo()],
            NodesTableName => [CreateNodesMethodInfo()],
            SecretsTableName => [CreateSecretsMethodInfo()],
            ConfigMapsTableName => [CreateConfigMapsMethodInfo()],
            IngressesTableName => [CreateIngressesMethodInfo()],
            PersistentVolumesTableName => [CreatePersistentVolumesMethodInfo()],
            PersistentVolumeClaimsTableName => [CreatePersistentVolumeClaimsMethodInfo()],
            JobsTableName => [CreateJobsMethodInfo()],
            CronJobsTableName => [CreateCronJobsMethodInfo()],
            StatefulSetsTableName => [CreateStatefulSetsMethodInfo()],
            DaemonSetsTableName => [CreateDaemonSetsMethodInfo()],
            PodContainersTableName => [CreatePodContainersMethodInfo()],
            PodLogsTableName => [CreatePodLogsMethodInfo()],
            EventsTableName => CreateEventsMethodInfos(),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {string.Join(", ", PodsTableName, ServicesTableName, DeploymentsTableName, ReplicaSetsTableName, NodesTableName, SecretsTableName, ConfigMapsTableName, IngressesTableName, PersistentVolumesTableName, PersistentVolumeClaimsTableName, JobsTableName, CronJobsTableName, StatefulSetsTableName, DaemonSetsTableName, PodContainersTableName, PodLogsTableName, EventsTableName)}")
        };
    }

    /// <summary>
    /// Gets raw constructor information for all data source methods in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for all methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        var constructors = new List<SchemaMethodInfo>
        {
            CreatePodsMethodInfo(),
            CreateServicesMethodInfo(),
            CreateDeploymentsMethodInfo(),
            CreateReplicaSetsMethodInfo(),
            CreateNodesMethodInfo(),
            CreateSecretsMethodInfo(),
            CreateConfigMapsMethodInfo(),
            CreateIngressesMethodInfo(),
            CreatePersistentVolumesMethodInfo(),
            CreatePersistentVolumeClaimsMethodInfo(),
            CreateJobsMethodInfo(),
            CreateCronJobsMethodInfo(),
            CreateStatefulSetsMethodInfo(),
            CreateDaemonSetsMethodInfo(),
            CreatePodContainersMethodInfo(),
            CreatePodLogsMethodInfo()
        };

        constructors.AddRange(CreateEventsMethodInfos());

        return constructors.ToArray();
    }

    private static SchemaMethodInfo CreatePodsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(PodsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateServicesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(ServicesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateDeploymentsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(DeploymentsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateReplicaSetsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(ReplicaSetsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateNodesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(NodesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateSecretsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(SecretsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateConfigMapsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(ConfigMapsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateIngressesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(IngressesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreatePersistentVolumesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(PersistentVolumesTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreatePersistentVolumeClaimsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(PersistentVolumeClaimsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateJobsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(JobsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateCronJobsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(CronJobsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateStatefulSetsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(StatefulSetsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreateDaemonSetsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(DaemonSetsTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreatePodContainersMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        return new SchemaMethodInfo(PodContainersTableName, constructorInfo);
    }

    private static SchemaMethodInfo CreatePodLogsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments:
            [
                ("podName", typeof(string)),
                ("containerName", typeof(string)),
                ("namespaceName", typeof(string))
            ]);

        return new SchemaMethodInfo(PodLogsTableName, constructorInfo);
    }

    private static SchemaMethodInfo[] CreateEventsMethodInfos()
    {
        var eventsInfo1 = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments: []);

        var eventsInfo2 = new ConstructorInfo(
            originConstructorInfo: null!,
            supportsInterCommunicator: false,
            arguments:
            [
                ("namespaceName", typeof(string))
            ]);

        return
        [
            new SchemaMethodInfo(EventsTableName, eventsInfo1),
            new SchemaMethodInfo(EventsTableName, eventsInfo2)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new KubernetesLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}