#nullable enable

using System.Collections.Generic;

namespace Musoq.DataSources.Search;

/// <summary>One labeled pattern supplied to the typed many source.</summary>
public readonly struct SearchPatternInput
{
    /// <summary>Creates one labeled search pattern.</summary>
    public SearchPatternInput(string id, string pattern, string mode = "literal")
    {
        Id = id;
        Pattern = pattern;
        Mode = mode;
    }

    /// <summary>Gets the stable ASCII label used in <c>PatternId</c>.</summary>
    public string Id { get; }

    /// <summary>Gets the literal or regular-expression text.</summary>
    public string Pattern { get; }

    /// <summary>Gets <c>literal</c> or <c>regex</c>; the default is <c>literal</c>.</summary>
    public string Mode { get; }
}

/// <summary>
///     Text options for single-pattern sources. Omitted values mean literal,
///     sensitive, automatic encoding, and unrestricted word matching.
/// </summary>
public readonly struct SearchTextInput
{
    /// <summary>Creates single-pattern text options.</summary>
    public SearchTextInput(
        string mode = "literal",
        string caseMode = "sensitive",
        string encoding = "auto",
        bool wholeWord = false)
    {
        Mode = mode;
        CaseMode = caseMode;
        Encoding = encoding;
        WholeWord = wholeWord;
    }

    /// <summary>Gets <c>literal</c> or <c>regex</c>.</summary>
    public string Mode { get; }

    /// <summary>
    ///     Gets <c>sensitive</c> or explicit <c>insensitive</c> matching.
    ///     The SQL field is named <c>CaseMode</c> because the current Core
    ///     grammar reserves the unbracketed word <c>Case</c> for expressions.
    /// </summary>
    public string CaseMode { get; }

    /// <summary>Gets the decoding policy, normally <c>auto</c>.</summary>
    public string Encoding { get; }

    /// <summary>Gets whether matches must have word boundaries.</summary>
    public bool WholeWord { get; }
}

/// <summary>
///     Text options for the typed many source. Pattern mode belongs to each
///     <see cref="SearchPatternInput"/>; this group has no overall mode.
/// </summary>
public readonly struct SearchManyTextInput
{
    /// <summary>Creates many-pattern text options.</summary>
    public SearchManyTextInput(
        string caseMode = "sensitive",
        string encoding = "auto",
        bool wholeWord = false)
    {
        CaseMode = caseMode;
        Encoding = encoding;
        WholeWord = wholeWord;
    }

    /// <summary>
    ///     Gets <c>sensitive</c> or explicit <c>insensitive</c> matching.
    ///     The SQL field is named <c>CaseMode</c> because the current Core
    ///     grammar reserves the unbracketed word <c>Case</c> for expressions.
    /// </summary>
    public string CaseMode { get; }

    /// <summary>Gets the decoding policy, normally <c>auto</c>.</summary>
    public string Encoding { get; }

    /// <summary>Gets whether every pattern must have word boundaries.</summary>
    public bool WholeWord { get; }
}

/// <summary>Physical-line or bounded-multiline record options for regex scans.</summary>
public readonly struct SearchRecordsInput
{
    /// <summary>Creates record framing options.</summary>
    public SearchRecordsInput(
        string mode = "physical-line",
        long maxBytes = 1_048_576,
        string? startDelimiter = null,
        string? endDelimiter = null,
        bool allowUnterminatedEof = false)
    {
        Mode = mode;
        MaxBytes = maxBytes;
        StartDelimiter = startDelimiter;
        EndDelimiter = endDelimiter;
        AllowUnterminatedEof = allowUnterminatedEof;
    }

    /// <summary>Gets <c>physical-line</c> or <c>bounded-multiline</c>.</summary>
    public string Mode { get; }

    /// <summary>Gets the maximum decoded record size, up to 1 MiB.</summary>
    public long MaxBytes { get; }

    /// <summary>Gets the bounded-multiline start delimiter.</summary>
    public string? StartDelimiter { get; }

    /// <summary>Gets the bounded-multiline end delimiter.</summary>
    public string? EndDelimiter { get; }

    /// <summary>Gets whether an unterminated final bounded record is accepted.</summary>
    public bool AllowUnterminatedEof { get; }
}

