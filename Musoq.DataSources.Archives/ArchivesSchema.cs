using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Archives;

/// <description>
/// Provides schema to work with archives files
/// </description>
/// <short-description>
/// Provides schema to work with archives files
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class ArchivesSchema : SchemaBase
{
    private const string SchemaName = "Archives";

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <virtual-param>Path to the archive file</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#archives.file(string path)</from>
    /// <description>Enumerate archive files like they were regular files</description>
    /// <columns>
    /// <column name="CompressionType" type="CompressionType">Compression type</column>
    /// <column name="ArchivedTime" type="DateTime?">When the file or directory were archived</column>
    /// <column name="CompressedSize" type="long">Compressed size of the file or directory</column>
    /// <column name="Crc" type="long">CRC of the file or directory</column>
    /// <column name="CreatedTime" type="DateTime?">When the file or directory were created</column>
    /// <column name="Key" type="string">Path to file or directory</column>
    /// <column name="LinkTarget" type="string">Link target</column>
    /// <column name="IsDirectory" type="bool">Is directory</column>
    /// <column name="IsEncrypted" type="bool">Is encrypted</column>
    /// <column name="IsSplitAfter" type="bool">Is split after</column>
    /// <column name="IsSolid" type="bool">Is solid</column>
    /// <column name="VolumeIndexFirst" type="int">Volume index first</column>
    /// <column name="VolumeIndexLast" type="int">Volume index last</column>
    /// <column name="LastAccessTime" type="DateTime?">When the file or directory were last accessed</column>
    /// <column name="LastModifiedTime" type="DateTime?">When the file or directory were last modified</column>
    /// <column name="Size" type="long">Size of the file or directory</column>
    /// <column name="Attrib" type="long?">Attributes of the file or directory</column>
    /// <column name="TextContent" type="string">Text content of a file</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public ArchivesSchema()
        : base(SchemaName, CreateLibrary())
    {
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
        return new ArchivesTable();
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
            "file" => new ArchivesRowSource((string) parameters[0]),
            _ => throw new NotSupportedException($"Source {parameters[0]} is not supported.")
        };
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new ArchivesLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}