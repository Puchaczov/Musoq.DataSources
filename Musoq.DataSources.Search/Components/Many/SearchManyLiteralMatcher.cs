#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Many;

/// <summary>
///     Searches a labeled set of literal patterns with one Aho-Corasick
///     traversal. Pattern selection remains independent after candidates are
///     produced, so cross-pattern overlaps and equal text are preserved.
/// </summary>
internal sealed class SearchManyLiteralMatcher : ISearchTextMatcher
{
    private readonly Pattern[] _patterns;
    private readonly TrieNode[] _nodes;
    private readonly long[] _nextAllowedStarts;
    private readonly long[] _matchStartLines;
    private readonly long[] _matchStartColumns;
    private readonly SearchCharCoordinate[] _matchCoordinates;
    private readonly char[] _matchCharacters;
    private readonly SearchCaseMode _caseMode;
    private readonly bool _wholeWord;
    private readonly bool _retainMatchText;
    private readonly int _coordinateRingLength;

    private readonly List<PendingMatch> _pendingMatches = [];
    private long _characterOffset;
    private int _state;
    private bool _hasDeferredCharacter;
    private char _deferredCharacter;
    private long _deferredLineNumber;
    private long _deferredUtf16Column;
    private SearchCharCoordinate _deferredCoordinate;

    public SearchManyLiteralMatcher(SearchManyRequest request)
        : this(LiteralPlan.Create(request), retainMatchText: true)
    {
    }

