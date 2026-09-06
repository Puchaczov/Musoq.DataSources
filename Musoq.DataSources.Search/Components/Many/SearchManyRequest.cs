#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Many;

internal sealed record SearchManyRequest
{
    public const int CurrentVersion = 1;

    public SearchManyRequest(
        IEnumerable<SearchManyPattern> patterns,
        SearchManyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        var copy = patterns.ToArray();
        if (copy.Length == 0 || copy.Length > SearchManyRequestParser.MaxPatternCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(patterns),
                $"A many request must contain 1 to {SearchManyRequestParser.MaxPatternCount} patterns.");
        }

        Options = options ?? SearchManyOptions.Default;
        Options.Limits.ValidatePatternSet(copy);
        Patterns = Array.AsReadOnly(copy);
    }

    public int Version => CurrentVersion;

    public IReadOnlyList<SearchManyPattern> Patterns { get; }

    public SearchManyOptions Options { get; }
}

internal sealed record SearchManyPattern
{
    public SearchManyPattern(
        string id,
        string pattern,
        SearchPatternMode mode)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Mode = mode;
    }

    public string Id { get; }

    public string Pattern { get; }

    public SearchPatternMode Mode { get; }
}

internal sealed record SearchManyOptions
{
    public static SearchManyOptions Default { get; } = new();

    public SearchManyOptions(
        SearchCaseMode caseMode = SearchCaseMode.Sensitive,
        bool wholeWord = false,
        SearchEncodingMode encodingMode = SearchEncodingMode.Auto,
        SearchSelectionMode selection = SearchSelectionMode.LeftmostFirstNonOverlapping,
        int? take = null,
        SearchPartialPolicy partialPolicy = SearchPartialPolicy.Reject,
        SearchValidationMode validation = SearchValidationMode.FullInput,
        ScopePolicy? scope = null,
        SearchResourceLimits? limits = null)
    {
        CaseMode = caseMode;
        WholeWord = wholeWord;
        EncodingMode = encodingMode;
        Selection = selection;
        Take = take;
        PartialPolicy = partialPolicy;
        Validation = validation;
        Scope = scope ?? ScopePolicy.Default;
        Limits = limits ?? SearchResourceLimits.Default;
    }

    public SearchCaseMode CaseMode { get; }

    public bool WholeWord { get; }

    public SearchEncodingMode EncodingMode { get; }

    public SearchSelectionMode Selection { get; }

    public int? Take { get; }

    public SearchPartialPolicy PartialPolicy { get; }

    public SearchValidationMode Validation { get; }

    public ScopePolicy Scope { get; }

    public SearchResourceLimits Limits { get; }
}

internal enum SearchSelectionMode
{
    LeftmostFirstNonOverlapping
}

internal enum SearchPartialPolicy
{
    Reject,
    Allow
}

internal enum SearchValidationMode
{
    FullInput,
    ObservedPrefix
}
