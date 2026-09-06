#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Contracts;

internal sealed record SearchRequest
{
    private SearchRequest(
        string root,
        string literal,
        SearchPatternMode patternMode,
        ScopePolicy scope,
        SearchEncodingMode encodingMode,
        SearchCaseMode caseMode,
        bool wholeWord,
        SearchRecordMode recordMode,
        int maxRecordBytes,
        SearchContextOptions context,
        SearchRecordFraming? recordFraming,
        SearchResourceLimits limits,
        SearchPartialPolicy partialPolicy)
    {
        Root = root;
        Literal = literal;
        PatternMode = patternMode;
        Scope = scope;
        EncodingMode = encodingMode;
        CaseMode = caseMode;
        WholeWord = wholeWord;
        RecordMode = recordMode;
        MaxRecordBytes = maxRecordBytes;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        RecordFraming = recordFraming;
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        PartialPolicy = partialPolicy;
    }

    public string Root { get; }

    public string Literal { get; }

    public SearchPatternMode PatternMode { get; }

    public ScopePolicy Scope { get; }

    public SearchEncodingMode EncodingMode { get; }

    public SearchCaseMode CaseMode { get; }

    public bool WholeWord { get; }

    public SearchRecordMode RecordMode { get; }

    public int MaxRecordBytes { get; }

    public SearchContextOptions Context { get; }

    public SearchResourceLimits Limits { get; }

    internal SearchPartialPolicy PartialPolicy { get; }

    internal SearchRecordFraming? RecordFraming { get; }

    public static SearchRequest Create(
        string? root,
        string? literal,
        ScopePolicy? scope = null,
        string? encoding = null,
        string? caseMode = null,
        bool wholeWord = false,
        SearchContextOptions? context = null,
        SearchResourceLimits? limits = null,
        SearchPartialPolicy partialPolicy = SearchPartialPolicy.Reject)
    {
        var effectiveLimits = limits ?? SearchResourceLimits.Default;
        return CreateCore(
            root,
            literal,
            SearchPatternMode.Literal,
            scope,
            encoding,
            caseMode,
            wholeWord,
            SearchRecordMode.PhysicalLine,
            checked((int)Math.Min(
                SearchRegexScanner.MaxMultilineRecordBytes,
                effectiveLimits.MaxRecordBytes)),
            context ?? SearchContextOptions.Disabled,
            recordFraming: null,
            effectiveLimits,
            partialPolicy);
    }

    internal static SearchRequest CreateRegex(
        string? root,
        string? pattern,
        ScopePolicy? scope = null,
        string? encoding = null,
        string? caseMode = null,
        bool wholeWord = false,
        SearchRecordMode recordMode = SearchRecordMode.PhysicalLine,
        int maxRecordBytes = SearchRegexScanner.MaxMultilineRecordBytes,
        SearchContextOptions? context = null,
        SearchRecordFraming? recordFraming = null,
        SearchResourceLimits? limits = null,
        SearchPartialPolicy partialPolicy = SearchPartialPolicy.Reject)
    {
        var effectiveLimits = limits ?? SearchResourceLimits.Default;
        var effectiveRecordBytes = maxRecordBytes ==
                                   SearchRegexScanner.MaxMultilineRecordBytes
            ? checked((int)Math.Min(
                maxRecordBytes,
                effectiveLimits.MaxRecordBytes))
            : maxRecordBytes;
        var request = CreateCore(
            root,
            pattern,
            SearchPatternMode.Regex,
            scope,
            encoding,
            caseMode,
            wholeWord,
            recordMode,
            effectiveRecordBytes,
            context ?? SearchContextOptions.Disabled,
            recordFraming,
            effectiveLimits,
            partialPolicy);
        SearchRegexBackend.Validate(
            request.Literal,
            request.CaseMode,
            request.WholeWord,
            request.Limits.MaxPatternLength,
            request.Limits.MaxPatternCompilationMilliseconds);
        return request;
    }