/// <summary>Bounded context lines retained around a text match.</summary>
public readonly struct SearchContextInput
{
    /// <summary>Creates context options.</summary>
    public SearchContextInput(int beforeLines = 0, int afterLines = 0, int maxBytes = 65_536)
    {
        BeforeLines = beforeLines;
        AfterLines = afterLines;
        MaxBytes = maxBytes;
    }

    /// <summary>Gets the number of preceding physical lines.</summary>
    public int BeforeLines { get; }

    /// <summary>Gets the number of following physical lines.</summary>
    public int AfterLines { get; }

    /// <summary>Gets the total context byte bound.</summary>
    public int MaxBytes { get; }
}

/// <summary>Optional file metadata predicates used during scope traversal.</summary>
public readonly struct SearchMetadataInput
{
    /// <summary>Creates metadata predicates.</summary>
    public SearchMetadataInput(
        IReadOnlyList<string>? nameIncludes = null,
        IReadOnlyList<string>? nameExcludes = null,
        IReadOnlyList<string>? extensionIncludes = null,
        IReadOnlyList<string>? extensionExcludes = null,
        long? minimumSizeBytes = null,
        long? maximumSizeBytes = null,
        string? modifiedAfterOrEqualUtc = null,
        string? modifiedBeforeOrEqualUtc = null)
    {
        NameIncludes = nameIncludes;
        NameExcludes = nameExcludes;
        ExtensionIncludes = extensionIncludes;
        ExtensionExcludes = extensionExcludes;
        MinimumSizeBytes = minimumSizeBytes;
        MaximumSizeBytes = maximumSizeBytes;
        ModifiedAfterOrEqualUtc = modifiedAfterOrEqualUtc;
        ModifiedBeforeOrEqualUtc = modifiedBeforeOrEqualUtc;
    }

    /// <summary>Gets root-relative filename include globs.</summary>
    public IReadOnlyList<string>? NameIncludes { get; }

    /// <summary>Gets root-relative filename exclude globs.</summary>
    public IReadOnlyList<string>? NameExcludes { get; }

    /// <summary>Gets extension include values.</summary>
    public IReadOnlyList<string>? ExtensionIncludes { get; }

    /// <summary>Gets extension exclude values.</summary>
    public IReadOnlyList<string>? ExtensionExcludes { get; }

    /// <summary>Gets the inclusive minimum file size.</summary>
    public long? MinimumSizeBytes { get; }

    /// <summary>Gets the inclusive maximum file size.</summary>
    public long? MaximumSizeBytes { get; }

    /// <summary>Gets the inclusive UTC modification lower bound.</summary>
    public string? ModifiedAfterOrEqualUtc { get; }

    /// <summary>Gets the inclusive UTC modification upper bound.</summary>
    public string? ModifiedBeforeOrEqualUtc { get; }
}

/// <summary>
///     Directory and file scope policy. A directory root is recursive by
///     default; hidden entries and links remain excluded unless enabled.
/// </summary>
public readonly struct SearchScopeInput
{
    /// <summary>Creates scope policy options.</summary>
    public SearchScopeInput(
        bool recursive = true,
        IReadOnlyList<string>? include = null,
        IReadOnlyList<string>? exclude = null,
        string repositoryIgnores = "respect",
        string globalIgnores = "disabled",
        bool hiddenEntries = false,
        bool followLinks = false,
        string? inaccessibleEntries = null,
        IReadOnlyList<string>? globalIgnoreRules = null,
        SearchMetadataInput? metadata = null)
    {
        Recursive = recursive;
        Include = include;
        Exclude = exclude;
        RepositoryIgnores = repositoryIgnores;
        GlobalIgnores = globalIgnores;
        HiddenEntries = hiddenEntries;
        FollowLinks = followLinks;
        InaccessibleEntries = inaccessibleEntries;
        GlobalIgnoreRules = globalIgnoreRules;
        Metadata = metadata;
    }

    /// <summary>Gets whether a directory root descends into subdirectories.</summary>
    public bool Recursive { get; }

    /// <summary>Gets root-relative path globs eligible for inclusion.</summary>
    public IReadOnlyList<string>? Include { get; }

    /// <summary>Gets root-relative path globs excluded after inclusion.</summary>
    public IReadOnlyList<string>? Exclude { get; }

    /// <summary>Gets <c>respect</c> or <c>disabled</c> repository ignore behavior.</summary>
    public string RepositoryIgnores { get; }

    /// <summary>Gets <c>disabled</c> or <c>configured</c> global ignore behavior.</summary>
    public string GlobalIgnores { get; }

    /// <summary>Gets whether hidden files and directories are eligible.</summary>
    public bool HiddenEntries { get; }

    /// <summary>Gets whether symbolic links and reparse points may be followed.</summary>
    public bool FollowLinks { get; }

    /// <summary>Gets the inaccessible-entry policy; omitted means fail.</summary>
    public string? InaccessibleEntries { get; }

    /// <summary>Gets configured global ignore globs.</summary>
    public IReadOnlyList<string>? GlobalIgnoreRules { get; }

    /// <summary>Gets optional metadata predicates.</summary>
    public SearchMetadataInput? Metadata { get; }
}

