#nullable enable

using System;
using System.IO;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Sources;

internal sealed class SearchMatchesSource : SearchTextSourceBase<SearchMatch>
{
    public SearchMatchesSource(
        string root,
        string literal,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null)
        : base(
            SearchRequest.Create(root, literal),
            executionContext,
            readerFactory)
    {
    }

    internal SearchMatchesSource(
        SearchRequest request,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? scopeCounters = null)
        : base(request, executionContext, readerFactory, scopeCounters)
    {
    }

    protected override SearchTextRowSink<SearchMatch> CreateSink(
        string relativePath,
        string literal,
        IChunkWriter<SearchMatch> writer,
        SearchProjectionRequirements projection,
        SearchContextOptions context,
        SearchEvidenceHandle? evidence,
        Func<SearchMatch, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
    {
        return new SearchOccurrenceSink(
            relativePath,
            literal,
            projection.RetainMatchText,
            projection.RetainCaptures,
            projection.RetainContext,
            context,
            evidence,
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget);
    }

    protected override object? GetPredicateValue(SearchMatch row, string columnName)
    {
        return SearchPredicateValues.GetMatchValue(row, columnName);
    }
}