    private static SearchRequest CreateCore(
        string? root,
        string? literal,
        SearchPatternMode patternMode,
        ScopePolicy? scope,
        string? encoding,
        string? caseMode,
        bool wholeWord,
        SearchRecordMode recordMode,
        int maxRecordBytes,
        SearchContextOptions context,
        SearchRecordFraming? recordFraming,
        SearchResourceLimits limits,
        SearchPartialPolicy partialPolicy)
    {
        if (root is null)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("root"));
        if (root.Length == 0)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("root"));

        if (literal is null)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("literal"));
        if (literal.Length == 0)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("literal"));

        if (recordMode is not SearchRecordMode.PhysicalLine and not SearchRecordMode.BoundedMultiline)
        {
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("recordMode"));
        }

        if (recordFraming is not null && recordMode != SearchRecordMode.BoundedMultiline)
        {
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("recordFraming"));
        }

        if (maxRecordBytes <= 0 || maxRecordBytes > SearchRegexScanner.MaxMultilineRecordBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "recordBytes",
                    SearchRegexScanner.MaxMultilineRecordBytes));
        }

        limits.ValidatePattern(literal, patternCount: 1);
        if (maxRecordBytes > limits.MaxRecordBytes)
        {
            throw limits.Exhausted(
                "record-bytes",
                limits.MaxRecordBytes,
                "record-bytes");
        }

        if (context.IsEnabled && context.MaxBytes > limits.MaxContextBytes)
        {
            throw limits.Exhausted(
                "context-bytes",
                limits.MaxContextBytes,
                "context-bytes");
        }

        return new SearchRequest(
            root,
            literal,
            patternMode,
            scope ?? ScopePolicy.Default,
            SearchEncodingPolicy.ParseOptional(encoding),
            SearchCasePolicy.ParseOptional(caseMode),
            wholeWord,
            recordMode,
            maxRecordBytes,
            context,
            recordFraming,
            limits,
            partialPolicy);
    }

    public static SearchRequest FromSourceArguments(object?[]? arguments)
    {
        if (arguments is null)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("arguments"));

        if (arguments.Length != 2)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgumentCount(arguments.Length));

        return Create(
            RequireString(arguments[0], "root"),
            RequireString(arguments[1], "literal"));
    }

    private static string RequireString(object? value, string argumentName)
    {
        return value as string ??
               throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument(argumentName));
    }
}

internal enum SearchPatternMode
{
    Literal,
    Regex
}

internal enum SearchRecordMode
{
    PhysicalLine,
    BoundedMultiline
}

internal sealed record ScopePolicy
{
    public static ScopePolicy Default { get; } = new();

    public ScopePolicy(
        bool recursive = true,
        IEnumerable<string>? include = null,
        IEnumerable<string>? exclude = null,
        RepositoryIgnorePolicy repositoryIgnores = RepositoryIgnorePolicy.Respect,
        GlobalIgnorePolicy globalIgnores = GlobalIgnorePolicy.Disabled,
        HiddenEntryPolicy hiddenEntries = HiddenEntryPolicy.Exclude,
        LinkTraversalPolicy followLinks = LinkTraversalPolicy.DoNotFollow,
        InaccessibleEntryPolicy inaccessibleEntries = InaccessibleEntryPolicy.Fail,
        IEnumerable<string>? globalIgnoreRules = null,
        SearchMetadataPolicy? metadata = null)
    {
        Recursive = recursive;
        Include = Freeze(include, nameof(include));
        Exclude = Freeze(exclude, nameof(exclude));
        GlobalIgnoreRules = Freeze(globalIgnoreRules, nameof(globalIgnoreRules));
        Metadata = metadata ?? SearchMetadataPolicy.Default;
        RepositoryIgnores = repositoryIgnores;
        GlobalIgnores = globalIgnores;
        HiddenEntries = hiddenEntries;
        FollowLinks = followLinks;
        InaccessibleEntries = inaccessibleEntries;
    }

    public bool Recursive { get; }

    public IReadOnlyList<string> Include { get; }

    public IReadOnlyList<string> Exclude { get; }

    public IReadOnlyList<string> GlobalIgnoreRules { get; }

    public SearchMetadataPolicy Metadata { get; }

    public RepositoryIgnorePolicy RepositoryIgnores { get; }

    public GlobalIgnorePolicy GlobalIgnores { get; }

    public HiddenEntryPolicy HiddenEntries { get; }

    public LinkTraversalPolicy FollowLinks { get; }

    public InaccessibleEntryPolicy InaccessibleEntries { get; }

    private static IReadOnlyList<string> Freeze(
        IEnumerable<string>? values,
        string parameterName)
    {
        if (values is null)
            return Array.Empty<string>();

        var copy = values.ToArray();
        if (copy.Length > 128)
            throw new SearchResourceLimitException(SearchDiagnosticCatalog.ResourceLimit(parameterName, 128));

        for (var index = 0; index < copy.Length; index++)
        {
            if (copy[index] is null)
                throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument(parameterName));

            if (copy[index].Length > 4096)
                throw new SearchResourceLimitException(SearchDiagnosticCatalog.ResourceLimit(parameterName, 4096));
        }

        return Array.AsReadOnly(copy);
    }
}

internal sealed record SearchMetadataPolicy
{
    public static SearchMetadataPolicy Default { get; } = new();