/// <summary>Per-request work and input resource limits.</summary>
public readonly struct SearchLimitsInput
{
    /// <summary>Creates resource limits. Omitted work limits are unlimited.</summary>
    public SearchLimitsInput(
        long maxTotalBytes = long.MaxValue,
        long maxFileBytes = long.MaxValue,
        long maxFiles = long.MaxValue,
        int maxPatternCount = 1_024,
        int maxPatternLength = 65_536,
        long maxPatternBytes = 8L * 1024 * 1024,
        int maxPatternCompilationMilliseconds = 5_000,
        long maxRecordBytes = long.MaxValue,
        long maxContextBytes = 1_048_576,
        long maxInFlightOutputBytes = long.MaxValue,
        long maxMatchCount = long.MaxValue)
    {
        MaxTotalBytes = maxTotalBytes;
        MaxFileBytes = maxFileBytes;
        MaxFiles = maxFiles;
        MaxPatternCount = maxPatternCount;
        MaxPatternLength = maxPatternLength;
        MaxPatternBytes = maxPatternBytes;
        MaxPatternCompilationMilliseconds = maxPatternCompilationMilliseconds;
        MaxRecordBytes = maxRecordBytes;
        MaxContextBytes = maxContextBytes;
        MaxInFlightOutputBytes = maxInFlightOutputBytes;
        MaxMatchCount = maxMatchCount;
    }

    /// <summary>Gets the total input-byte limit.</summary>
    public long MaxTotalBytes { get; }

    /// <summary>Gets the per-file input-byte limit.</summary>
    public long MaxFileBytes { get; }

    /// <summary>Gets the eligible-file limit.</summary>
    public long MaxFiles { get; }

    /// <summary>Gets the maximum pattern count.</summary>
    public int MaxPatternCount { get; }

    /// <summary>Gets the maximum individual pattern length in UTF-16 units.</summary>
    public int MaxPatternLength { get; }

    /// <summary>Gets the aggregate pattern-byte limit in UTF-8.</summary>
    public long MaxPatternBytes { get; }

    /// <summary>Gets the regex compilation time limit in milliseconds.</summary>
    public int MaxPatternCompilationMilliseconds { get; }

    /// <summary>Gets the explicit record-byte limit; omitted means literal streaming.</summary>
    public long MaxRecordBytes { get; }

    /// <summary>Gets the maximum context-byte limit.</summary>
    public long MaxContextBytes { get; }

    /// <summary>Gets the staged output-byte limit.</summary>
    public long MaxInFlightOutputBytes { get; }

    /// <summary>Gets the total observed-match limit.</summary>
    public long MaxMatchCount { get; }
}

/// <summary>Options for occurrence searches, including bounded context.</summary>
public readonly struct SearchMatchOptionsInput
{
    /// <summary>Creates occurrence-search options.</summary>
    public SearchMatchOptionsInput(
        SearchTextInput? text = null,
        SearchRecordsInput? records = null,
        SearchContextInput? context = null,
        SearchScopeInput? scope = null,
        SearchLimitsInput? limits = null)
    {
        Text = text;
        Records = records;
        Context = context;
        Scope = scope;
        Limits = limits;
    }

    /// <summary>Gets text matching options.</summary>
    public SearchTextInput? Text { get; }

    /// <summary>Gets regex record options.</summary>
    public SearchRecordsInput? Records { get; }

    /// <summary>Gets bounded context options.</summary>
    public SearchContextInput? Context { get; }

    /// <summary>Gets scope options.</summary>
    public SearchScopeInput? Scope { get; }

    /// <summary>Gets work and output limits.</summary>
    public SearchLimitsInput? Limits { get; }
}

