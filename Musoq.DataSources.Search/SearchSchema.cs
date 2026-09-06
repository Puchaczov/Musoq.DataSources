using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Tables;

namespace Musoq.DataSources.Search;

/// <description>
///     Provides deterministic literal and raw-byte search over files below a requested root.
/// </description>
/// <short-description>
///     Provides deterministic literal and raw-byte search over files below a requested root.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
/// <summary>Provides deterministic Search sources for literal matches, raw-byte occurrences and eligible paths.</summary>
public sealed class SearchSchema : SchemaBase
{
    private const string SchemaName = "search";
    private const string MatchesTable = "matches";
    private const string LinesTable = "lines";
    private const string FilesTable = "files";
    private const string CountsTable = "counts";
    private const string PathsTable = "paths";
    private const string ManyTable = "many";
    private const string BytesTable = "bytes";
    private const string AuditTable = "audit";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Root file or directory</virtual-param>
    ///         <virtual-param>Literal text to find</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>search.matches(string root, string literal)</from>
    ///                 <description>Returns one typed row for every non-overlapping literal occurrence.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                     <column name="MatchIndex" type="long">Zero-based occurrence ordinal within the path</column>
    ///                     <column name="ByteOffset" type="long?">Original-byte start when the decoded span is losslessly mapped</column>
    ///                     <column name="ByteLength" type="long?">Original-byte length when the decoded span is losslessly mapped</column>
    ///                     <column name="LineNumber" type="long">One-based physical line containing the occurrence</column>
    ///                     <column name="Utf16Column" type="long">Zero-based UTF-16 column within the line</column>
    ///                     <column name="Utf16Length" type="long">Match length in UTF-16 code units</column>
    ///                     <column name="MatchText" type="string">Matched text when retained by the projection</column>
    ///                     <column name="Captures" type="IReadOnlyList&lt;SearchCapture&gt;">Typed regular-expression captures; empty for literal matches</column>
    ///                     <column name="Context" type="IReadOnlyList&lt;SearchContextLine&gt;">Bounded adjacent physical lines; empty when context is disabled or not projected</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.many(string root, string request)</from>
    ///                 <description>Returns one typed row for every non-overlapping literal occurrence of each labeled pattern in a bounded scalar JSON request.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="PatternId" type="string">The request label for the matched pattern</column>
    ///                     <column name="MatchIndex" type="long">Zero-based occurrence ordinal within the path and pattern label</column>
    ///                     <column name="ByteOffset" type="long?">Original-byte start when the decoded span is losslessly mapped</column>
    ///                     <column name="ByteLength" type="long?">Original-byte length when the decoded span is losslessly mapped</column>
    ///                     <column name="LineNumber" type="long">One-based physical line containing the occurrence</column>
    ///                     <column name="Utf16Column" type="long">Zero-based UTF-16 column within the line</column>
    ///                     <column name="Utf16Length" type="long">Match length in UTF-16 code units</column>
    ///                     <column name="MatchText" type="string">Matched literal text</column>
    ///                     <column name="Captures" type="IReadOnlyList&lt;SearchCapture&gt;">Empty for the literal-only many source</column>
    ///                     <column name="Context" type="IReadOnlyList&lt;SearchContextLine&gt;">Bounded adjacent physical lines; empty for the current many request contract</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.bytes(string root, string patternJson)</from>
    ///                 <description>Returns one typed row for every leftmost, non-overlapping raw-byte occurrence described by the versioned pattern JSON.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern source</column>
    ///                     <column name="MatchIndex" type="long">Zero-based occurrence ordinal within the path</column>
    ///                     <column name="ByteOffset" type="long">Original input byte start</column>
    ///                     <column name="ByteLength" type="long">Original input byte length</column>
    ///                     <column name="MatchedBytes" type="byte[]">Matched source bytes when requested by the projection</column>
    ///                     <column name="WindowStartByteOffset" type="long">Actual bounded window start after beginning-of-file clipping</column>
    ///                     <column name="WindowByteLength" type="long">Actual bounded window length after boundary clipping</column>
    ///                     <column name="WindowComplete" type="bool">False when the requested before/after bounds were clipped at a file boundary</column>
    ///                     <column name="WindowBytes" type="byte[]">Bounded same-read window bytes when requested by the projection</column>
    ///                     <column name="LineNumber" type="long">Null for raw byte matches</column>
    ///                     <column name="Utf16Column" type="long">Null for raw byte matches</column>
    ///                     <column name="Utf16Length" type="long">Null for raw byte matches</column>
    ///                     <column name="MatchText" type="string">Null for raw byte matches</column>
    ///                     <column name="LineText" type="string">Null for raw byte matches</column>
    ///                     <column name="Captures" type="IReadOnlyList&lt;SearchCapture&gt;">Empty for raw byte matches</column>
    ///                     <column name="Context" type="IReadOnlyList&lt;SearchContextLine&gt;">Empty for raw byte matches</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.lines(string root, string literal)</from>
    ///                 <description>Returns one typed row for every physical line containing one or more non-overlapping literal occurrences.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                     <column name="LineNumber" type="long">One-based physical line number</column>
    ///                     <column name="ByteOffset" type="long?">Original-byte start when the decoded line is losslessly mapped</column>
    ///                     <column name="LineText" type="string">Decoded physical line including its terminator when available</column>
    ///                     <column name="OccurrenceCount" type="long">Number of occurrences on the physical line</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.files(string root, string literal)</from>
    ///                 <description>Returns one typed row for every eligible file containing at least one literal occurrence.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.counts(string root, string literal)</from>
    ///                 <description>Returns one exact count row for every eligible file completed by the scan, including zero-hit files.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                     <column name="OccurrenceCount" type="long">Number of occurrences in the file</column>
    ///                     <column name="MatchingLineCount" type="long">Number of physical lines containing an occurrence</column>
    ///                     <column name="BytesScanned" type="long">Original input bytes scanned for the file</column>
    ///                     <column name="Complete" type="bool">True for every emitted exact count row</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.paths(string root)</from>
    ///                 <description>Returns one typed row for every eligible regular-file path without opening file content.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="EntryKind" type="string">The stable local entry kind, file</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.audit(string root, string literal)</from>
    ///                 <description>Executes a fresh count scan and returns one typed terminal summary row for that execution.</description>
    ///                 <columns>
    ///                     <column name="Root" type="string">Requested root representation</column>
    ///                     <column name="ScanId" type="string">Opaque identifier for this execution</column>
    ///                     <column name="ScopeFingerprint" type="string">Stable fingerprint of the requested scope</column>
    ///                     <column name="Outcome" type="SearchOutcome">Terminal execution outcome</column>
    ///                     <column name="TerminalReason" type="string">Stable terminal reason</column>
    ///                     <column name="Complete" type="bool">Whether the result is complete under the outcome contract</column>
    ///                     <column name="ScopeExhausted" type="bool">Whether the eligible scope was exhausted</column>
    ///                     <column name="QuerySatisfied" type="bool">Whether an explicit positive query demand was satisfied</column>
    ///                     <column name="CountsExact" type="bool">Whether counters are exact for the resolved scope</column>
    ///                     <column name="ScopeResolved" type="bool">Whether the requested root was resolved</column>
    ///                     <column name="VisitedFiles" type="long">Visited file candidates</column>
    ///                     <column name="EligibleFiles" type="long">Eligible file candidates</column>
    ///                     <column name="FilesOpened" type="long">Content files opened</column>
    ///                     <column name="FilesRead" type="long">Files whose readers opened successfully</column>
    ///                     <column name="FilesCompleted" type="long">Files completed by the scan</column>
    ///                     <column name="FilesFailed" type="long">Files that failed during processing</column>
    ///                     <column name="BinaryFilesSkipped" type="long">Files skipped by binary policy</column>
    ///                     <column name="BytesScanned" type="long">Bytes attributed to completed text-file scans</column>
    ///                     <column name="FilesMatched" type="long">Completed files with observed matches</column>
    ///                     <column name="MatchingLines" type="long">Distinct matching physical lines</column>
    ///                     <column name="Occurrences" type="long">Matcher occurrences observed</column>
    ///                     <column name="ObservedRows" type="long">Rows observed by the underlying count scan</column>
    ///                     <column name="FailureCode" type="string">Bounded failure code when execution failed or was partial</column>
    ///                     <column name="FailurePath" type="string">Bounded failure path when available</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    /// <summary>Initializes the Search schema and registers its library methods.</summary>
    public SearchSchema()
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    ///     Gets the table metadata for the requested Search source.
    /// </summary>
    /// <param name="name">Data source name.</param>
    /// <param name="metadataContext">Metadata context.</param>
    /// <param name="parameters">Parameters to pass to the data source.</param>
    /// <returns>Requested table metadata.</returns>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            MatchesTable or ManyTable => new SearchMatchesTable(),
            LinesTable => new SearchLinesTable(),
            FilesTable => new SearchFilesTable(),
            CountsTable => new SearchCountsTable(),
            PathsTable => new SearchPathsTable(),
            BytesTable => new SearchBytesTable(),
            AuditTable => new SearchAuditsTable(),
            _ => throw new TableNotFoundException(nameof(name))
        };
    }

    /// <summary>
    ///     Gets the Search row source for the requested constructor.
    /// </summary>
    /// <typeparam name="T">Requested row type.</typeparam>
    /// <param name="name">Data source name.</param>
    /// <param name="executionContext">Execution context.</param>
    /// <param name="parameters">Source-specific parameters.</param>
    /// <returns>Typed Search row source.</returns>
    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            MatchesTable => EnsureSourceType<T, SearchMatch>(
                name,
                new SearchMatchesSource(
                    SearchRequest.FromSourceArguments(parameters),
                    executionContext)),
            ManyTable => EnsureSourceType<T, SearchMatch>(
                name,
                CreateManySource(parameters, executionContext)),
            LinesTable => EnsureSourceType<T, SearchLine>(
                name,
                new SearchLinesSource(
                    SearchRequest.FromSourceArguments(parameters),
                    executionContext)),
            FilesTable => EnsureSourceType<T, SearchFile>(
                name,
                new SearchFilesSource(
                    SearchRequest.FromSourceArguments(parameters),
                    executionContext)),
            CountsTable => EnsureSourceType<T, SearchCount>(
                name,
                new SearchCountsSource(
                    SearchRequest.FromSourceArguments(parameters),
                    executionContext)),
            PathsTable => EnsureSourceType<T, SearchPath>(
                name,
                new SearchPathsSource(
                    RequirePathArguments(parameters),
                    executionContext)),
            BytesTable => EnsureSourceType<T, SearchByteMatch>(
                name,
                CreateBytesSource(parameters, executionContext)),
            AuditTable => EnsureSourceType<T, SearchAudit>(
                name,
                new SearchAuditSource(
                    SearchRequest.FromSourceArguments(parameters),
                    executionContext)),
            _ => throw new SourceNotFoundException(nameof(name))
        };
    }

    /// <summary>
    ///     Describes the resolved Search source without reading its content.
    /// </summary>
    /// <param name="name">Data source name.</param>
    /// <param name="context">Source description context.</param>
    /// <param name="parameters">Source-specific parameters.</param>
    /// <returns>Static source descriptor.</returns>
    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    /// <summary>
    ///     Describes Search runtime settings.
    /// </summary>
    /// <param name="name">Data source name.</param>
    /// <param name="context">Runtime settings description context.</param>
    /// <param name="parameters">Root and literal parameters.</param>
    /// <returns>No runtime settings for the minimal source.</returns>
    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return [];
    }

    /// <summary>
    ///     Plans the Search source's conservative scalar predicate pushdown.
    /// </summary>
    /// <param name="name">Data source name.</param>
    /// <param name="request">Source planning request.</param>
    /// <param name="parameters">Root and literal parameters.</param>
    /// <returns>Source plan with accepted scalar predicates and residual work.</returns>
    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return SearchSourcePlanner.Plan(name, request);
    }

    /// <summary>
    ///     Gets constructor metadata for all Search data sources.
    /// </summary>
    /// <returns>Search data source constructors.</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return [
            CreateMatchesMethodInfo(),
            CreateManyMethodInfo(),
            CreateLinesMethodInfo(),
            CreateFilesMethodInfo(),
            CreateCountsMethodInfo(),
            CreatePathsMethodInfo(),
            CreateBytesMethodInfo(),
            CreateAuditMethodInfo()
        ];
    }

    /// <summary>
    ///     Gets raw constructor metadata for a Search data source method.
    /// </summary>
    /// <param name="methodName">Data source method name.</param>
    /// <param name="metadataContext">Metadata context.</param>
    /// <returns>Matching constructor metadata.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            MatchesTable => [CreateMatchesMethodInfo()],
            ManyTable => [CreateManyMethodInfo()],
            LinesTable => [CreateLinesMethodInfo()],
            FilesTable => [CreateFilesMethodInfo()],
            CountsTable => [CreateCountsMethodInfo()],
            PathsTable => [CreatePathsMethodInfo()],
            BytesTable => [CreateBytesMethodInfo()],
            AuditTable => [CreateAuditMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {MatchesTable}, {ManyTable}, {LinesTable}, {FilesTable}, {CountsTable}, {PathsTable}, {BytesTable}, {AuditTable}")
        };
    }

    /// <summary>
    ///     Gets raw constructor metadata for all Search data source methods.
    /// </summary>
    /// <param name="metadataContext">Metadata context.</param>
    /// <returns>Search data source constructors.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return [
            CreateMatchesMethodInfo(),
            CreateManyMethodInfo(),
            CreateLinesMethodInfo(),
            CreateFilesMethodInfo(),
            CreateCountsMethodInfo(),
            CreatePathsMethodInfo(),
            CreateBytesMethodInfo(),
            CreateAuditMethodInfo()
        ];
    }

    private static SchemaMethodInfo CreateMatchesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("literal", typeof(string))
            ]);

        return new SchemaMethodInfo(MatchesTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateManyMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("request", typeof(string))
            ]);

        return new SchemaMethodInfo(ManyTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateLinesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("literal", typeof(string))
            ]);

        return new SchemaMethodInfo(LinesTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateFilesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("literal", typeof(string))
            ]);

        return new SchemaMethodInfo(FilesTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateCountsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("literal", typeof(string))
            ]);

        return new SchemaMethodInfo(CountsTable, constructorInfo);
    }

    private static SchemaMethodInfo CreatePathsMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [("root", typeof(string))]);

        return new SchemaMethodInfo(PathsTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateBytesMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("patternJson", typeof(string))
            ]);

        return new SchemaMethodInfo(BytesTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateAuditMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("root", typeof(string)),
                ("literal", typeof(string))
            ]);

        return new SchemaMethodInfo(AuditTable, constructorInfo);
    }

    private static SearchManySource CreateManySource(
        object[] arguments,
        SourceExecutionContext executionContext)
    {
        var (root, requestJson) = RequireManyArguments(arguments);
        return new SearchManySource(root, requestJson, executionContext);
    }

    private static SearchBytesSource CreateBytesSource(
        object[] arguments,
        SourceExecutionContext executionContext)
    {
        var (root, patternJson) = RequireBytesArguments(arguments);
        return new SearchBytesSource(root, patternJson, executionContext);
    }

    private static (string Root, string RequestJson) RequireManyArguments(object[] arguments)
    {
        if (arguments is null)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("arguments"));

        if (arguments.Length != 2)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyArgumentCount(arguments.Length));

        if (arguments[0] is not string root)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("root"));

        if (arguments[1] is not string requestJson)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("request"));

        return (root, requestJson);
    }

    private static (string Root, string PatternJson) RequireBytesArguments(object[] arguments)
    {
        if (arguments is null)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("arguments"));

        if (arguments.Length != 2)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgumentCount(arguments.Length));

        if (arguments[0] is not string root)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("root"));

        if (arguments[1] is not string patternJson)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("patternJson"));

        return (root, patternJson);
    }

    private static string RequirePathArguments(object[] arguments)
    {
        if (arguments is null)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("arguments"));

        if (arguments.Length != 1)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgumentCount(arguments.Length));

        if (arguments[0] is not string root)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("root"));

        return root;
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new SearchLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
