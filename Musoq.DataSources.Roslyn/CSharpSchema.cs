using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.RowsSources;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Roslyn;

/// <description>
///     Provides schema to work with Roslyn data source.
/// </description>
/// <short-description>
///     Provides schema to work with Roslyn data source.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class CSharpSchema : SchemaBase
{
    private const string SchemaName = "Csharp";

    private static readonly IFileSystem? FileSystem = new DefaultFileSystem();

    private readonly Func<string, IHttpClient?, INuGetPropertiesResolver> _createNugetPropertiesResolver;

    static CSharpSchema()
    {
        SolutionOperationsCommand.Initialize();
    }

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="GITHUB_API_KEY" isRequired="false">GitHub API key</environmentVariable>
    ///                         <environmentVariable name="GITLAB_API_KEY" isRequired="false">GitLab API key</environmentVariable>
    ///                         <environmentVariable name="EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT" isRequired="false">
    ///                             External
    ///                             server endpoint to resolve properties
    ///                         </environmentVariable>
    ///                     </environmentVariables>
    ///                     #csharp.solution(string path)
    ///                 </from>
    ///                 <description>Allows to perform queries on the given solution file.</description>
    ///                 <columns>
    ///                     <column name="Id" type="string">Solution id</column>
    ///                     <column name="Projects" type="ProjectEntity[]">Projects within the solution</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    ///     <additional-table>
    ///         <description>Represent project of solution</description>
    ///         <columns type="ProjectEntity">
    ///             <column name="Id" type="string">Project id</column>
    ///             <column name="FilePath" type="string">File path</column>
    ///             <column name="OutputFilePath" type="string">Output file path</column>
    ///             <column name="OutputRefFilePath" type="string">Output reference file path</column>
    ///             <column name="DefaultNamespace" type="string">Default namespace</column>
    ///             <column name="Language" type="string">Language</column>
    ///             <column name="AssemblyName" type="string">Assembly name</column>
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="IsSubmission" type="bool">Is submission</column>
    ///             <column name="Version" type="string">Version</column>
    ///             <column name="Documents" type="DocumentEntity[]">Documents</column>
    ///             <column name="Types" type="TypeEntity[]">Types</column>
    ///             <column name="NugetPackages" type="NugetPackageEntity[]">Nuget packages</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent document of project</description>
    ///         <columns type="DocumentEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="Text" type="string">Text</column>
    ///             <column name="FilePath" type="string">Absolute file path of the document on disk</column>
    ///             <column name="ClassCount" type="int">Class count</column>
    ///             <column name="InterfaceCount" type="int">Interface count</column>
    ///             <column name="EnumCount" type="int">Enum count</column>
    ///             <column name="Classes" type="ClassEntity[]">Classes (includes records)</column>
    ///             <column name="Interfaces" type="InterfaceEntity[]">Interfaces</column>
    ///             <column name="Enums" type="EnumEntity[]">Enums</column>
    ///             <column name="Delegates" type="DelegateEntity[]">Delegate declarations</column>
    ///             <column name="DelegateCount" type="int">Number of delegate declarations</column>
    ///             <column name="Diagnostics" type="DiagnosticEntity[]">Compiler diagnostics (errors, warnings)</column>
    ///             <column name="DiagnosticCount" type="int">Number of compiler diagnostics</column>
    ///             <column name="ErrorCount" type="int">Number of error-level diagnostics</column>
    ///             <column name="WarningCount" type="int">Number of warning-level diagnostics</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent referenced document of project</description>
    ///         <columns type="ReferencedDocumentEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="Text" type="string">Text</column>
    ///             <column name="ClassCount" type="int">Class count</column>
    ///             <column name="InterfaceCount" type="int">Interface count</column>
    ///             <column name="EnumCount" type="int">Enum count</column>
    ///             <column name="Classes" type="ClassEntity[]">Struct count</column>
    ///             <column name="Interfaces" type="InterfaceEntity[]">Interfaces</column>
    ///             <column name="Enums" type="EnumEntity[]">Enums</column>
    ///             <column name="StartLine" type="int">Start line</column>
    ///             <column name="StartColumn" type="int">Start column</column>
    ///             <column name="EndLine" type="int">End line</column>
    ///             <column name="EndColumn" type="int">End column</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent class of document</description>
    ///         <columns type="ClassEntity">
    ///             <column name="Document" type="DocumentEntity">Document</column>
    ///             <column name="Text" type="string">Text</column>
    ///             <column name="IsAbstract" type="bool">Is abstract</column>
    ///             <column name="IsSealed" type="bool">Is sealed</column>
    ///             <column name="IsStatic" type="bool">Is static</column>
    ///             <column name="BaseTypes" type="string[]">Base types</column>
    ///             <column name="Interfaces" type="string[]">Directly implemented interfaces (short names)</column>
    ///             <column name="AllInterfaces" type="string[]">All interfaces including transitive (fully qualified)</column>
    ///             <column name="TypeParameters" type="string[]">Type parameters</column>
    ///             <column name="MemberNames" type="string[]">Member names</column>
    ///             <column name="Attributes" type="string[]">Attributes</column>
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="FullName" type="string">Full name</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Methods" type="MethodEntity[]">Methods</column>
    ///             <column name="Properties" type="PropertyEntity[]">Properties</column>
    ///             <column name="MethodsCount" type="int">Methods count</column>
    ///             <column name="PropertiesCount" type="int">Properties count</column>
    ///             <column name="FieldsCount" type="int">Fields count</column>
    ///             <column name="InheritanceDepth" type="int">Inheritance depth</column>
    ///             <column name="ConstructorsCount" type="int">Constructors count</column>
    ///             <column name="NestedClassesCount" type="int">Nested classes count</column>
    ///             <column name="NestedInterfacesCount" type="int">Nested interfaces count</column>
    ///             <column name="InterfacesCount" type="int">Interfaces count</column>
    ///             <column name="LackOfCohesion" type="int">Lack of cohesion</column>
    ///             <column name="LinesOfCode" type="int">Lines of code</column>
    ///             <column name="IsRecord" type="bool">Whether the class is a record type</column>
    ///             <column name="IsPartial" type="bool">Whether the class is partial</column>
    ///             <column name="HasDocumentation" type="bool">Whether the class has XML documentation</column>
    ///             <column name="Events" type="EventEntity[]">Events declared in the class</column>
    ///             <column name="EventsCount" type="int">Number of events</column>
    ///             <column name="TypeParameterConstraints" type="TypeParameterConstraintEntity[]">Type parameter constraints</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent enum of document</description>
    ///         <columns type="EnumEntity">
    ///             <column name="Document" type="DocumentEntity">Document</column>
    ///             <column name="Members" type="string[]">Members</column>
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="FullName" type="string">Full name</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Methods" type="MethodEntity[]">Methods</column>
    ///             <column name="Properties" type="PropertyEntity[]">Properties</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///             <column name="UnderlyingType" type="string">Underlying type (e.g. int, byte)</column>
    ///             <column name="HasFlagsAttribute" type="bool">Whether the enum has [Flags] attribute</column>
    ///             <column name="EnumMembers" type="EnumMemberEntity[]">Enum members with name and value</column>
    ///             <column name="HasDocumentation" type="bool">Whether the enum has XML documentation</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent interface of document</description>
    ///         <columns type="InterfaceEntity">
    ///             <column name="Document" type="DocumentEntity">Document</column>
    ///             <column name="BaseInterfaces" type="string[]">Directly extended base interfaces (short names)</column>
    ///             <column name="AllBaseInterfaces" type="string[]">All base interfaces including transitive (fully qualified)</column>
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="FullName" type="string">Full name</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Methods" type="MethodEntity[]">Methods</column>
    ///             <column name="Properties" type="PropertyEntity[]">Properties</column>
    ///             <column name="IsPartial" type="bool">Whether the interface is partial</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///             <column name="HasDocumentation" type="bool">Whether the interface has XML documentation</column>
    ///             <column name="TypeParameters" type="string[]">Type parameters (e.g. T, TKey)</column>
    ///             <column name="MemberNames" type="string[]">Names of all interface members</column>
    ///             <column name="MethodsCount" type="int">Number of methods</column>
    ///             <column name="PropertiesCount" type="int">Number of properties</column>
    ///             <column name="Events" type="EventEntity[]">Events declared in the interface</column>
    ///             <column name="EventsCount" type="int">Number of events</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent method of class</description>
    ///         <columns type="MethodEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="ReturnType" type="string">Return type</column>
    ///             <column name="FullReturnType" type="string">Full return type name including namespace</column>
    ///             <column name="Parameters" type="ParameterEntity[]">Parameters</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Text" type="string">Text</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///             <column name="CyclomaticComplexity" type="int">Cyclomatic complexity</column>
    ///             <column name="LinesOfCode" type="int">Lines of code</column>
    ///             <column name="StatementsCount" type="int">Number of statements in the method body</column>
    ///             <column name="HasBody" type="bool">Whether the method has an implementation body</column>
    ///             <column name="IsEmpty" type="bool">Whether the method has a body but contains no statements</column>
    ///             <column name="BodyContainsOnlyTrivia" type="bool">
    ///                 Whether the method body contains only comments and/or
    ///                 whitespace
    ///             </column>
    ///             <column name="LocalFunctions" type="LocalFunctionEntity[]">Local functions defined within this method</column>
    ///             <column name="ReferenceCount" type="int?">Number of references to the method (null if unavailable)</column>
    ///             <column name="IsUsed" type="bool?">Whether the method is referenced (null if unavailable)</column>
    ///             <column name="ReferencedTypes" type="TypeReferenceEntity[]">Types referenced in the method body with usage context</column>
    ///             <column name="TypeParameterConstraints" type="TypeParameterConstraintEntity[]">Type parameter constraints</column>
    ///             <column name="ExplicitInterfaceImplementations" type="string[]">Explicit interface implementations</column>
    ///             <column name="IsExplicitInterfaceImplementation" type="bool">Whether the method is an explicit interface implementation</column>
    ///             <column name="IsPartial" type="bool">Whether the method is partial</column>
    ///             <column name="MethodKind" type="string">Kind of method (Ordinary, PropertyGet, etc.)</column>
    ///             <column name="Callers" type="CalledMethodInfo[]">Methods that call this method</column>
    ///             <column name="CallerCount" type="int">Number of callers</column>
    ///             <column name="DataFlow" type="DataFlowInfo">Data flow analysis for the method body</column>
    ///             <column name="ControlFlow" type="ControlFlowInfo">Control flow analysis for the method body</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent property of class</description>
    ///         <columns type="PropertyEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="Type" type="string">Type</column>
    ///             <column name="FullTypeName" type="string">Full type name including namespace</column>
    ///             <column name="IsIndexer" type="bool">Is indexer</column>
    ///             <column name="IsReadOnly" type="bool">Is read only</column>
    ///             <column name="IsWriteOnly" type="bool">Is write only</column>
    ///             <column name="IsRequired" type="bool">Is required</column>
    ///             <column name="IsWithEvents" type="bool">Is with events</column>
    ///             <column name="IsVirtual" type="bool">Is virtual</column>
    ///             <column name="IsOverride" type="bool">Is override</column>
    ///             <column name="IsAbstract" type="bool">Is abstract</column>
    ///             <column name="IsSealed" type="bool">Is sealed</column>
    ///             <column name="IsStatic" type="bool">Is static</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="IsAutoProperty" type="bool">Whether the property is an auto-implemented property</column>
    ///             <column name="HasGetter" type="bool">Whether the property has a get accessor</column>
    ///             <column name="HasSetter" type="bool">Whether the property has a set accessor (includes init)</column>
    ///             <column name="HasInitSetter" type="bool">Whether the property has an init accessor specifically</column>
    ///             <column name="ReferencedTypes" type="TypeReferenceEntity[]">Types referenced in property accessor bodies with usage context</column>
    ///             <column name="LocalVariables" type="VariableEntity[]">Local variables declared in property accessor bodies</column>
    ///             <column name="LocalVariableCount" type="int">Number of local variables in property accessor bodies</column>
    ///             <column name="StartLine" type="int">Start line of the property declaration</column>
    ///             <column name="EndLine" type="int">End line of the property declaration</column>
    ///             <column name="SourceFilePath" type="string">Source file path of the property</column>
    ///             <column name="ContainingTypeName" type="string">Name of the containing type</column>
    ///             <column name="ContainingNamespace" type="string">Namespace of the containing type</column>
    ///             <column name="HasDocumentation" type="bool">Whether the property has XML documentation</column>
    ///             <column name="DefaultValue" type="string">Default value initializer text</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///             <column name="ExplicitInterfaceImplementations" type="string[]">Explicit interface implementations</column>
    ///             <column name="IsExplicitInterfaceImplementation" type="bool">Whether the property is an explicit interface implementation</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent parameter of method</description>
    ///         <columns type="ParameterEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="Type" type="string">Type</column>
    ///             <column name="FullTypeName" type="string">Full type name including namespace</column>
    ///             <column name="IsOptional" type="bool">Is optional</column>
    ///             <column name="IsParams" type="bool">Is params</column>
    ///             <column name="IsThis" type="bool">Is this</column>
    ///             <column name="IsDiscard" type="bool">Is discard</column>
    ///             <column name="IsIn" type="bool">Is in</column>
    ///             <column name="IsOut" type="bool">Is out</column>
    ///             <column name="IsRef" type="bool">Is ref</column>
    ///             <column name="IsByRef" type="bool">Is by ref</column>
    ///             <column name="IsByValue" type="bool">Is by value</column>
    ///             <column name="IsUsed" type="bool?">Whether parameter is used in method body</column>
    ///             <column name="Ordinal" type="int">Zero-based ordinal position of the parameter</column>
    ///             <column name="HasDefaultValue" type="bool">Whether the parameter has a default value</column>
    ///             <column name="DefaultValue" type="string">Default value text</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent local variable in a method</description>
    ///         <columns type="VariableEntity">
    ///             <column name="Name" type="string">Variable name</column>
    ///             <column name="Type" type="string">Variable type</column>
    ///             <column name="FullTypeName" type="string">Full type name including namespace</column>
    ///             <column name="IsConst" type="bool">Whether variable is const</column>
    ///             <column name="IsRef" type="bool">Whether variable has ref modifier</column>
    ///             <column name="IsFixed" type="bool">Whether variable has fixed modifier</column>
    ///             <column name="HasInitializer" type="bool">Whether variable has an initializer</column>
    ///             <column name="InitializerText" type="string">Initializer expression text</column>
    ///             <column name="IsUsed" type="bool">Whether variable is used after declaration</column>
    ///             <column name="LineNumber" type="int">Line number where declared</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent project reference</description>
    ///         <columns type="ProjectReferenceEntity">
    ///             <column name="Name" type="string">Name</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent library reference</description>
    ///         <columns type="LibraryReferenceEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="Version" type="string">Version</column>
    ///             <column name="Culture" type="string">Culture</column>
    ///             <column name="Location" type="string">Location</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent type within project</description>
    ///         <columns type="TypeEntity">
    ///             <column name="Name" type="string">Name</column>
    ///             <column name="FullName" type="string">Full name</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="IsInterface" type="bool">Is interface</column>
    ///             <column name="IsClass" type="bool">Is class</column>
    ///             <column name="IsEnum" type="bool">Is enum</column>
    ///             <column name="IsStruct" type="bool">Is struct</column>
    ///             <column name="IsAbstract" type="bool">Is abstract</column>
    ///             <column name="IsSealed" type="bool">Is sealed</column>
    ///             <column name="IsStatic" type="bool">Is static</column>
    ///             <column name="IsNested" type="bool">Is nested</column>
    ///             <column name="IsGenericType" type="bool">Is generic type</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Methods" type="MethodEntity[]">Methods</column>
    ///             <column name="Properties" type="PropertyEntity[]">Properties</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent nuget package</description>
    ///         <columns type="NugetPackageEntity">
    ///             <column name="Id" type="string">Package ID</column>
    ///             <column name="Version" type="string">Package version</column>
    ///             <column name="LicenseUrl" type="string">License URL</column>
    ///             <column name="ProjectUrl" type="string">Project URL</column>
    ///             <column name="Title" type="string">Package title</column>
    ///             <column name="Authors" type="string">Package authors</column>
    ///             <column name="Owners" type="string">Package owners</column>
    ///             <column name="RequireLicenseAcceptance" type="bool">License acceptance required</column>
    ///             <column name="Description" type="string">Package description</column>
    ///             <column name="Summary" type="string">Package summary</column>
    ///             <column name="ReleaseNotes" type="string">Release notes</column>
    ///             <column name="Copyright" type="string">Copyright info</column>
    ///             <column name="Language" type="string">Language</column>
    ///             <column name="Tags" type="string">Tags</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent field of class or struct</description>
    ///         <columns type="FieldEntity">
    ///             <column name="Name" type="string">Field name</column>
    ///             <column name="Type" type="string">Field type</column>
    ///             <column name="FullTypeName" type="string">Full type name including namespace</column>
    ///             <column name="IsReadOnly" type="bool">Whether the field is readonly</column>
    ///             <column name="IsConst" type="bool">Whether the field is const</column>
    ///             <column name="IsStatic" type="bool">Whether the field is static</column>
    ///             <column name="IsVolatile" type="bool">Whether the field is volatile</column>
    ///             <column name="HasInitializer" type="bool">Whether the field has an initializer</column>
    ///             <column name="Accessibility" type="string">Accessibility level</column>
    ///             <column name="Modifiers" type="string[]">Modifiers</column>
    ///             <column name="Attributes" type="AttributeEntity[]">Attributes</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent constructor of class or struct</description>
    ///         <columns type="ConstructorEntity">
    ///             <column name="Name" type="string">Containing type name</column>
    ///             <column name="IsStatic" type="bool">Whether it is a static constructor</column>
    ///             <column name="IsImplicitlyDeclared" type="bool">Whether it is default constructor</column>
    ///             <column name="Accessibility" type="string">Accessibility level</column>
    ///             <column name="ParameterCount" type="int">Number of parameters</column>
    ///             <column name="Parameters" type="ParameterEntity[]">Constructor parameters</column>
    ///             <column name="HasBody" type="bool">Whether constructor has a body</column>
    ///             <column name="StatementsCount" type="int">Number of statements</column>
    ///             <column name="LinesOfCode" type="int">Lines of code</column>
    ///             <column name="HasInitializer" type="bool">Whether it calls this() or base()</column>
    ///             <column name="InitializerKind" type="string">Type of initializer (this or base)</column>
    ///             <column name="ReferencedTypes" type="TypeReferenceEntity[]">Types referenced in constructor body with usage context</column>
    ///             <column name="LocalVariables" type="VariableEntity[]">Local variables declared in constructor body</column>
    ///             <column name="LocalVariableCount" type="int">Number of local variables in constructor body</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent struct declaration</description>
    ///         <columns type="StructEntity">
    ///             <column name="Name" type="string">Struct name</column>
    ///             <column name="FullName" type="string">Full name including namespace</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="IsReadOnly" type="bool">Whether it is readonly struct</column>
    ///             <column name="IsRefStruct" type="bool">Whether it is ref struct</column>
    ///             <column name="IsRecordStruct" type="bool">Whether it is record struct</column>
    ///             <column name="MethodsCount" type="int">Number of methods</column>
    ///             <column name="PropertiesCount" type="int">Number of properties</column>
    ///             <column name="FieldsCount" type="int">Number of fields</column>
    ///             <column name="ConstructorsCount" type="int">Number of constructors</column>
    ///             <column name="LinesOfCode" type="int">Lines of code</column>
    ///             <column name="Methods" type="MethodEntity[]">Methods</column>
    ///             <column name="Properties" type="PropertyEntity[]">Properties</column>
    ///             <column name="Fields" type="FieldEntity[]">Fields</column>
    ///             <column name="Constructors" type="ConstructorEntity[]">Constructors</column>
    ///             <column name="IsPartial" type="bool">Whether the struct is partial</column>
    ///             <column name="AllInterfaces" type="string[]">All interfaces including transitive (fully qualified)</column>
    ///             <column name="Events" type="EventEntity[]">Events declared in the struct</column>
    ///             <column name="EventsCount" type="int">Number of events</column>
    ///             <column name="HasDocumentation" type="bool">Whether the struct has XML documentation</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent event declaration</description>
    ///         <columns type="EventEntity">
    ///             <column name="Name" type="string">Event name</column>
    ///             <column name="Type" type="string">Event delegate type</column>
    ///             <column name="IsStatic" type="bool">Whether it is static</column>
    ///             <column name="IsVirtual" type="bool">Whether it is virtual</column>
    ///             <column name="IsAbstract" type="bool">Whether it is abstract</column>
    ///             <column name="IsOverride" type="bool">Whether it is override</column>
    ///             <column name="Accessibility" type="string">Accessibility level</column>
    ///             <column name="HasExplicitAccessors" type="bool">Whether it has add/remove accessors</column>
    ///             <column name="IsFieldLike" type="bool">Whether it is field-like event</column>
    ///             <column name="ReferenceCount" type="int?">Number of references to the event (null if unavailable)</column>
    ///             <column name="IsUsed" type="bool?">Whether the event is referenced (null if unavailable)</column>
    ///             <column name="HasDocumentation" type="bool">Whether the event has XML documentation</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent local function within a method</description>
    ///         <columns type="LocalFunctionEntity">
    ///             <column name="Name" type="string">Function name</column>
    ///             <column name="ReturnType" type="string">Return type</column>
    ///             <column name="IsAsync" type="bool">Whether it is async</column>
    ///             <column name="IsStatic" type="bool">Whether it is static</column>
    ///             <column name="ParameterCount" type="int">Number of parameters</column>
    ///             <column name="Parameters" type="ParameterEntity[]">Parameters</column>
    ///             <column name="HasBody" type="bool">Whether it has a body</column>
    ///             <column name="StatementsCount" type="int">Number of statements</column>
    ///             <column name="LinesOfCode" type="int">Lines of code</column>
    ///             <column name="CyclomaticComplexity" type="int">Cyclomatic complexity</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent using directive in document</description>
    ///         <columns type="UsingDirectiveEntity">
    ///             <column name="Name" type="string">Namespace or type being imported</column>
    ///             <column name="IsStatic" type="bool">Whether it is static using</column>
    ///             <column name="IsGlobal" type="bool">Whether it is global using</column>
    ///             <column name="Alias" type="string">Alias name if present</column>
    ///             <column name="HasAlias" type="bool">Whether it has an alias</column>
    ///             <column name="LineNumber" type="int">Line number</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent type reference within a code body (method, constructor, or property accessor)</description>
    ///         <columns type="TypeReferenceEntity">
    ///             <column name="Name" type="string">Referenced type name</column>
    ///             <column name="Namespace" type="string">Referenced type namespace</column>
    ///             <column name="FullName" type="string">Fully qualified type name</column>
    ///             <column name="Kind" type="string">Type kind (Class, Interface, Enum, Struct, Delegate)</column>
    ///             <column name="UsageKind" type="string">How the type is used (Cast, Is, As, PatternMatch, LocalVariable, ObjectCreation, GenericArgument, TypeOf, Default, ArrayCreation, CatchDeclaration, Other)</column>
    ///             <column name="LineNumber" type="int">Line number where the reference occurs</column>
    ///             <column name="IsInterface" type="bool">Whether the referenced type is an interface</column>
    ///             <column name="IsClass" type="bool">Whether the referenced type is a class</column>
    ///             <column name="IsEnum" type="bool">Whether the referenced type is an enum</column>
    ///             <column name="IsStruct" type="bool">Whether the referenced type is a struct</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent enum member (constant value)</description>
    ///         <columns type="EnumMemberEntity">
    ///             <column name="Name" type="string">Enum member name</column>
    ///             <column name="Value" type="int">Ordinal value of the enum member</column>
    ///             <column name="ConstantValue" type="string">String representation of the constant value</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent delegate declaration</description>
    ///         <columns type="DelegateEntity">
    ///             <column name="Name" type="string">Delegate name</column>
    ///             <column name="FullName" type="string">Fully qualified delegate name</column>
    ///             <column name="Namespace" type="string">Namespace</column>
    ///             <column name="ReturnType" type="string">Return type</column>
    ///             <column name="FullReturnType" type="string">Full return type name</column>
    ///             <column name="ParameterCount" type="int">Number of parameters</column>
    ///             <column name="Parameters" type="ParameterEntity[]">Parameters</column>
    ///             <column name="IsGeneric" type="bool">Whether the delegate is generic</column>
    ///             <column name="TypeParameters" type="string[]">Type parameters</column>
    ///             <column name="TypeParameterCount" type="int">Number of type parameters</column>
    ///             <column name="Accessibility" type="string">Accessibility level</column>
    ///             <column name="HasDocumentation" type="bool">Whether it has XML documentation</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent compiler diagnostic (error, warning, info)</description>
    ///         <columns type="DiagnosticEntity">
    ///             <column name="Id" type="string">Diagnostic ID (e.g. CS0168)</column>
    ///             <column name="Message" type="string">Diagnostic message</column>
    ///             <column name="Severity" type="string">Severity level (Error, Warning, Info, Hidden)</column>
    ///             <column name="IsError" type="bool">Whether it is an error</column>
    ///             <column name="IsWarning" type="bool">Whether it is a warning</column>
    ///             <column name="Category" type="string">Diagnostic category</column>
    ///             <column name="StartLine" type="int">Start line number</column>
    ///             <column name="EndLine" type="int">End line number</column>
    ///             <column name="FilePath" type="string">File path</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Represent type parameter constraint</description>
    ///         <columns type="TypeParameterConstraintEntity">
    ///             <column name="Name" type="string">Type parameter name</column>
    ///             <column name="Ordinal" type="int">Ordinal position</column>
    ///             <column name="HasReferenceTypeConstraint" type="bool">Has class constraint</column>
    ///             <column name="HasValueTypeConstraint" type="bool">Has struct constraint</column>
    ///             <column name="HasUnmanagedTypeConstraint" type="bool">Has unmanaged constraint</column>
    ///             <column name="HasNotNullConstraint" type="bool">Has notnull constraint</column>
    ///             <column name="HasConstructorConstraint" type="bool">Has new() constraint</column>
    ///             <column name="ConstraintTypes" type="string[]">Type constraints (e.g. IDisposable)</column>
    ///             <column name="ConstraintSummary" type="string">Human-readable constraint summary</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Data flow analysis information for a method body</description>
    ///         <columns type="DataFlowInfo">
    ///             <column name="ReadInside" type="string[]">Variables read inside the body</column>
    ///             <column name="WrittenInside" type="string[]">Variables written inside the body</column>
    ///             <column name="ReadOutside" type="string[]">Variables read outside the body</column>
    ///             <column name="WrittenOutside" type="string[]">Variables written outside the body</column>
    ///             <column name="VariablesDeclared" type="string[]">Variables declared in the body</column>
    ///             <column name="Captured" type="string[]">Captured variables (closures)</column>
    ///             <column name="AlwaysAssigned" type="string[]">Variables always assigned</column>
    ///             <column name="CapturedCount" type="int">Number of captured variables</column>
    ///             <column name="ReadInsideCount" type="int">Number of variables read inside</column>
    ///             <column name="WrittenInsideCount" type="int">Number of variables written inside</column>
    ///         </columns>
    ///     </additional-table>
    ///     <additional-table>
    ///         <description>Control flow analysis information for a method body</description>
    ///         <columns type="ControlFlowInfo">
    ///             <column name="StartPointIsReachable" type="bool">Whether the start point is reachable</column>
    ///             <column name="EndPointIsReachable" type="bool">Whether the end point is reachable</column>
    ///             <column name="ReturnStatementCount" type="int">Number of return statements</column>
    ///             <column name="ExitPointCount" type="int">Number of exit points</column>
    ///             <column name="EntryPointCount" type="int">Number of entry points</column>
    ///         </columns>
    ///     </additional-table>
    /// </additional-tables>
    public CSharpSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
        AddSource<CSharpImmediateLoadSolutionRowsSource>("file");
        AddTable<CSharpSolutionTable>("file");

        _createNugetPropertiesResolver = (baseUrl, client) => new NuGetPropertiesResolver(baseUrl, client);
    }

    internal CSharpSchema(Func<string, IHttpClient?, INuGetPropertiesResolver> createNuGetPropertiesResolver)
        : this()
    {
        _createNugetPropertiesResolver = createNuGetPropertiesResolver;
    }

    private static ConcurrentDictionary<string, Solution> Solutions => SolutionOperationsCommand.Solutions;

    internal static string DefaultNugetCacheDirectoryPath { get; } =
        IFileSystem.Combine(SolutionOperationsCommand.DefaultCacheDirectoryPath, "NuGet");

    private static ConcurrentDictionary<string, PersistentCacheResponseHandler> HttpResponseCache =>
        SolutionOperationsCommand.HttpResponseCache;

    private static ResolveValueStrategy ResolveValueStrategy => SolutionOperationsCommand.ResolveValueStrategy;

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "solution" => new CSharpSolutionTable(),
            _ => base.GetTableByName(name, runtimeContext, parameters)
        };
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
        string? externalNugetPropertiesResolveEndpoint = null;

        if (runtimeContext.EnvironmentVariables.TryGetValue("EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT",
                out var nugetPropertiesResolveEndpointValue))
            externalNugetPropertiesResolveEndpoint = nugetPropertiesResolveEndpointValue;

        string? internalNugetPropertiesResolveEndpoint = null;

        if (runtimeContext.EnvironmentVariables.TryGetValue("MUSOQ_SERVER_HTTP_ENDPOINT",
                out var internalNugetPropertiesResolveEndpointValue))
            internalNugetPropertiesResolveEndpoint = internalNugetPropertiesResolveEndpointValue;

        if (internalNugetPropertiesResolveEndpoint == null)
            throw new InvalidOperationException("MUSOQ_SERVER_HTTP_ENDPOINT environment variable is not set.");

        var cacheDirectory = DefaultNugetCacheDirectoryPath;

        if (!IFileSystem.DirectoryExists(cacheDirectory)) IFileSystem.CreateDirectory(cacheDirectory);

        if (runtimeContext.EnvironmentVariables.TryGetValue("CACHE_DIRECTORY", out var incommingCacheDirectory) &&
            incommingCacheDirectory is not null)
        {
            if (!Directory.Exists(incommingCacheDirectory))
                throw new DirectoryNotFoundException($"Cache directory '{incommingCacheDirectory}' does not exist.");

            cacheDirectory = incommingCacheDirectory;
        }

        runtimeContext.Logger.LogTrace("Using cache directory: {CacheDirectory}", cacheDirectory);

        var httpClient = WithCacheDirectory(
            cacheDirectory,
            configs => GetDomains(configs, runtimeContext.EnvironmentVariables),
            runtimeContext.Logger
        );

        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();

        switch (name.ToLowerInvariant())
        {
            case "solution":
            {
                if (Solutions.TryGetValue((string)parameters[0], out var solution))
                {
                    var solutionEntity = new SolutionEntity(
                        solution,
                        new NuGetPackageMetadataRetriever(
                            new NuGetCachePathResolver(
                                solution.FilePath,
                                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                    ? OSPlatform.Windows
                                    : OSPlatform.Linux,
                                runtimeContext.Logger
                            ),
                            externalNugetPropertiesResolveEndpoint,
                            new NuGetRetrievalService(
                                _createNugetPropertiesResolver(internalNugetPropertiesResolveEndpoint, httpClient),
                                FileSystem,
                                httpClient
                            ),
                            FileSystem,
                            packageVersionConcurrencyManager,
                            SolutionOperationsCommand.BannedPropertiesValues,
                            ResolveValueStrategy,
                            runtimeContext.Logger
                        ),
                        runtimeContext.EndWorkToken
                    );

                    return new CSharpInMemorySolutionRowsSource(
                        solutionEntity,
                        httpClient,
                        FileSystem,
                        externalNugetPropertiesResolveEndpoint,
                        _createNugetPropertiesResolver(internalNugetPropertiesResolveEndpoint, httpClient),
                        runtimeContext.Logger,
                        runtimeContext
                    );
                }

                return new CSharpImmediateLoadSolutionRowsSource(
                    (string)parameters[0],
                    httpClient,
                    FileSystem,
                    externalNugetPropertiesResolveEndpoint,
                    _createNugetPropertiesResolver(internalNugetPropertiesResolveEndpoint, httpClient),
                    runtimeContext.Logger,
                    runtimeContext
                );
            }
        }

        return base.GetRowSource(name, runtimeContext, parameters);
    }

    private static IReadOnlyDictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig> GetDomains(
        IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
            DomainRateLimitingHandler.DomainRateLimitConfig> configs,
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        var accessTokens = ExtractAccessTokens(environmentVariables);
        var domains = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>();

        foreach (var unauthenticatedConfigs in configs.Where(f => !f.Key.HasApiKey))
            domains[unauthenticatedConfigs.Key.Domain] = unauthenticatedConfigs.Value;

        foreach (var authenticatedConfigs in configs.Where(f => f.Key.HasApiKey))
            if (accessTokens.ContainsKey(authenticatedConfigs.Key.Domain))
                domains[authenticatedConfigs.Key.Domain] = authenticatedConfigs.Value;

        return domains;
    }

    private static IHttpClient WithCacheDirectory(
        string cacheDirectory,
        Func<IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
                DomainRateLimitingHandler.DomainRateLimitConfig>,
            IReadOnlyDictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>> getDomains,
        ILogger logger
    )
    {
        var cachedResponseHandler = HttpResponseCache.AddOrUpdate(cacheDirectory,
            _ => new PersistentCacheResponseHandler(cacheDirectory, new SingleQueryCacheResponseHandler(
                    new DomainRateLimitingHandler(
                        getDomains(SolutionOperationsCommand.RateLimitingOptions ??
                                   throw new InvalidOperationException("Rate limiting options are not set.")),
                        new DomainRateLimitingHandler.DomainRateLimitConfig(
                            10,
                            TimeSpan.FromSeconds(1),
                            100), false, logger)),
                logger),
            (key, handler) =>
            {
                if (key == DefaultNugetCacheDirectoryPath && handler.InnerHandler is HttpClientHandler)
                    handler.InnerHandler = new SingleQueryCacheResponseHandler(
                        new DomainRateLimitingHandler(
                            getDomains(SolutionOperationsCommand.RateLimitingOptions ??
                                       throw new InvalidOperationException("Rate limiting options are not set.")),
                            new DomainRateLimitingHandler.DomainRateLimitConfig(
                                10,
                                TimeSpan.FromSeconds(1),
                                100), false, logger));

                return handler;
            });

        return new DefaultHttpClient(() => new HttpClient(cachedResponseHandler));
    }

    private static IReadOnlyDictionary<string, string> ExtractAccessTokens(
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        var accessTokens = new Dictionary<string, string>();

        foreach (var environmentVariable in environmentVariables)
        {
            if (environmentVariable.Key == "GITHUB_API_KEY")
                accessTokens.Add("github", environmentVariable.Value);

            if (environmentVariable.Key == "GITLAB_API_KEY")
                accessTokens.Add("gitlab", environmentVariable.Value);
        }

        return accessTokens;
    }

    /// <summary>
    ///     Gets raw information's about specific method in the schema.
    /// </summary>
    /// <param name="methodName">Method name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName switch
        {
            "solution" => CreateSolutionMethodInfo(),
            _ => throw new NotSupportedException($"Method '{methodName}' is not supported. Available methods: solution")
        };
    }

    /// <summary>
    ///     Gets raw information's about all tables in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return CreateSolutionMethodInfo();
    }

    private static SchemaMethodInfo[] CreateSolutionMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string))
            ]);

        return
        [
            new SchemaMethodInfo("solution", constructorInfo)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new CSharpLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}