/// <summary>Options for line, file, count, and audit scans.</summary>
public readonly struct SearchScanOptionsInput
{
    /// <summary>Creates scan options.</summary>
    public SearchScanOptionsInput(
        SearchTextInput? text = null,
        SearchRecordsInput? records = null,
        SearchScopeInput? scope = null,
        SearchLimitsInput? limits = null)
    {
        Text = text;
        Records = records;
        Scope = scope;
        Limits = limits;
    }

    /// <summary>Gets text matching options.</summary>
    public SearchTextInput? Text { get; }

    /// <summary>Gets regex record options.</summary>
    public SearchRecordsInput? Records { get; }

    /// <summary>Gets scope options.</summary>
    public SearchScopeInput? Scope { get; }

    /// <summary>Gets work and output limits.</summary>
    public SearchLimitsInput? Limits { get; }
}

/// <summary>Options for labeled multi-pattern searches.</summary>
public readonly struct SearchManyOptionsInput
{
    /// <summary>Creates many-pattern options.</summary>
    public SearchManyOptionsInput(
        SearchManyTextInput? text = null,
        SearchContextInput? context = null,
        SearchScopeInput? scope = null,
        SearchLimitsInput? limits = null)
    {
        Text = text;
        Context = context;
        Scope = scope;
        Limits = limits;
    }

    /// <summary>Gets shared case, encoding, and word-boundary options.</summary>
    public SearchManyTextInput? Text { get; }

    /// <summary>Gets bounded context options.</summary>
    public SearchContextInput? Context { get; }

    /// <summary>Gets scope options.</summary>
    public SearchScopeInput? Scope { get; }

    /// <summary>Gets work and output limits.</summary>
    public SearchLimitsInput? Limits { get; }
}

/// <summary>Options for eligible path enumeration.</summary>
public readonly struct SearchPathsOptionsInput
{
    /// <summary>Creates path-enumeration options.</summary>
    public SearchPathsOptionsInput(
        SearchScopeInput? scope = null,
        long maxFiles = long.MaxValue)
    {
        Scope = scope;
        MaxFiles = maxFiles;
    }

    /// <summary>Gets scope options.</summary>
    public SearchScopeInput? Scope { get; }

    /// <summary>Gets the maximum number of emitted paths.</summary>
    public long MaxFiles { get; }
}

/// <summary>One bounded raw-byte window around a byte match.</summary>
public readonly struct SearchBytesWindowInput
{
    /// <summary>Creates a byte-window specification.</summary>
    public SearchBytesWindowInput(long beforeBytes = 0, long afterBytes = 0)
    {
        BeforeBytes = beforeBytes;
        AfterBytes = afterBytes;
    }

    /// <summary>Gets the number of bytes before the match.</summary>
    public long BeforeBytes { get; }

    /// <summary>Gets the number of bytes after the match.</summary>
    public long AfterBytes { get; }
}

/// <summary>Options for raw-byte searches.</summary>
public readonly struct SearchBytesOptionsInput
{
    /// <summary>Creates raw-byte search options.</summary>
    public SearchBytesOptionsInput(
        string? maskHex = null,
        SearchBytesWindowInput? window = null,
        SearchScopeInput? scope = null,
        SearchLimitsInput? limits = null)
    {
        MaskHex = maskHex;
        Window = window;
        Scope = scope;
        Limits = limits;
    }

    /// <summary>Gets an optional hexadecimal wildcard mask.</summary>
    public string? MaskHex { get; }

    /// <summary>Gets the optional byte window.</summary>
    public SearchBytesWindowInput? Window { get; }

    /// <summary>Gets scope options.</summary>
    public SearchScopeInput? Scope { get; }

    /// <summary>Gets work and output limits.</summary>
    public SearchLimitsInput? Limits { get; }
}
