#nullable enable

using System;
using System.Collections.Generic;

using Musoq.DataSources.Search.Components.Contracts;

namespace Musoq.DataSources.Search.Components.Traversal;

internal sealed class SearchScopeExplanation
{
    public SearchScopeExplanation(
        string requestedRoot,
        string resolvedRoot,
        string rootKind,
        ScopePolicy scope,
        SearchScopeCounters counters,
        long eligibleFiles)
    {
        RequestedRoot = requestedRoot;
        ResolvedRoot = resolvedRoot;
        RootKind = rootKind;
        ScopeResolved = true;
        FilesConsidered = counters.FilesConsidered;
        MetadataReads = counters.MetadataReads;
        EligibleFiles = eligibleFiles;
        MetadataRejected = counters.MetadataRejected;
        CandidatesYielded = counters.CandidatesYielded;
        ContentOpenAttempts = counters.ContentOpenAttempts;
        AppliedPolicies = Array.AsReadOnly(
        [
            $"recursive={scope.Recursive}",
            $"include-count={scope.Include.Count}",
            $"exclude-count={scope.Exclude.Count}",
            $"repository-ignores={scope.RepositoryIgnores}",
            $"global-ignores={scope.GlobalIgnores}",
            $"hidden-entries={scope.HiddenEntries}",
            $"follow-links={scope.FollowLinks}",
            $"inaccessible-entries={scope.InaccessibleEntries}",
            $"metadata-filters={CountMetadataFilters(scope.Metadata)}"
        ]);

        Summary = string.Join(
            "; ",
            [
                $"root-kind={RootKind}",
                $"scope-resolved={ScopeResolved}",
                $"files-considered={FilesConsidered}",
                $"metadata-reads={MetadataReads}",
                $"eligible-files={EligibleFiles}",
                $"metadata-rejected={MetadataRejected}",
                $"candidates-yielded={CandidatesYielded}",
                $"content-open-attempts={ContentOpenAttempts}",
                string.Join(',', AppliedPolicies)
            ]);
    }

    public string RequestedRoot { get; }

    public string ResolvedRoot { get; }

    public string RootKind { get; }

    public bool ScopeResolved { get; }

    public long FilesConsidered { get; }

    public long MetadataReads { get; }

    public long EligibleFiles { get; }

    public long MetadataRejected { get; }

    public long CandidatesYielded { get; }

    public long ContentOpenAttempts { get; }

    public IReadOnlyList<string> AppliedPolicies { get; }

    public string Summary { get; }

    private static int CountMetadataFilters(SearchMetadataPolicy metadata)
    {
        var count = metadata.NameIncludes.Count +
                    metadata.NameExcludes.Count +
                    metadata.ExtensionIncludes.Count +
                    metadata.ExtensionExcludes.Count;
        if (metadata.MinimumSizeBytes is not null)
            count++;
        if (metadata.MaximumSizeBytes is not null)
            count++;
        if (metadata.ModifiedAfterOrEqualUtc is not null)
            count++;
        if (metadata.ModifiedBeforeOrEqualUtc is not null)
            count++;

        return count;
    }
}