    internal SearchManyLiteralMatcher(
        LiteralPlan plan,
        bool retainMatchText)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var patterns = plan.Patterns;
        _patterns = patterns;
        _caseMode = plan.CaseMode;
        _wholeWord = plan.WholeWord;
        _retainMatchText = retainMatchText;
        _nextAllowedStarts = new long[patterns.Length];
        _coordinateRingLength = Math.Max(1, plan.MaximumPatternLength);
        _matchStartLines = new long[_coordinateRingLength];
        _matchStartColumns = new long[_coordinateRingLength];
        _matchCoordinates = new SearchCharCoordinate[_coordinateRingLength];
        _matchCharacters = new char[checked(plan.MaximumPatternLength + 2)];
        _nodes = plan.Nodes;
    }

    // These structural counters are an internal measurement seam for the
    // Search benchmark. They do not participate in matching and are not part
    // of the published datasource surface.
    internal int AutomatonNodeCount => _nodes.Length;

    internal int AutomatonTransitionCount
    {
        get
        {
            var count = 0;
            foreach (var node in _nodes)
                count = checked(count + node.Transitions.Count);

            return count;
        }
    }

    internal int AutomatonOutputReferenceCount
    {
        get
        {
            var count = 0;
            foreach (var node in _nodes)
                count = checked(count + node.Outputs.Count);

            return count;
        }
    }

    public void Reset()
    {
        _characterOffset = 0;
        _state = 0;
        Array.Clear(_nextAllowedStarts);
        _pendingMatches.Clear();
        _hasDeferredCharacter = false;
        _deferredCharacter = default;
        _deferredLineNumber = 0;
        _deferredUtf16Column = 0;
        _deferredCoordinate = default;
    }

    public void ConsumeBlock(
        ReadOnlySpan<char> block,
        ref long lineNumber,
        ref long utf16Column,
        ICollection<MatchSpan> spans,
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (coordinates.Length != 0 && coordinates.Length != block.Length)
        {
            throw new ArgumentException(
                "Character coordinates must be empty or aligned with the character block.",
                nameof(coordinates));
        }

        Span<char> surrogateLookahead = stackalloc char[2];
        var index = 0;
        while (index < block.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_hasDeferredCharacter)
            {
                var next = block[index];
                if (char.IsLowSurrogate(next))
                {
                    surrogateLookahead[0] = _deferredCharacter;
                    surrogateLookahead[1] = next;
                    ResolvePending(surrogateLookahead, spans, cancellationToken);
                }
                else
                {
                    ResolvePendingWithNextWord(false, spans, cancellationToken);
                }

                var deferredCharacter = _deferredCharacter;
                var deferredLineNumber = _deferredLineNumber;
                var deferredUtf16Column = _deferredUtf16Column;
                var deferredCoordinate = _deferredCoordinate;
                _hasDeferredCharacter = false;
                ProcessCharacter(
                    deferredCharacter,
                    deferredLineNumber,
                    deferredUtf16Column,
                    deferredCoordinate,
                    ref lineNumber,
                    ref utf16Column,
                    spans,
                    cancellationToken);
            }

            var current = block[index];
            var coordinate = coordinates.Length == 0
                ? default
                : coordinates[index];

            if (_wholeWord && _pendingMatches.Count > 0 && char.IsHighSurrogate(current))
            {
                _deferredCharacter = current;
                _deferredLineNumber = lineNumber;
                _deferredUtf16Column = utf16Column;
                _deferredCoordinate = coordinate;
                _hasDeferredCharacter = true;
                index++;
                continue;
            }

            if (_pendingMatches.Count > 0)
            {
                ResolvePending(block[index..], spans, cancellationToken);
            }

            ProcessCharacter(
                current,
                lineNumber,
                utf16Column,
                coordinate,
                ref lineNumber,
                ref utf16Column,
                spans,
                cancellationToken);
            index++;
        }
    }

    public void Complete(
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);

        if (_hasDeferredCharacter)
        {
            ResolvePendingWithNextWord(false, spans, cancellationToken);
            _hasDeferredCharacter = false;
            var ignoredLineNumber = _deferredLineNumber;
            var ignoredUtf16Column = _deferredUtf16Column;
            ProcessCharacter(
                _deferredCharacter,
                ignoredLineNumber,
                ignoredUtf16Column,
                _deferredCoordinate,
                ref ignoredLineNumber,
                ref ignoredUtf16Column,
                spans,
                cancellationToken);
        }

        ResolvePending(ReadOnlySpan<char>.Empty, spans, cancellationToken);
    }

    private void ProcessCharacter(
        char current,
        long lineNumber,
        long utf16Column,
        SearchCharCoordinate coordinate,
        ref long currentLineNumber,
        ref long currentUtf16Column,
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken)
    {
        var coordinateIndex = (int)(_characterOffset % _coordinateRingLength);
        _matchStartLines[coordinateIndex] = lineNumber;
        _matchStartColumns[coordinateIndex] = utf16Column;
        _matchCoordinates[coordinateIndex] = coordinate;
        _matchCharacters[(int)(_characterOffset % _matchCharacters.Length)] = current;

        if (current == '\n')
        {
            _state = 0;
            _characterOffset = checked(_characterOffset + 1);
            currentLineNumber = checked(currentLineNumber + 1);
            currentUtf16Column = 0;
            return;
        }

        var symbol = Normalize(current, _caseMode);
        _state = NextState(symbol);
        var outputs = _nodes[_state].Outputs;
        for (var outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var patternIndex = outputs[outputIndex];
            var pattern = _patterns[patternIndex];
            if (!pattern.IsSearchable)
                continue;

            var start = checked(_characterOffset - pattern.Length + 1);
            if (start < _nextAllowedStarts[patternIndex])
                continue;

            var startIndex = (int)(start % _coordinateRingLength);
            var span = CreateSpan(pattern, startIndex, start);
            if (_wholeWord)
            {
                if (!HasLeftWordBoundary(start, span.EndExclusive))
                    continue;

                _pendingMatches.Add(new PendingMatch(patternIndex, span));
            }
            else
            {
                _nextAllowedStarts[patternIndex] = span.EndExclusive;
                spans.Add(span);
            }
        }

        _characterOffset = checked(_characterOffset + 1);
        currentUtf16Column = checked(currentUtf16Column + 1);
    }

    private MatchSpan CreateSpan(Pattern pattern, int startIndex, long start)
    {
        var span = new MatchSpan(
            start,
            pattern.Length,
            _matchStartLines[startIndex],
            _matchStartColumns[startIndex],
            null,
            null)
        {
            PatternId = pattern.Id,
            MatchText = _retainMatchText
                ? _caseMode == SearchCaseMode.Sensitive
                    ? pattern.Text
                    : ReadMatchText(start, pattern.Length)
                : null
        };

        if (TryCreateByteRangeFromRing(
                startIndex,
                pattern.Length,
                out var byteOffset,
                out var byteLength))
        {
            span = span with
            {
                ByteOffset = byteOffset,
                ByteLength = byteLength
            };
        }

        return span;
    }

    private void ResolvePending(
        ReadOnlySpan<char> lookahead,
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken)
    {
        if (_pendingMatches.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        for (var index = 0; index < _pendingMatches.Count; index++)
        {
            var pending = _pendingMatches[index];
            if (!HasRightWordBoundary(pending.Span, lookahead))
                continue;

            if (pending.Span.Start < _nextAllowedStarts[pending.PatternIndex])
                continue;

            _nextAllowedStarts[pending.PatternIndex] = pending.Span.EndExclusive;
            spans.Add(pending.Span);
        }

        _pendingMatches.Clear();
    }

    private void ResolvePendingWithNextWord(
        bool nextIsWord,
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken)
    {
        if (_pendingMatches.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        for (var index = 0; index < _pendingMatches.Count; index++)
        {
            var pending = _pendingMatches[index];
            if (!HasRightWordBoundary(pending.Span, nextIsWord))
                continue;

            if (pending.Span.Start < _nextAllowedStarts[pending.PatternIndex])
                continue;

            _nextAllowedStarts[pending.PatternIndex] = pending.Span.EndExclusive;
            spans.Add(pending.Span);
        }

        _pendingMatches.Clear();
    }

    private int NextState(char symbol)
    {
        var state = _state;
        while (state != 0 && !_nodes[state].Transitions.TryGetValue(symbol, out _))
            state = _nodes[state].Failure;

        return _nodes[state].Transitions.TryGetValue(symbol, out var next)
            ? next
            : 0;
    }

    private bool HasLeftWordBoundary(long start, long endExclusive)
    {
        if (!TryGetStartingWord(start, endExclusive, out var firstIsWord))
            return false;

        if (start == 0 || CharacterAt(start - 1) == '\n')
            return true;

        if (!TryGetWordAt(start - 1, endExclusive, out var previousIsWord))
            return false;

        return firstIsWord != previousIsWord;
    }

    private bool HasRightWordBoundary(MatchSpan match, ReadOnlySpan<char> lookahead)
    {
        if (!TryGetEndingWord(match.Start, match.EndExclusive, out var lastIsWord))
            return false;

        if (lookahead.Length == 0 || lookahead[0] == '\n')
            return true;

        return TryGetLookaheadWord(lookahead, out var nextIsWord) &&
               lastIsWord != nextIsWord;
    }

    private bool HasRightWordBoundary(MatchSpan match, bool nextIsWord)
    {
        return TryGetEndingWord(match.Start, match.EndExclusive, out var lastIsWord) &&
               lastIsWord != nextIsWord;
    }

    private bool TryGetStartingWord(long start, long endExclusive, out bool isWord)
    {
        isWord = false;
        var first = CharacterAt(start);
        if (char.IsLowSurrogate(first))
            return false;

        if (char.IsHighSurrogate(first))
        {
            if (start + 1 >= endExclusive)
                return false;

            var low = CharacterAt(start + 1);
            if (!char.IsLowSurrogate(low))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(first, low);
            return true;
        }

        isWord = SearchWordPolicy.IsWordScalar(first);
        return true;
    }

    private bool TryGetEndingWord(long start, long endExclusive, out bool isWord)
    {
        isWord = false;
        var last = CharacterAt(endExclusive - 1);
        if (char.IsHighSurrogate(last))
            return false;

        if (char.IsLowSurrogate(last))
        {
            if (endExclusive - 2 < start)
                return false;

            var high = CharacterAt(endExclusive - 2);
            if (!char.IsHighSurrogate(high))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(high, last);
            return true;
        }

        isWord = SearchWordPolicy.IsWordScalar(last);
        return true;
    }

    private bool TryGetWordAt(long position, long availableEndExclusive, out bool isWord)
    {
        isWord = false;
        var current = CharacterAt(position);
        if (char.IsHighSurrogate(current))
        {
            if (position + 1 >= availableEndExclusive)
                return false;

            var low = CharacterAt(position + 1);
            if (!char.IsLowSurrogate(low))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(current, low);
            return true;
        }

        if (char.IsLowSurrogate(current))
        {
            if (position == 0)
                return false;

            var high = CharacterAt(position - 1);
            if (!char.IsHighSurrogate(high))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(high, current);
            return true;
        }

        isWord = SearchWordPolicy.IsWordScalar(current);
        return true;
    }

    private static bool TryGetLookaheadWord(
        ReadOnlySpan<char> lookahead,
        out bool isWord)
    {
        isWord = false;
        var current = lookahead[0];
        if (char.IsHighSurrogate(current))
        {
            if (lookahead.Length < 2 || !char.IsLowSurrogate(lookahead[1]))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(current, lookahead[1]);
            return true;
        }

        if (char.IsLowSurrogate(current))
            return false;

        isWord = SearchWordPolicy.IsWordScalar(current);
        return true;
    }

    private bool TryCreateByteRangeFromRing(
        int startIndex,
        int length,
        out long byteOffset,
        out long byteLength)
    {
        var first = _matchCoordinates[startIndex];
        var last = _matchCoordinates[
            (startIndex + length - 1) % _coordinateRingLength];
        byteOffset = 0;
        byteLength = 0;
        if (!first.IsMapped || !first.CanStart || !last.IsMapped || !last.CanEnd)
            return false;

        var end = first.ByteEndExclusive;
        for (var index = 1; index < length; index++)
        {
            var current = _matchCoordinates[(startIndex + index) % _coordinateRingLength];
            if (!current.IsMapped)
                return false;

            var sameScalar = current.ByteOffset == first.ByteOffset &&
                              current.ByteEndExclusive == first.ByteEndExclusive;
            if (current.ByteOffset != end && !sameScalar)
                return false;

            end = Math.Max(end, current.ByteEndExclusive);
        }

        byteOffset = first.ByteOffset;
        byteLength = checked(end - byteOffset);
        return true;
    }

    private char CharacterAt(long position)
    {
        return _matchCharacters[(int)(position % _matchCharacters.Length)];
    }

    private string ReadMatchText(long start, int length)
    {
        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = _matchCharacters[
                (int)((start + index) % _matchCharacters.Length)];
        }

        return new string(characters);
    }

    /// <summary>
    ///     Immutable compiled data shared by all file-local matcher cursors.
    ///     The cursor arrays remain in SearchManyLiteralMatcher because they
    ///     contain mutable offsets and boundary state.
    /// </summary>
    internal sealed class LiteralPlan
    {
        private LiteralPlan(
            Pattern[] patterns,
            TrieNode[] nodes,
            SearchCaseMode caseMode,
            bool wholeWord,
            int maximumPatternLength)
        {
            Patterns = patterns;
            Nodes = nodes;
            CaseMode = caseMode;
            WholeWord = wholeWord;
            MaximumPatternLength = maximumPatternLength;
        }

        internal Pattern[] Patterns { get; }

        internal TrieNode[] Nodes { get; }

        internal SearchCaseMode CaseMode { get; }

        internal bool WholeWord { get; }

        internal int MaximumPatternLength { get; }

        internal static LiteralPlan Create(SearchManyRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Options.Selection != SearchSelectionMode.LeftmostFirstNonOverlapping)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidManyInput(
                        "options.selection is not supported by the literal matcher",
                        SearchDiagnosticPhase.Argument));
            }

            if (request.Options.CaseMode is not SearchCaseMode.Sensitive and not SearchCaseMode.Insensitive)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidManyInput(
                        "options.case is not supported by the literal matcher",
                        SearchDiagnosticPhase.Argument));
            }

            if (request.Options.Take is not null ||
                request.Options.PartialPolicy != SearchPartialPolicy.Reject ||
                request.Options.Validation != SearchValidationMode.FullInput)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidManyInput(
                        "options.take, options.partialPolicy=allow and options.validation=observed-prefix require the later completion surface",
                        SearchDiagnosticPhase.Argument));
            }

            var patterns = new Pattern[request.Patterns.Count];
            var maximumPatternLength = 0;
            for (var index = 0; index < request.Patterns.Count; index++)
            {
                var pattern = request.Patterns[index];
                if (pattern.Mode != SearchPatternMode.Literal)
                {
                    throw new SearchRequestException(
                        SearchDiagnosticCatalog.InvalidManyInput(
                            $"patterns[{index}].mode '{pattern.Mode}' is not supported by the literal matcher",
                            SearchDiagnosticPhase.Argument));
                }

                if (pattern.Pattern.Length == 0)
                {
                    throw new SearchRequestException(
                        SearchDiagnosticCatalog.InvalidManyInput(
                            $"patterns[{index}].pattern must not be empty",
                            SearchDiagnosticPhase.Argument));
                }

                patterns[index] = new Pattern(
                    pattern.Id,
                    pattern.Pattern,
                    Normalize(pattern.Pattern, request.Options.CaseMode),
                    pattern.Pattern.IndexOf('\n') < 0);
                maximumPatternLength = Math.Max(maximumPatternLength, pattern.Pattern.Length);
            }

            return new LiteralPlan(
                patterns,
                BuildTrie(patterns),
                request.Options.CaseMode,
                request.Options.WholeWord,
                maximumPatternLength);
        }
    }

    private static TrieNode[] BuildTrie(Pattern[] patterns)
    {
        var nodes = new List<TrieNode> { new() };
        for (var patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
        {
            var pattern = patterns[patternIndex];
            if (!pattern.IsSearchable)
                continue;

            var nodeIndex = 0;
            foreach (var symbol in pattern.NormalizedText)
            {
                if (!nodes[nodeIndex].Transitions.TryGetValue(symbol, out var next))
                {
                    next = nodes.Count;
                    nodes[nodeIndex].Transitions.Add(symbol, next);
                    nodes.Add(new TrieNode());
                }

                nodeIndex = next;
            }

            nodes[nodeIndex].Outputs.Add(patternIndex);
        }

        var queue = new Queue<int>();
        foreach (var child in nodes[0].Transitions.Values)
        {
            nodes[child].Failure = 0;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var nodeIndex = queue.Dequeue();
            foreach (var transition in nodes[nodeIndex].Transitions)
            {
                var symbol = transition.Key;
                var child = transition.Value;
                var failure = nodes[nodeIndex].Failure;
                while (failure != 0 &&
                       !nodes[failure].Transitions.ContainsKey(symbol))
                {
                    failure = nodes[failure].Failure;
                }

                if (nodes[failure].Transitions.TryGetValue(symbol, out var failureTarget) &&
                    failureTarget != child)
                {
                    nodes[child].Failure = failureTarget;
                }
                else
                {
                    nodes[child].Failure = 0;
                }

                var inherited = nodes[nodes[child].Failure].Outputs;
                for (var outputIndex = 0; outputIndex < inherited.Count; outputIndex++)
                    nodes[child].Outputs.Add(inherited[outputIndex]);

                queue.Enqueue(child);
            }
        }

        foreach (var node in nodes)
            node.Outputs.Sort();

        return nodes.ToArray();
    }

    private static string Normalize(string value, SearchCaseMode caseMode)
    {
        if (caseMode == SearchCaseMode.Sensitive)
            return value;

        var normalized = new char[value.Length];
        for (var index = 0; index < value.Length; index++)
            normalized[index] = Normalize(value[index], caseMode);

        return new string(normalized);
    }

    private static char Normalize(char value, SearchCaseMode caseMode)
    {
        return caseMode == SearchCaseMode.Sensitive
            ? value
            : char.ToUpperInvariant(value);
    }

    internal sealed class TrieNode
    {
        public Dictionary<char, int> Transitions { get; } = [];

        public int Failure { get; set; }

        public List<int> Outputs { get; } = [];
    }

    internal readonly record struct Pattern(
        string Id,
        string Text,
        string NormalizedText,
        bool IsSearchable)
    {
        public int Length => Text.Length;
    }

    private readonly record struct PendingMatch(int PatternIndex, MatchSpan Span);
}
