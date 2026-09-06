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

internal sealed class SearchCountsSource : SearchTextSourceBase<SearchCount>
{
    public SearchCountsSource(
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

    internal SearchCountsSource(
        SearchRequest request,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? scopeCounters = null)
        : base(request, executionContext, readerFactory, scopeCounters)
    {
    }

    protected override SearchTextRowSink<SearchCount> CreateSink(
        string relativePath,
        string literal,
        IChunkWriter<SearchCount> writer,
        SearchProjectionRequirements projection,
        SearchContextOptions context,
        SearchEvidenceHandle? evidence,
        Func<SearchCount, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
    {
        return new SearchCountSink(
            relativePath,
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget);
    }

    protected override object? GetPredicateValue(SearchCount row, string columnName)
    {
        return SearchPredicateValues.GetCountValue(row, columnName);
    }
}
