using Docker.DotNet;
using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Docker.Images;
using Musoq.DataSources.Docker.Networks;
using Musoq.DataSources.Docker.Volumes;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Docker;

/// <description>
/// Provides schema to work with docker containers, images, networks and volumes.
/// </description>
/// <short-description>
/// Provides schema to work with docker containers, images, networks and volumes.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class DockerSchema : SchemaBase
{
    private const string DockerSchemaName = "docker";
    
    private const string ContainersTableName = "containers";
    private const string ImagesTableName = "images";
    private const string NetworksTableName = "networks";
    private const string VolumesTableName = "volumes";

    private readonly IDockerApi _dockerApi;

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#docker.containers()</from>
    /// <description>Gets containers of local docker</description>
    /// <columns>
    /// <column name="ID" type="string">Container ID</column>
    /// <column name="Names" type="IList&lt;string&gt;">Container names</column>
    /// <column name="Image" type="string">Image name</column>
    /// <column name="ImageID" type="string">Image ID</column>
    /// <column name="Command" type="string">Command the container run on with</column>
    /// <column name="Created" type="string">Container created datetime</column>
    /// <column name="Ports" type="IList&lt;string&gt;">Mapped ports</column>
    /// <column name="SizeRw" type="long">Size of the created or changed files</column>
    /// <column name="SizeRootFs" type="long">Total size of all files in the container</column>
    /// <column name="Labels" type="IDictionary&lt;string, string&gt;">Assigned labels to specific container</column>
    /// <column name="Status" type="string">Status of the container</column>
    /// <column name="NetworkSettings" type="SummaryNetworkSettings">Network settings</column>
    /// <column name="Mounts" type="IList&lt;MountPoint&gt;">Mounted points</column>
    /// <column name="FlattenPorts" type="string">Mapped ports as a string with comma delimiter</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#docker.images()</from>
    /// <description>Gets images of local docker</description>
    /// <columns>
    /// <column name="Containers" type="long">Number of containers</column>
    /// <column name="Created" type="DateTime">Creation time</column>
    /// <column name="ID" type="string">Unique identifier</column>
    /// <column name="Labels" type="IDictionary&lt;string, string&gt;">Set of labels</column>
    /// <column name="ParentID" type="string">Parent's unique identifier</column>
    /// <column name="RepoDigests" type="IList&lt;string&gt;">List of repository digests</column>
    /// <column name="RepoTags" type="IList&lt;string&gt;">List of repository tags</column>
    /// <column name="SharedSize" type="long">Shared size in bytes</column>
    /// <column name="Size" type="long">Size in bytes</column>
    /// <column name="VirtualSize" type="long">Virtual size in bytes</column>
    /// </columns>
    /// </example>       
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#docker.volumes()</from>
    /// <description>Gets volumes of local docker</description>
    /// <columns>
    /// <column name="CreatedAt" type="string">Creation time of the volume</column>
    /// <column name="Driver" type="string">Driver used for the volume</column>
    /// <column name="Labels" type="IDictionary&lt;string, string&gt;">Set of labels for the volume</column>
    /// <column name="Mountpoint" type="string">Mount point for the volume</column>
    /// <column name="Name" type="string">Name of the volume</column>
    /// <column name="Options" type="IDictionary&lt;string, string&gt;">Set of options for the volume</column>
    /// <column name="Scope" type="string">Scope of the volume</column>
    /// <column name="Status" type="IDictionary&lt;string, string&gt;">Status information for the volume</column>
    /// <column name="UsageData" type="VolumeUsageData">Usage data for the volume</column>
    /// </columns>
    /// </example>      
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#docker.networks()</from>
    /// <description>Gets networks of local docker</description>
    /// <columns>
    /// <column name="Name" type="string">Name of the network</column>
    /// <column name="ID" type="string">Unique identifier of the network</column>
    /// <column name="Created" type="DateTime">Creation time of the network</column>
    /// <column name="Scope" type="string">Scope of the network</column>
    /// <column name="Driver" type="string">Driver used for the network</column>
    /// <column name="EnableIPv6" type="bool">Flag indicating if IPv6 is enabled</column>
    /// <column name="IPAM" type="IPAM">IP Address Management specification</column>
    /// <column name="Internal" type="bool">Flag indicating if the network is internal</column>
    /// <column name="Attachable" type="bool">Flag indicating if the network is attachable</column>
    /// <column name="Ingress" type="bool">Flag indicating if the network is ingress</column>
    /// <column name="ConfigFrom" type="ConfigReference">Network configuration source</column>
    /// <column name="ConfigOnly" type="bool">Flag indicating if the network is configuration only</column>
    /// <column name="Containers" type="IDictionary&lt;string, EndpointResource&gt;">Dictionary of connected containers</column>
    /// <column name="Options" type="IDictionary&lt;string, string&gt;">Set of options for the network</column>
    /// <column name="Labels" type="IDictionary&lt;string, string&gt;">Set of labels for the network</column>
    /// <column name="Peers" type="IList&lt;PeerInfo&gt;">List of network peers</column>
    /// <column name="Services" type="IDictionary&lt;string, ServiceInfo&gt;">Dictionary of network services</column>
    /// </columns>
    /// </example>      
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public DockerSchema() 
        : base(DockerSchemaName, CreateLibrary())
    {
        AddSource<ContainersSource>(ContainersTableName);
        AddTable<ContainersTable>(ContainersTableName);
        
        AddSource<ImagesSource>(ImagesTableName);
        AddTable<ImagesTable>(ImagesTableName);
        
        AddSource<NetworksSource>(NetworksTableName);
        AddTable<NetworksTable>(NetworksTableName);
        
        AddSource<VolumesSource>(VolumesTableName);
        AddTable<VolumesTable>(VolumesTableName);
        
        var configuration = new DockerClientConfiguration();
        var client = configuration.CreateClient();
        _dockerApi = new DockerApi(client);
    }

    internal DockerSchema(IDockerApi dockerApi)
        : base(DockerSchemaName, CreateLibrary())
    {
        _dockerApi = dockerApi;
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
            ContainersTableName => new ContainersTable(),
            ImagesTableName => new ImagesTable(),
            NetworksTableName => new NetworksTable(),
            VolumesTableName => new VolumesTable(),
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
        
        return name.ToLowerInvariant() switch
        {
            ContainersTableName => new ContainersSource(_dockerApi),
            ImagesTableName => new ImagesSource(_dockerApi),
            NetworksTableName => new NetworksSource(_dockerApi),
            VolumesTableName => new VolumesSource(_dockerApi),
            _ => throw new NotSupportedException($"Table {name} not supported.")
        };
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();

        var library = new DockerLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}