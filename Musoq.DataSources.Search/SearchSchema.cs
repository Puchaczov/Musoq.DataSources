#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
///     Provides deterministic literal, regular-expression and raw-byte search over files below a requested root.
/// </description>
/// <short-description>
///     Provides deterministic literal, regular-expression and raw-byte search over files below a requested root.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    /// <summary>Provides deterministic Search sources for literal or regular-expression matches, raw-byte occurrences and eligible paths.</summary>
    /// <remarks>
    ///     Typed structural constructors are the preferred SQL surface. Simple
    ///     two-argument calls retain their meaning: text is literal and
    ///     case-sensitive, directory scope is recursive, repository ignores
    ///     are respected, hidden entries are excluded, and links are not
    ///     followed. Use <c>CaseMode: 'insensitive'</c> explicitly. The SQL
    ///     word <c>Case</c> is reserved by the current Core grammar. An insensitive
    ///     literal match projects the spelling found in the file.
    ///
    ///     Use paths for an eligible-file manifest, files for one row per
    ///     matching file, lines for one row per matching physical line,
    ///     matches for one row per occurrence, many for labeled literal and
    ///     regex occurrences, counts for exact per-file totals including
    ///     zero-hit eligible text files, audit for one terminal count-scan
    ///     outcome, and bytes for raw data without text decoding.
    ///
    ///     Include and Exclude are root-relative path globs, not SQL LIKE
    ///     patterns. TAKE limits accepted output and does not promise a
    ///     deterministic path prefix when parallel workers are enabled; use
    ///     ORDER BY when result order matters. Omitted literal record limits
    ///     preserve streaming long-line behavior. Regex records, and an
    ///     explicitly supplied MaxRecordBytes, are bounded at 1 MiB.
    /// </remarks>
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
    ///         <virtual-param>Root file or directory; it is required and is never inferred from the working directory</virtual-param>
    ///         <virtual-param>Literal text, typed pattern collection, or hexadecimal byte pattern</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>search.matches(string root, string pattern[, SearchMatchOptionsInput options])</from>
    ///                 <description>Returns one typed row for every non-overlapping occurrence. Omitted options mean literal, sensitive, automatic encoding and recursive scope.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                     <column name="MatchIndex" type="long">Zero-based occurrence ordinal within the path</column>
    ///                     <column name="ByteOffset" type="long?">Original-byte start when the decoded span is losslessly mapped</column>
    ///                     <column name="ByteLength" type="long?">Original-byte length when the decoded span is losslessly mapped</column>
    ///                     <column name="LineNumber" type="long">One-based physical line containing the occurrence</column>
    ///                     <column name="Utf16Column" type="long">Zero-based UTF-16 column within the line</column>
    ///                     <column name="Utf16Length" type="long">Match length in UTF-16 code units</column>
    ///                     <column name="MatchText" type="string">Actual matched source spelling when retained by the projection</column>
    ///                     <column name="Captures" type="IReadOnlyList&lt;SearchCapture&gt;">Typed regular-expression captures; empty for literal matches</column>
    ///                     <column name="Context" type="IReadOnlyList&lt;SearchContextLine&gt;">Bounded adjacent physical lines; empty when context is disabled or not projected</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.many(string root, IReadOnlyList&lt;SearchPatternInput&gt; patterns[, SearchManyOptionsInput options])</from>
    ///                 <description>Returns one typed row for every non-overlapping occurrence of each labeled literal or regex pattern. The SQL collection form is array { (Id: 'todo', Pattern: 'TODO'), (Id: 'issue', Pattern: 'ISSUE-[0-9]+', Mode: 'regex') }.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="PatternId" type="string">The request label for the matched pattern</column>
    ///                     <column name="MatchIndex" type="long">Zero-based occurrence ordinal within the path and pattern label</column>
    ///                     <column name="ByteOffset" type="long?">Original-byte start when the decoded span is losslessly mapped</column>
    ///                     <column name="ByteLength" type="long?">Original-byte length when the decoded span is losslessly mapped</column>
    ///                     <column name="LineNumber" type="long">One-based physical line containing the occurrence</column>
    ///                     <column name="Utf16Column" type="long">Zero-based UTF-16 column within the line</column>
    ///                     <column name="Utf16Length" type="long">Match length in UTF-16 code units</column>
    ///                     <column name="MatchText" type="string">Matched source spelling for literals or matched value for regex</column>
    ///                     <column name="Captures" type="IReadOnlyList&lt;SearchCapture&gt;">Captures for regex rows; empty for literal rows</column>
    ///                     <column name="Context" type="IReadOnlyList&lt;SearchContextLine&gt;">Bounded adjacent physical lines when requested and projected; otherwise empty</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.bytes(string root, string patternHex[, SearchBytesOptionsInput options])</from>
    ///                 <description>Returns one typed row for every leftmost, non-overlapping raw-byte occurrence. Hex text uses two nibbles per byte; wildcard nibbles and an optional typed mask are supported.</description>
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
    ///                 <from>search.lines(string root, string pattern[, SearchScanOptionsInput options])</from>
    ///                 <description>Returns one typed row for every physical line containing one or more non-overlapping occurrences. Omitted options mean literal, sensitive, automatic encoding and recursive scope.</description>
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
    ///                 <from>search.files(string root, string pattern[, SearchScanOptionsInput options])</from>
    ///                 <description>Returns one typed row for every eligible file containing at least one occurrence. Omitted options mean literal, sensitive, automatic encoding and recursive scope.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="PatternId" type="string">Null for the single-pattern scaffold</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.counts(string root, string pattern[, SearchScanOptionsInput options])</from>
    ///                 <description>Returns one exact count row for every eligible file completed by the scan, including zero-hit files. Omitted options mean literal, sensitive, automatic encoding and recursive scope.</description>
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
    ///                 <from>search.paths(string root[, SearchPathsOptionsInput options])</from>
    ///                 <description>Returns one typed row for every eligible regular-file path without opening file content. Omitted options mean recursive scope, hidden exclusion and no path-count limit.</description>
    ///                 <columns>
    ///                     <column name="Path" type="string">Path relative to the requested root</column>
    ///                     <column name="Origin" type="string">Null for a local path row</column>
    ///                     <column name="EntryKind" type="string">The stable local entry kind, file</column>
    ///                 </columns>
    ///             </example>
    ///             <example>
    ///                 <from>search.audit(string root, string pattern[, SearchScanOptionsInput options])</from>
    ///                 <description>Executes a fresh count scan and returns one typed terminal summary row for that execution. Omitted options mean literal, sensitive, automatic encoding and recursive scope.</description>
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
        AddTable<SearchMatchesTable>(MatchesTable);
        AddTable<SearchLinesTable>(LinesTable);
        AddTable<SearchFilesTable>(FilesTable);
        AddTable<SearchCountsTable>(CountsTable);
        AddTable<SearchPathsTable>(PathsTable);
        AddTable<SearchBytesTable>(BytesTable);
        AddTable<SearchAuditsTable>(AuditTable);

        AddTypedSource<SearchMatchesTypedSource>(MatchesTable);
        AddTypedSource<SearchLinesTypedSource>(LinesTable);
        AddTypedSource<SearchFilesTypedSource>(FilesTable);
        AddTypedSource<SearchCountsTypedSource>(CountsTable);
        AddTypedSource<SearchPathsTypedSource>(PathsTable);
        AddTypedSource<SearchManyTypedSource>(ManyTable);
        AddTypedSource<SearchBytesTypedSource>(BytesTable);
        AddTypedSource<SearchAuditTypedSource>(AuditTable);
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
        params object?[] parameters)
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
        params object?[] parameters)
    {
        if (!IsKnownSource(name))
        {
            throw new SourceNotFoundException(
                $"Search source '{name}' was not found. Use one of: matches, lines, files, counts, audit, paths, many, bytes.");
        }

        try
        {
            return base.GetRowSource<T>(name, executionContext, parameters);
        }
        catch (MethodResolutionException exception)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("arguments"),
                exception);
        }
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
        params object?[] parameters)
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
    /// <returns>Execution-phase worker and buffered-output settings.</returns>
    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.MetadataContext.EndWorkToken.ThrowIfCancellationRequested();

        return
        [
            new SourceRuntimeSettingRequirement(
                "search.max_parallelism",
                Required: false,
                Secret: false,
                SourceRuntimeSettingPhase.Execution,
                "Optional worker count for Search file scans. Zero or omission uses twice the CPU count, clamped to 1 through 8; explicit values are 1 through 32."),
            new SourceRuntimeSettingRequirement(
                "search.buffered_output_bytes",
                Required: false,
                Secret: false,
                SourceRuntimeSettingPhase.Execution,
                "Optional per-query buffered output budget in bytes. The default is 33554432; valid values are 65536 through 536870912.")
        ];
    }

    /// <summary>
    ///     Plans the Search source's conservative scalar predicate pushdown.
    /// </summary>
    /// <param name="name">Data source name.</param>
    /// <param name="request">Source planning request.</param>
    /// <param name="parameters">Root and literal parameters.</param>
    /// <returns>Source plan with accepted scalar predicates and residual work.</returns>
    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
    {
        return SearchSourcePlanner.Plan(name, request);
    }

    /// <summary>
    ///     Gets constructor metadata for all Search data sources.
    /// </summary>
    /// <returns>Search data source constructors.</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return base.GetConstructors();
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
        ArgumentNullException.ThrowIfNull(metadataContext);
        metadataContext.EndWorkToken.ThrowIfCancellationRequested();
        return TypedConstructors()
            .Where(constructor => constructor.MethodName.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    ///     Gets raw constructor metadata for all Search data source methods.
    /// </summary>
    /// <param name="metadataContext">Metadata context.</param>
    /// <returns>Search data source constructors.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        ArgumentNullException.ThrowIfNull(metadataContext);
        metadataContext.EndWorkToken.ThrowIfCancellationRequested();
        return TypedConstructors();
    }

    private static SchemaMethodInfo[] TypedConstructors()
    {
        return
        [
            CreateMethod(MatchesTable, ("root", typeof(string)), ("pattern", typeof(string))),
            CreateMethod(MatchesTable, ("root", typeof(string)), ("pattern", typeof(string)), ("options", typeof(SearchMatchOptionsInput))),
            CreateMethod(ManyTable, ("root", typeof(string)), ("patterns", typeof(IReadOnlyList<SearchPatternInput>))),
            CreateMethod(ManyTable, ("root", typeof(string)), ("patterns", typeof(IReadOnlyList<SearchPatternInput>)), ("options", typeof(SearchManyOptionsInput))),
            CreateMethod(LinesTable, ("root", typeof(string)), ("pattern", typeof(string))),
            CreateMethod(LinesTable, ("root", typeof(string)), ("pattern", typeof(string)), ("options", typeof(SearchScanOptionsInput))),
            CreateMethod(FilesTable, ("root", typeof(string)), ("pattern", typeof(string))),
            CreateMethod(FilesTable, ("root", typeof(string)), ("pattern", typeof(string)), ("options", typeof(SearchScanOptionsInput))),
            CreateMethod(CountsTable, ("root", typeof(string)), ("pattern", typeof(string))),
            CreateMethod(CountsTable, ("root", typeof(string)), ("pattern", typeof(string)), ("options", typeof(SearchScanOptionsInput))),
            CreateMethod(PathsTable, ("root", typeof(string))),
            CreateMethod(PathsTable, ("root", typeof(string)), ("options", typeof(SearchPathsOptionsInput))),
            CreateMethod(BytesTable, ("root", typeof(string)), ("patternHex", typeof(string))),
            CreateMethod(BytesTable, ("root", typeof(string)), ("patternHex", typeof(string)), ("options", typeof(SearchBytesOptionsInput))),
            CreateMethod(AuditTable, ("root", typeof(string)), ("pattern", typeof(string))),
            CreateMethod(AuditTable, ("root", typeof(string)), ("pattern", typeof(string)), ("options", typeof(SearchScanOptionsInput)))
        ];
    }

    private static SchemaMethodInfo CreateMethod(
        string methodName,
        params (string Name, Type Type)[] arguments)
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            arguments);

        return new SchemaMethodInfo(methodName, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new SearchLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }

    private static bool IsKnownSource(string? name)
    {
        return name is not null &&
               (name.Equals(MatchesTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(ManyTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(LinesTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(FilesTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(CountsTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(PathsTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(BytesTable, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(AuditTable, StringComparison.OrdinalIgnoreCase));
    }
}
