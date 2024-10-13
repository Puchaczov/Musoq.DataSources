using System.Collections.Concurrent;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.RowsSources;
using Musoq.Schema;
using Musoq.Schema.Attributes;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Roslyn;

/// <description>
/// Provides schema to work with Roslyn data source.
/// </description>
/// <short-description>
/// Provides schema to work with Roslyn data source.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class CSharpSchema : SchemaBase
{
    private const string SchemaName = "Csharp";
    
    internal static readonly ConcurrentDictionary<string, SolutionEntity> Solutions = new();
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables></environmentVariables>
    /// #csharp.solution(string path)
    /// </from>
    /// <description>Allows to perform queries on the given solution file.</description>
    /// <columns>
    /// <column name="Id" type="string">Solution id</column>
    /// <column name="Projects" type="ProjectEntity[]">Projects within the solution</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    /// <additional-table>
    /// <description>Represent project of solution</description>
    /// <columns type="ProjectEntity">
    /// <column name="Id" type="string">Project id</column>
    /// <column name="FilePath" type="string">File path</column>
    /// <column name="OutputFilePath" type="string">Output file path</column>
    /// <column name="OutputRefFilePath" type="string">Output reference file path</column>
    /// <column name="DefaultNamespace" type="string">Default namespace</column>
    /// <column name="Language" type="string">Language</column>
    /// <column name="AssemblyName" type="string">Assembly name</column>
    /// <column name="Name" type="string">Name</column>
    /// <column name="IsSubmission" type="bool">Is submission</column>
    /// <column name="Version" type="string">Version</column>
    /// <column name="Documents" type="DocumentEntity[]">Documents</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent document of project</description>
    /// <columns type="DocumentEntity">
    /// <column name="Name" type="string">Name</column>
    /// <column name="Text" type="string">Text</column>
    /// <column name="ClassCount" type="int">Class count</column>
    /// <column name="InterfaceCount" type="int">Interface count</column>
    /// <column name="EnumCount" type="int">Enum count</column>
    /// <column name="Classes" type="ClassEntity[]">Struct count</column>
    /// <column name="Interfaces" type="InterfaceEntity[]">Interfaces</column>
    /// <column name="Enums" type="EnumEntity[]">Enums</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent class of document</description>
    /// <columns type="ClassEntity">
    /// <column name="Text" type="string">Text</column>
    /// <column name="IsAbstract" type="bool">Is abstract</column>
    /// <column name="IsSealed" type="bool">Is sealed</column>
    /// <column name="IsStatic" type="bool">Is static</column>
    /// <column name="BaseTypes" type="string[]">Base types</column>
    /// <column name="Interfaces" type="string[]">Interfaces</column>
    /// <column name="TypeParameters" type="string[]">Type parameters</column>
    /// <column name="MemberNames" type="string[]">Member names</column>
    /// <column name="Attributes" type="string[]">Attributes</column>
    /// <column name="Name" type="string">Name</column>
    /// <column name="FullName" type="string">Full name</column>
    /// <column name="Namespace" type="string">Namespace</column>
    /// <column name="Modifiers" type="string[]">Modifiers</column>
    /// <column name="Methods" type="MethodEntity[]">Methods</column>
    /// <column name="Properties" type="PropertyEntity[]">Properties</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent enum of document</description>
    /// <columns type="EnumEntity">
    /// <column name="Members" type="string[]">Members</column>
    /// <column name="Name" type="string">Name</column>
    /// <column name="FullName" type="string">Full name</column>
    /// <column name="Namespace" type="string">Namespace</column>
    /// <column name="Modifiers" type="string[]">Modifiers</column>
    /// <column name="Methods" type="MethodEntity[]">Methods</column>
    /// <column name="Properties" type="PropertyEntity[]">Properties</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent interface of document</description>
    /// <columns type="InterfaceEntity">
    /// <column name="BaseInterfaces" type="string[]">Base interfaces</column>
    /// <column name="Name" type="string">Name</column>
    /// <column name="FullName" type="string">Full name</column>
    /// <column name="Namespace" type="string">Namespace</column>
    /// <column name="Modifiers" type="string[]">Modifiers</column>
    /// <column name="Methods" type="MethodEntity[]">Methods</column>
    /// <column name="Properties" type="PropertyEntity[]">Properties</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent method of class</description>
    /// <columns type="MethodEntity">
    /// <column name="Name" type="string">Name</column>
    /// <column name="ReturnType" type="string">Return type</column>
    /// <column name="Parameters" type="ParameterEntity[]">Parameters</column>
    /// <column name="Modifiers" type="string[]">Modifiers</column>
    /// <column name="Body" type="string">Body</column>
    /// <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent property of class</description>
    /// <columns type="PropertyEntity">
    /// <column name="Name" type="string">Name</column>
    /// <column name="Type" type="string">Type</column>
    /// <column name="IsIndexer" type="bool">Is indexer</column>
    /// <column name="IsReadOnly" type="bool">Is read only</column>
    /// <column name="IsWriteOnly" type="bool">Is write only</column>
    /// <column name="IsRequired" type="bool">Is required</column>
    /// <column name="IsWithEvents" type="bool">Is with events</column>
    /// <column name="IsVirtual" type="bool">Is virtual</column>
    /// <column name="IsOverride" type="bool">Is override</column>
    /// <column name="IsAbstract" type="bool">Is abstract</column>
    /// <column name="IsSealed" type="bool">Is sealed</column>
    /// <column name="IsStatic" type="bool">Is static</column>
    /// <column name="Modifiers" type="string[]">Modifiers</column>
    /// </columns>
    /// </additional-table>
    /// <additional-table>
    /// <description>Represent parameter of method</description>
    /// <columns type="ParameterEntity">
    /// <column name="Name" type="string">Name</column>
    /// <column name="Type" type="string">Type</column>
    /// <column name="IsOptional" type="bool">Is optional</column>
    /// <column name="IsParams" type="bool">Is params</column>
    /// <column name="IsThis" type="bool">Is this</column>
    /// <column name="IsDiscard" type="bool">Is discard</column>
    /// <column name="IsIn" type="bool">Is in</column>
    /// <column name="IsOut" type="bool">Is out</column>
    /// <column name="IsRef" type="bool">Is ref</column>
    /// <column name="IsByRef" type="bool">Is by ref</column>
    /// <column name="IsByValue" type="bool">Is by value</column>
    /// </columns>
    /// </additional-table>
    /// </additional-tables>
    public CSharpSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
        AddSource<CSharpSolutionRowsSource>("file");
        AddTable<CSharpSolutionTable>("file");
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
        switch (name.ToLowerInvariant())
        {
            case "solution":
                return new CSharpSolutionTable();
        }

        return base.GetTableByName(name, runtimeContext, parameters);
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
        switch (name.ToLowerInvariant())
        {
            case "solution":
                if (Solutions.TryGetValue((string) parameters[0], out var solutionFilePath))
                {
                    return new CSharpInMemorySolutionRowsSource(solutionFilePath, runtimeContext.EndWorkToken);
                }
                
                return new CSharpSolutionRowsSource((string) parameters[0], runtimeContext.EndWorkToken);
        }

        return base.GetRowSource(name, runtimeContext, parameters);
    }
    
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new CSharpLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}