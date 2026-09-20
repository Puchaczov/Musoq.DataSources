#nullable enable

using System.Collections.Generic;

using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Sources;

/// <summary>Typed structural-input adapter for the matches source.</summary>
public sealed class SearchMatchesTypedSource : RowSource<SearchMatch>
{
    private readonly SearchMatchesSource _inner;

    /// <summary>Creates the default two-argument matches source.</summary>
    public SearchMatchesTypedSource(string root, string pattern, SourceExecutionContext context)
        : this(root, pattern, options: null, context)
    {
    }

    /// <summary>Creates matches with explicit typed options.</summary>
    public SearchMatchesTypedSource(
        string root,
        string pattern,
        SearchMatchOptionsInput options,
        SourceExecutionContext context)
        : this(root, pattern, (SearchMatchOptionsInput?)options, context)
    {
    }

    private SearchMatchesTypedSource(
        string root,
        string pattern,
        SearchMatchOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchMatchesSource(
            SearchTypedInputNormalizer.CreateSingle(
                root,
                pattern,
                options?.Text,
                options?.Records,
                options?.Context,
                options?.Scope,
                options?.Limits),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchMatch>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the lines source.</summary>
public sealed class SearchLinesTypedSource : RowSource<SearchLine>
{
    private readonly SearchLinesSource _inner;

    /// <summary>Creates the default two-argument lines source.</summary>
    public SearchLinesTypedSource(string root, string pattern, SourceExecutionContext context)
        : this(root, pattern, options: null, context)
    {
    }

    /// <summary>Creates lines with explicit typed options.</summary>
    public SearchLinesTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput options,
        SourceExecutionContext context)
        : this(root, pattern, (SearchScanOptionsInput?)options, context)
    {
    }

    private SearchLinesTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchLinesSource(
            SearchTypedInputNormalizer.CreateSingle(
                root,
                pattern,
                options?.Text,
                options?.Records,
                context: null,
                scope: options?.Scope,
                limits: options?.Limits),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchLine>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the files source.</summary>
public sealed class SearchFilesTypedSource : RowSource<SearchFile>
{
    private readonly SearchFilesSource _inner;

    /// <summary>Creates the default two-argument files source.</summary>
    public SearchFilesTypedSource(string root, string pattern, SourceExecutionContext context)
        : this(root, pattern, options: null, context)
    {
    }

    /// <summary>Creates files with explicit typed options.</summary>
    public SearchFilesTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput options,
        SourceExecutionContext context)
        : this(root, pattern, (SearchScanOptionsInput?)options, context)
    {
    }

    private SearchFilesTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchFilesSource(
            SearchTypedInputNormalizer.CreateSingle(
                root,
                pattern,
                options?.Text,
                options?.Records,
                context: null,
                scope: options?.Scope,
                limits: options?.Limits),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchFile>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the counts source.</summary>
public sealed class SearchCountsTypedSource : RowSource<SearchCount>
{
    private readonly SearchCountsSource _inner;

    /// <summary>Creates the default two-argument counts source.</summary>
    public SearchCountsTypedSource(string root, string pattern, SourceExecutionContext context)
        : this(root, pattern, options: null, context)
    {
    }

    /// <summary>Creates counts with explicit typed options.</summary>
    public SearchCountsTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput options,
        SourceExecutionContext context)
        : this(root, pattern, (SearchScanOptionsInput?)options, context)
    {
    }

    private SearchCountsTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchCountsSource(
            SearchTypedInputNormalizer.CreateSingle(
                root,
                pattern,
                options?.Text,
                options?.Records,
                context: null,
                scope: options?.Scope,
                limits: options?.Limits),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchCount>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the audit source.</summary>
public sealed class SearchAuditTypedSource : RowSource<SearchAudit>
{
    private readonly SearchAuditSource _inner;

    /// <summary>Creates the default two-argument audit source.</summary>
    public SearchAuditTypedSource(string root, string pattern, SourceExecutionContext context)
        : this(root, pattern, options: null, context)
    {
    }

    /// <summary>Creates audit with explicit typed options.</summary>
    public SearchAuditTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput options,
        SourceExecutionContext context)
        : this(root, pattern, (SearchScanOptionsInput?)options, context)
    {
    }

    private SearchAuditTypedSource(
        string root,
        string pattern,
        SearchScanOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchAuditSource(
            SearchTypedInputNormalizer.CreateSingle(
                root,
                pattern,
                options?.Text,
                options?.Records,
                context: null,
                scope: options?.Scope,
                limits: options?.Limits),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchAudit>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the labeled many source.</summary>
public sealed class SearchManyTypedSource : RowSource<SearchMatch>
{
    private readonly SearchManySource _inner;

    /// <summary>Creates typed many patterns with default options.</summary>
    public SearchManyTypedSource(
        string root,
        IReadOnlyList<SearchPatternInput> patterns,
        SourceExecutionContext context)
        : this(root, patterns, options: null, context)
    {
    }

    /// <summary>Creates typed many patterns with explicit options.</summary>
    public SearchManyTypedSource(
        string root,
        IReadOnlyList<SearchPatternInput> patterns,
        SearchManyOptionsInput options,
        SourceExecutionContext context)
        : this(root, patterns, (SearchManyOptionsInput?)options, context)
    {
    }

    private SearchManyTypedSource(
        string root,
        IReadOnlyList<SearchPatternInput> patterns,
        SearchManyOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchManySource(
            root,
            SearchTypedInputNormalizer.CreateMany(root, patterns, options),
            context);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchMatch>> Chunks => _inner.Chunks;
}

/// <summary>Typed structural-input adapter for the raw-byte source.</summary>
public sealed class SearchBytesTypedSource : RowSource<SearchByteMatch>
{
    private readonly SearchBytesSource _inner;

    /// <summary>Creates a raw-byte source from typed hexadecimal text.</summary>
    public SearchBytesTypedSource(
        string root,
        string patternHex,
        SourceExecutionContext context)
        : this(root, patternHex, options: null, context)
    {
    }

    /// <summary>Creates a raw-byte source with typed mask, window, scope, and limits.</summary>
    public SearchBytesTypedSource(
        string root,
        string patternHex,
        SearchBytesOptionsInput options,
        SourceExecutionContext context)
        : this(root, patternHex, (SearchBytesOptionsInput?)options, context)
    {
    }

    private SearchBytesTypedSource(
        string root,
        string patternHex,
        SearchBytesOptionsInput? options,
        SourceExecutionContext context)
    {
        _inner = new SearchBytesSource(
            root,
            SearchTypedInputNormalizer.CreateBytes(patternHex, options),
            context,
            SearchTypedInputNormalizer.CreateScope(options?.Scope),
            SearchTypedInputNormalizer.CreateLimits(options?.Limits));
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchByteMatch>> Chunks => _inner.Chunks;

}

/// <summary>Typed structural-input adapter for eligible path enumeration.</summary>
public sealed class SearchPathsTypedSource : RowSource<SearchPath>
{
    private readonly SearchPathsSource _inner;

    /// <summary>Creates the default eligible-path source.</summary>
    public SearchPathsTypedSource(string root, SourceExecutionContext context)
        : this(root, options: null, context)
    {
    }

    /// <summary>Creates eligible paths with explicit scope and file limits.</summary>
    public SearchPathsTypedSource(
        string root,
        SearchPathsOptionsInput options,
        SourceExecutionContext context)
        : this(root, (SearchPathsOptionsInput?)options, context)
    {
    }

    private SearchPathsTypedSource(
        string root,
        SearchPathsOptionsInput? options,
        SourceExecutionContext context)
    {
        var effectiveOptions = options ?? new SearchPathsOptionsInput(
            scope: null,
            maxFiles: long.MaxValue);
        if (effectiveOptions.MaxFiles < 0)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("options.maxFiles"));

        _inner = new SearchPathsSource(
            root,
            context,
            SearchTypedInputNormalizer.CreateScope(effectiveOptions.Scope),
            effectiveOptions.MaxFiles,
            counters: null);
    }

    /// <inheritdoc />
    public override IEnumerable<IReadOnlyList<SearchPath>> Chunks => _inner.Chunks;
}
