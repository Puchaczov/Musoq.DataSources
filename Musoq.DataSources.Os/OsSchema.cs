using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Os.Directories;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Metadata;
using Musoq.DataSources.Os.Process;
using Musoq.DataSources.Os.Zip;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Os;

/// <description>
///     Provides schema to work with operating system abstractions
/// </description>
/// <short-description>
///     Provides schema to work with operating system abstractions
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class OsSchema : SchemaBase
{
    private const string SchemaName = "os";
    private const string DirectoriesTable = "directories";
    private const string FilesTable = "files";
    private const string DllsTable = "dlls";
    private const string ZipTable = "zip";
    private const string ProcessesName = "processes";
    private const string DirsCompare = "dirscompare";
    private const string Single = "single";
    private const string Metadata = "metadata";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>First directory</virtual-param>
    ///         <virtual-param>Second directory</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.dirscompare(string sourceDirectory, string destinationDirectory)</from>
    ///                 <description>Compares two directories</description>
    ///                 <columns>
    ///                     <column name="SourceFile" type="ExtendedFileInfo">Source file</column>
    ///                     <column name="DestinationFile" type="ExtendedFileInfo">Destination file</column>
    ///                     <column name="State" type="string">The Same / Modified / Added / Removed</column>
    ///                     <column name="SourceRoot" type="DirectoryInfo">Source directory</column>
    ///                     <column name="DestinationRoot" type="DirectoryInfo">Destination directory</column>
    ///                     <column name="SourceFileRelative" type="string">Relative path to source file</column>
    ///                     <column name="DestinationFileRelative" type="string">Relative path to destination file</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Directory path</virtual-param>
    ///         <virtual-param>Move through subfolders</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.directories(string directory, bool useSubdirectories)</from>
    ///                 <description>Gets the directories</description>
    ///                 <columns>
    ///                     <column name="FullName" type="string">Full name of the directory</column>
    ///                     <column name="Attributes" type="FileAttributes">Directory attributes</column>
    ///                     <column name="CreationTime" type="DateTime">Creation time</column>
    ///                     <column name="CreationTimeUtc" type="DateTime">Creation time in UTC</column>
    ///                     <column name="LastAccessTime" type="DateTime">Last access time</column>
    ///                     <column name="LastAccessTimeUtc" type="DateTime">Last access time in UTC</column>
    ///                     <column name="LastWriteTime" type="DateTime">Last write time</column>
    ///                     <column name="LastWriteTimeUtc" type="DateTime">Last write time in UTC</column>
    ///                     <column name="Exists" type="bool">Determine does the directory exists</column>
    ///                     <column name="Extension" type="string">Gets the extension part of the file name</column>
    ///                     <column name="LastAccessTime" type="DateTime">
    ///                         Gets the time the current file or directory was last
    ///                         accessed
    ///                     </column>
    ///                     <column name="LastAccessTimeUtc" type="DateTime">
    ///                         Gets the time, in coordinated universal time
    ///                         (UTC), that the current file or directory was last accessed
    ///                     </column>
    ///                     <column name="Name" type="string">Gets the directory name</column>
    ///                     <column name="LastWriteTime" type="DateTime">
    ///                         Gets the date when the file or directory was written
    ///                         to
    ///                     </column>
    ///                     <column name="Parent" type="DirectoryInfo">Gets the parent directory</column>
    ///                     <column name="Root" type="DirectoryInfo">Gets the root directory</column>
    ///                     <column name="DirectoryInfo" type="DirectoryInfo">Gets raw DirectoryInfo</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to dll</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.dlls(string path)</from>
    ///                 <description>Gets the dlls</description>
    ///                 <columns>
    ///                     <column name="FileInfo" type="FileInfo">Gets the metadata about the DLL file</column>
    ///                     <column name="Assembly" type="Assembly">Gets the Assembly object</column>
    ///                     <column name="Version" type="FileVersionInfo">Gets the assembly version</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to dll</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.dlls(string path)</from>
    ///                 <description>Gets the dlls</description>
    ///                 <columns>
    ///                     <column name="FileInfo" type="FileInfo">Gets the metadata about the DLL file</column>
    ///                     <column name="Assembly" type="Assembly">Gets the Assembly object</column>
    ///                     <column name="Version" type="FileVersionInfo">Gets the assembly version</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to directory</virtual-param>
    ///         <virtual-param>Move through subfolders</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.files(string directory, bool useSubdirectories)</from>
    ///                 <description>Gets the files</description>
    ///                 <columns>
    ///                     <column name="Name" type="string">Name of the file</column>
    ///                     <column name="FileName" type="string">Name of the file</column>
    ///                     <column name="CreationTime" type="DateTime">Creation time</column>
    ///                     <column name="CreationTimeUtc" type="DateTime">Creation time in UTC</column>
    ///                     <column name="LastAccessTime" type="DateTime">Last access time</column>
    ///                     <column name="LastAccessTimeUtc" type="DateTime">Last access time in UTC</column>
    ///                     <column name="LastWriteTime" type="DateTime">Last write time</column>
    ///                     <column name="LastWriteTimeUtc" type="DateTime">Last write time in UTC</column>
    ///                     <column name="Extension" type="string">Gets the extension part of the file name</column>
    ///                     <column name="FullPath" type="string">Gets the full path of file</column>
    ///                     <column name="DirectoryName" type="string">Gets the directory name</column>
    ///                     <column name="DirectoryPath" type="string">Gets the directory path</column>
    ///                     <column name="Exists" type="bool">Determine whether file exists or not</column>
    ///                     <column name="IsReadOnly" type="bool">Determine whether the file is readonly</column>
    ///                     <column name="Length" type="long">Gets the length of file</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.processes()</from>
    ///                 <description>Gets the processes</description>
    ///                 <columns>
    ///                     <column name="BasePriority" type="int">Gets the base priority of associated process</column>
    ///                     <column name="EnableRaisingEvents" type="bool">
    ///                         Gets whether the exited event should be raised when
    ///                         the process terminates
    ///                     </column>
    ///                     <column name="ExitCode" type="int">Gets the value describing process termination</column>
    ///                     <column name="ExitTime" type="DateTime">Exit time in UTC</column>
    ///                     <column name="Handle" type="IntPtr">Gets the native handle of the associated process</column>
    ///                     <column name="HandleCount" type="int">Gets the number of handles opened by the process</column>
    ///                     <column name="HasExited" type="bool">
    ///                         Gets a value indicating whether the associated process has
    ///                         been terminated
    ///                     </column>
    ///                     <column name="Id" type="int">Gets the unique identifier for the associated process</column>
    ///                     <column name="MachineName" type="string">
    ///                         Gets the name of the computer the associated process is
    ///                         running on
    ///                     </column>
    ///                     <column name="MainWindowTitle" type="string">Gets the caption of the main window of the process</column>
    ///                     <column name="PagedMemorySize64" type="long">
    ///                         Gets a value indicating whether the user interface of
    ///                         the process is responding
    ///                     </column>
    ///                     <column name="ProcessName" type="string">
    ///                         The name that the system uses to identify the process to
    ///                         the user
    ///                     </column>
    ///                     <column name="ProcessorAffinity" type="IntPtr">
    ///                         Gets the processors on which the threads in this
    ///                         process can be scheduled to run
    ///                     </column>
    ///                     <column name="Responding" type="bool">
    ///                         Gets a value indicating whether the user interface of the
    ///                         process is responding
    ///                     </column>
    ///                     <column name="StartTime" type="DateTime">Gets the time that the associated process was started</column>
    ///                     <column name="TotalProcessorTime" type="TimeSpan">Gets the total processor time for this process</column>
    ///                     <column name="UserProcessorTime" type="TimeSpan">Gets the user processor time for this process</column>
    ///                     <column name="Directory" type="string">Gets the directory of the process</column>
    ///                     <column name="FileName" type="string">Gets the filename of the process</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to zip file</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.zip(string path)</from>
    ///                 <description>Gets the zip files</description>
    ///                 <columns>
    ///                     <column name="Name" type="string">Gets the file name of the entry in the zip archive</column>
    ///                     <column name="FullName" type="string">Gets the relative path of the entry in the zip archive</column>
    ///                     <column name="CompressedLength" type="long">
    ///                         Gets the compressed size of the entry in the zip
    ///                         archive
    ///                     </column>
    ///                     <column name="LastWriteTime" type="DateTimeOffset">
    ///                         Gets the last time the entry in the zip archive
    ///                         was changed
    ///                     </column>
    ///                     <column name="Length" type="long">Gets the uncompressed size of the entry in the zip archive</column>
    ///                     <column name="IsDirectory" type="bool">Determine whether the entry is a directory</column>
    ///                     <column name="Level" type="int">Gets the nesting level</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to file</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#os.metadata(string directoryOrFile)</from>
    ///                 <description>Gets the metadata for file or for files within the directory</description>
    ///                 <columns>
    ///                     <column name="FullName" type="string">Gets the full path of the file</column>
    ///                     <column name="DirectoryName" type="string">Gets the directory the metadata resides in</column>
    ///                     <column name="TagName" type="string">Gets the tag name</column>
    ///                     <column name="Description" type="string">Gets the description</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>#os.metadata(string directory, bool throwOnMetadataReadError)</from>
    ///                 <description>Gets the metadata for files within directories</description>
    ///                 <columns>
    ///                     <column name="FullName" type="string">Gets the full path of the file</column>
    ///                     <column name="DirectoryName" type="string">Gets the directory the metadata resides in</column>
    ///                     <column name="TagName" type="string">Gets the tag name</column>
    ///                     <column name="Description" type="string">Gets the description</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>#os.metadata(string directory, bool useSubdirectories, bool throwOnMetadataReadError)</from>
    ///                 <description>Gets the metadata for files within directories</description>
    ///                 <columns>
    ///                     <column name="FullName" type="string">Gets the full path of the file</column>
    ///                     <column name="DirectoryName" type="string">Gets the directory the metadata resides in</column>
    ///                     <column name="TagName" type="string">Gets the tag name</column>
    ///                     <column name="Description" type="string">Gets the description</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public OsSchema()
        : base(SchemaName, CreateLibrary())
    {
        AddSource<FilesSource>(FilesTable);
        AddTable<FilesBasedTable>(FilesTable);

        AddSource<DirectoriesSource>(DirectoriesTable);
        AddTable<DirectoriesBasedTable>(DirectoriesTable);

        AddSource<ZipSource>(ZipTable);
        AddTable<ZipBasedTable>(ZipTable);

        AddSource<ProcessesSource>(ProcessesName);
        AddTable<ProcessBasedTable>(ProcessesName);

        AddSource<DllSource>(DllsTable);
        AddTable<DllBasedTable>(DllsTable);

        AddSource<CompareDirectoriesSource>(DirsCompare);
        AddTable<DirsCompareBasedTable>(DirsCompare);

        AddSource<MetadataSource>(Metadata);
        AddTable<MetadataTable>(Metadata);
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        switch (name.ToLowerInvariant())
        {
            case FilesTable:
                return new FilesBasedTable();
            case DirectoriesTable:
                return new DirectoriesBasedTable();
            case ZipTable:
                return new ZipBasedTable();
            case ProcessesName:
                return new ProcessBasedTable();
            case DllsTable:
                return new DllBasedTable();
            case DirsCompare:
                return new DirsCompareBasedTable();
            case Single:
                return new SingleRowSchemaTable();
            case Metadata:
                return new MetadataTable();
        }

        throw new NotSupportedException($"Unsupported table {name}.");
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        switch (name.ToLowerInvariant())
        {
            case FilesTable:
                return new FilesSource((string)parameters[0], (bool)parameters[1], runtimeContext);
            case DirectoriesTable:
                return new DirectoriesSource((string)parameters[0], (bool)parameters[1], runtimeContext);
            case ZipTable:
                return new ZipSource((string)parameters[0], runtimeContext);
            case ProcessesName:
                return new ProcessesSource(runtimeContext);
            case DllsTable:
                return new DllSource((string)parameters[0], (bool)parameters[1], runtimeContext);
            case DirsCompare:
                return new CompareDirectoriesSource((string)parameters[0], (string)parameters[1], runtimeContext);
            case Single:
                return new SingleRowSource();
            case Metadata:
            {
                {
                    if (parameters is [string pathDirectory, bool useSubDirectories, bool throwOnMetadataReadError])
                        return new MetadataSource(
                            pathDirectory,
                            null,
                            useSubDirectories,
                            MetadataSource.PathType.MustBeDirectory,
                            throwOnMetadataReadError,
                            runtimeContext);
                }

                {
                    if (parameters is [string pathDirectoryOrFile, bool throwOnMetadataReadError])
                    {
                        var isDirectory = Directory.Exists(pathDirectoryOrFile);
                        var directoryPath = isDirectory
                            ? pathDirectoryOrFile
                            : Path.GetDirectoryName(pathDirectoryOrFile) ??
                              throw new NotSupportedException($"Unsupported parameters for metadata source {name}");
                        var fileName = isDirectory ? null : Path.GetFileName(pathDirectoryOrFile);
                        return new MetadataSource(
                            directoryPath,
                            fileName,
                            false,
                            MetadataSource.PathType.DirectoryOrFile,
                            throwOnMetadataReadError,
                            runtimeContext);
                    }
                }

                {
                    if (parameters is [string pathDirectoryOrFile])
                    {
                        var isDirectory = Directory.Exists(pathDirectoryOrFile);
                        var directoryPath = isDirectory
                            ? pathDirectoryOrFile
                            : Path.GetDirectoryName(pathDirectoryOrFile) ??
                              throw new NotSupportedException($"Unsupported parameters for metadata source {name}");
                        var fileName = isDirectory ? null : Path.GetFileName(pathDirectoryOrFile);
                        return new MetadataSource(
                            directoryPath,
                            fileName,
                            false,
                            MetadataSource.PathType.DirectoryOrFile,
                            true,
                            runtimeContext);
                    }
                }

                throw new NotSupportedException($"Unsupported parameters for metadata source {name}");
            }
        }

        throw new NotSupportedException($"Unsupported row source {name}");
    }

    /// <summary>
    ///     Gets the raw constructors for a specific method.
    /// </summary>
    /// <param name="methodName">The name of the method</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of SchemaMethodInfo objects describing the method signatures</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            FilesTable => [CreateFilesMethodInfo()],
            DirectoriesTable => [CreateDirectoriesMethodInfo()],
            ZipTable => [CreateZipMethodInfo()],
            ProcessesName => [CreateProcessesMethodInfo()],
            DllsTable => [CreateDllsMethodInfo()],
            DirsCompare => [CreateDirsCompareMethodInfo()],
            Metadata => CreateMetadataMethodInfos(),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {string.Join(", ", FilesTable, DirectoriesTable, ZipTable, ProcessesName, DllsTable, DirsCompare, Metadata)}")
        };
    }

    /// <summary>
    ///     Gets the raw constructors for all methods in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of SchemaMethodInfo objects for all data source methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        var constructors = new List<SchemaMethodInfo>
        {
            CreateFilesMethodInfo(),
            CreateDirectoriesMethodInfo(),
            CreateZipMethodInfo(),
            CreateProcessesMethodInfo(),
            CreateDllsMethodInfo(),
            CreateDirsCompareMethodInfo()
        };

        constructors.AddRange(CreateMetadataMethodInfos());

        return constructors.ToArray();
    }

    private static SchemaMethodInfo CreateFilesMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<FilesSource>(FilesTable)[0];
    }

    private static SchemaMethodInfo CreateDirectoriesMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<DirectoriesSource>(DirectoriesTable)[0];
    }

    private static SchemaMethodInfo CreateZipMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<ZipSource>(ZipTable)[0];
    }

    private static SchemaMethodInfo CreateProcessesMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<ProcessesSource>(ProcessesName)[0];
    }

    private static SchemaMethodInfo CreateDllsMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<DllSource>(DllsTable)[0];
    }

    private static SchemaMethodInfo CreateDirsCompareMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<CompareDirectoriesSource>(DirsCompare)[0];
    }

    private static SchemaMethodInfo[] CreateMetadataMethodInfos()
    {
        var metadataInfo1 = new ConstructorInfo(
            null!,
            false,
            [
                ("directoryOrFile", typeof(string))
            ]
        );

        var metadataInfo2 = new ConstructorInfo(
            null!,
            false,
            [
                ("pathDirectoryOrFile", typeof(string)),
                ("throwOnMetadataReadError", typeof(bool))
            ]
        );

        var metadataInfo3 = new ConstructorInfo(
            null!,
            false,
            [
                ("directory", typeof(string)),
                ("useSubDirectories", typeof(bool)),
                ("throwOnMetadataReadError", typeof(bool))
            ]
        );

        return
        [
            new SchemaMethodInfo(Metadata, metadataInfo1),
            new SchemaMethodInfo(Metadata, metadataInfo2),
            new SchemaMethodInfo(Metadata, metadataInfo3)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new OsLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}