    public SearchMetadataPolicy(
        IEnumerable<string>? nameIncludes = null,
        IEnumerable<string>? nameExcludes = null,
        IEnumerable<string>? extensionIncludes = null,
        IEnumerable<string>? extensionExcludes = null,
        long? minimumSizeBytes = null,
        long? maximumSizeBytes = null,
        DateTimeOffset? modifiedAfterOrEqualUtc = null,
        DateTimeOffset? modifiedBeforeOrEqualUtc = null)
    {
        NameIncludes = Freeze(nameIncludes, nameof(nameIncludes));
        NameExcludes = Freeze(nameExcludes, nameof(nameExcludes));
        ExtensionIncludes = FreezeExtensions(extensionIncludes, nameof(extensionIncludes));
        ExtensionExcludes = FreezeExtensions(extensionExcludes, nameof(extensionExcludes));

        if (minimumSizeBytes is < 0 || maximumSizeBytes is < 0 ||
            minimumSizeBytes > maximumSizeBytes)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(nameof(minimumSizeBytes)));
        }

        MinimumSizeBytes = minimumSizeBytes;
        MaximumSizeBytes = maximumSizeBytes;
        ModifiedAfterOrEqualUtc = modifiedAfterOrEqualUtc?.ToUniversalTime();
        ModifiedBeforeOrEqualUtc = modifiedBeforeOrEqualUtc?.ToUniversalTime();

        if (ModifiedAfterOrEqualUtc > ModifiedBeforeOrEqualUtc)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(nameof(modifiedAfterOrEqualUtc)));
        }
    }

    public IReadOnlyList<string> NameIncludes { get; }

    public IReadOnlyList<string> NameExcludes { get; }

    public IReadOnlyList<string> ExtensionIncludes { get; }

    public IReadOnlyList<string> ExtensionExcludes { get; }

    public long? MinimumSizeBytes { get; }

    public long? MaximumSizeBytes { get; }

    public DateTimeOffset? ModifiedAfterOrEqualUtc { get; }

    public DateTimeOffset? ModifiedBeforeOrEqualUtc { get; }

    private static IReadOnlyList<string> Freeze(
        IEnumerable<string>? values,
        string parameterName)
    {
        if (values is null)
            return Array.Empty<string>();

        var copy = values.ToArray();
        if (copy.Length > 128)
            throw new SearchResourceLimitException(SearchDiagnosticCatalog.ResourceLimit(parameterName, 128));

        for (var index = 0; index < copy.Length; index++)
        {
            if (copy[index] is null || copy[index].Length == 0)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument(parameterName));
            }

            if (copy[index].Length > 4096)
                throw new SearchResourceLimitException(SearchDiagnosticCatalog.ResourceLimit(parameterName, 4096));
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<string> FreezeExtensions(
        IEnumerable<string>? values,
        string parameterName)
    {
        var frozen = Freeze(values, parameterName);
        var normalized = new string[frozen.Count];
        for (var index = 0; index < frozen.Count; index++)
        {
            var extension = frozen[index];
            normalized[index] = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : $".{extension}";
        }

        return Array.AsReadOnly(normalized);
    }
}

internal enum RepositoryIgnorePolicy
{
    Respect,
    Disabled
}

internal enum GlobalIgnorePolicy
{
    Disabled,
    Configured
}

internal enum HiddenEntryPolicy
{
    Exclude,
    Include
}

internal enum LinkTraversalPolicy
{
    DoNotFollow,
    Follow
}

internal enum InaccessibleEntryPolicy
{
    Fail,
    Skip
}

internal readonly record struct MatchSpan(
    long Start,
    long Length,
    long LineNumber,
    long Utf16Column,
    long? ByteOffset,
    long? ByteLength)
{
    public string? PatternId { get; init; }

    public string? MatchText { get; init; }

    public IReadOnlyList<SearchCapture>? Captures { get; init; }

    public MatchSpan(
        long start,
        long length,
        long lineNumber,
        long utf16Column)
        : this(start, length, lineNumber, utf16Column, null, null)
    {
    }

    public long EndExclusive => checked(Start + Length);
}

/// <summary>
///     Describes the terminal state of one Search execution.
/// </summary>
public enum SearchOutcome
{
    /// <summary>The requested positive result was satisfied before the scope was exhausted.</summary>
    QuerySatisfied,

    /// <summary>The resolved eligible scope was fully processed.</summary>
    ScopeExhausted,

    /// <summary>The execution failed before a complete answer was available.</summary>
    Failed,

    /// <summary>An explicitly permitted observed prefix was retained after an incomplete execution.</summary>
    Partial
}
