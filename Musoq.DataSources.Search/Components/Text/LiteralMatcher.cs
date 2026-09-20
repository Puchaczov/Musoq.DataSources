#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;

namespace Musoq.DataSources.Search.Components.Text;

internal sealed class LiteralMatcher : ISearchTextMatcher
{
    private readonly string _literal;
    private readonly SearchCaseMode _caseMode;
    private readonly bool _wholeWord;
    private readonly bool _containsNewline;
    private readonly SearchValues<char> _firstCharacters;
    private readonly int[] _prefixTable;
    private readonly long[] _matchStartLines;
    private readonly long[] _matchStartColumns;
    private readonly SearchCharCoordinate[] _matchCoordinates;
    private readonly char[] _matchCharacters;
    private readonly bool _useSingleCharacterIndexOf;
    private long _characterOffset;
    private int _matchedLength;
    private MatchSpan _pendingMatch;
    private bool _hasPendingMatch;
    private char _deferredCharacter;
    private long _deferredLineNumber;
    private long _deferredUtf16Column;
    private SearchCharCoordinate _deferredCoordinate;
    private bool _hasDeferredCharacter;

    public LiteralMatcher(string literal)
        : this(literal, SearchCaseMode.Sensitive, wholeWord: false, useSingleCharacterIndexOf: true)
    {
    }

    public LiteralMatcher(
        string literal,
        SearchCaseMode caseMode,
        bool wholeWord)
        : this(literal, caseMode, wholeWord, useSingleCharacterIndexOf: true)
    {
    }

    // The switch keeps the pre-specialization branch available to the benchmark and
    // boundary tests without exposing an alternate published constructor.
    internal LiteralMatcher(
        string literal,
        SearchCaseMode caseMode,
        bool wholeWord,
        bool useSingleCharacterIndexOf)
    {
        ArgumentNullException.ThrowIfNull(literal);
        if (literal.Length == 0)
            throw new ArgumentException("The search literal must not be empty.", nameof(literal));
        if (caseMode is not SearchCaseMode.Sensitive and not SearchCaseMode.Insensitive)
            throw new ArgumentOutOfRangeException(nameof(caseMode));

        _literal = literal;
        _caseMode = caseMode;
        _wholeWord = wholeWord;
        _useSingleCharacterIndexOf = useSingleCharacterIndexOf;
        _containsNewline = literal.IndexOf('\n') >= 0;
        _firstCharacters = SearchValues.Create(literal.AsSpan(0, 1));
        _prefixTable = BuildPrefixTable(literal, caseMode);
        _matchStartLines = new long[literal.Length];
        _matchStartColumns = new long[literal.Length];
        _matchCoordinates = new SearchCharCoordinate[literal.Length];
        _matchCharacters = new char[checked(literal.Length + 2)];
    }

    public void Reset()
    {
        _characterOffset = 0;
        _matchedLength = 0;
        _hasPendingMatch = false;
        _pendingMatch = default;
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
        CancellationToken cancellationToken = default)
    {
        ConsumeBlock(
            block,
            ref lineNumber,
            ref utf16Column,
            spans,
            ReadOnlySpan<SearchCharCoordinate>.Empty,
            cancellationToken);
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

        if (_containsNewline)
        {
            AdvancePositionOnly(block, ref lineNumber, ref utf16Column, cancellationToken);
            return;
        }

        if (_caseMode != SearchCaseMode.Sensitive || _wholeWord)
        {
            ConsumeBlockWithPolicy(
                block,
                ref lineNumber,
                ref utf16Column,
                spans,
                coordinates,
                cancellationToken);
            return;
        }

        if (_useSingleCharacterIndexOf && _literal.Length == 1)
        {
            ConsumeSingleCharacterBlock(
                block,
                ref lineNumber,
                ref utf16Column,
                spans,
                coordinates,
                cancellationToken);
            return;
        }

        var index = 0;
        while (index < block.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_matchedLength == 0)
            {
                var candidateOffset = block[index..].IndexOfAny(_firstCharacters);
                if (candidateOffset < 0)
                {
                    AdvancePositionOnly(
                        block[index..],
                        ref lineNumber,
                        ref utf16Column,
                        cancellationToken);
                    return;
                }

                if (candidateOffset > 0)
                {
                    AdvancePositionOnly(
                        block.Slice(index, candidateOffset),
                        ref lineNumber,
                        ref utf16Column,
                        cancellationToken);
                    index += candidateOffset;
                }

                var remaining = block.Length - index;
                if (remaining >= _literal.Length)
                {
                    var candidate = block.Slice(index, _literal.Length);
                    if (candidate.SequenceEqual(_literal.AsSpan()))
                    {
                        var byteOffset = (long?)null;
                        var byteLength = (long?)null;
                        if (coordinates.Length != 0 && TryCreateByteRange(
                                coordinates.Slice(index, _literal.Length),
                                out var mappedOffset,
                                out var mappedLength))
                        {
                            byteOffset = mappedOffset;
                            byteLength = mappedLength;
                        }

                        spans.Add(new MatchSpan(
                            _characterOffset,
                            _literal.Length,
                            lineNumber,
                            utf16Column,
                            byteOffset,
                            byteLength)
                        {
                            MatchText = _literal
                        });
                        AdvancePositionOnly(
                            candidate,
                            ref lineNumber,
                            ref utf16Column,
                            cancellationToken);
                        index += _literal.Length;
                        continue;
                    }

                    AdvancePositionOnly(
                        block.Slice(index, 1),
                        ref lineNumber,
                        ref utf16Column,
                        cancellationToken);
                    index++;
                    continue;
                }
            }

            var current = block[index];
            if (current == '\n')
            {
                _matchedLength = 0;
                AdvancePositionOnly(
                    block.Slice(index, 1),
                    ref lineNumber,
                    ref utf16Column,
                    cancellationToken);
                index++;
                continue;
            }

            var coordinate = coordinates.Length == 0
                ? default
                : coordinates[index];
            if (TryConsume(current, lineNumber, utf16Column, coordinate, out var span))
                spans.Add(span);

            AdvanceCoordinates(current, ref lineNumber, ref utf16Column);
            index++;
        }
    }

    private void ConsumeSingleCharacterBlock(
        ReadOnlySpan<char> block,
        ref long lineNumber,
        ref long utf16Column,
        ICollection<MatchSpan> spans,
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        CancellationToken cancellationToken)
    {
        var index = 0;
        var literalCharacter = _literal[0];
        while (index < block.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matchOffset = block[index..].IndexOf(literalCharacter);
            if (matchOffset < 0)
            {
                AdvancePositionOnly(
                    block[index..],
                    ref lineNumber,
                    ref utf16Column,
                    cancellationToken);
                return;
            }

            if (matchOffset > 0)
            {
                AdvancePositionOnly(
                    block.Slice(index, matchOffset),
                    ref lineNumber,
                    ref utf16Column,
                    cancellationToken);
                index += matchOffset;
            }

            var byteOffset = (long?)null;
            var byteLength = (long?)null;
            if (coordinates.Length != 0 && TryCreateByteRange(
                    coordinates.Slice(index, 1),
                    out var mappedOffset,
                    out var mappedLength))
            {
                byteOffset = mappedOffset;
                byteLength = mappedLength;
            }

            spans.Add(new MatchSpan(
                _characterOffset,
                1,
                lineNumber,
                utf16Column,
                byteOffset,
                byteLength)
            {
                MatchText = block[index].ToString()
            });
            AdvancePositionOnly(
                block.Slice(index, 1),
                ref lineNumber,
                ref utf16Column,
                cancellationToken);
            index++;
        }
    }

    private void ConsumeBlockWithPolicy(
        ReadOnlySpan<char> block,
        ref long lineNumber,
        ref long utf16Column,
        ICollection<MatchSpan> spans,
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        CancellationToken cancellationToken)
    {
        Span<char> deferredLookahead = stackalloc char[2];
        var index = 0;
        while (index < block.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_hasDeferredCharacter)
            {
                var next = block[index];
                if (char.IsLowSurrogate(next))
                {
                    deferredLookahead[0] = _deferredCharacter;
                    deferredLookahead[1] = next;
                    ResolvePendingMatch(deferredLookahead, spans, cancellationToken);
                }
                else
                {
                    ResolvePendingMatchWithNextWord(
                        nextIsWord: false,
                        spans,
                        cancellationToken);
                }

                var deferredCharacter = _deferredCharacter;
                var deferredLineNumber = _deferredLineNumber;
                var deferredUtf16Column = _deferredUtf16Column;
                var deferredCoordinate = _deferredCoordinate;
                _hasDeferredCharacter = false;
                TryConsumeWithPolicy(
                    deferredCharacter,
                    deferredLineNumber,
                    deferredUtf16Column,
                    deferredCoordinate,
                    spans);
                AdvanceCoordinates(
                    deferredCharacter,
                    ref lineNumber,
                    ref utf16Column);
            }

            var current = block[index];
            var coordinate = coordinates.Length == 0
                ? default
                : coordinates[index];
            if (_hasPendingMatch &&
                index == block.Length - 1 &&
                char.IsHighSurrogate(current))
            {
                _deferredCharacter = current;
                _deferredLineNumber = lineNumber;
                _deferredUtf16Column = utf16Column;
                _deferredCoordinate = coordinate;
                _hasDeferredCharacter = true;
                return;
            }

            ResolvePendingMatch(
                block[index..],
                spans,
                cancellationToken);

            if (current == '\n')
            {
                StoreCharacter(current);
                _matchedLength = 0;
                AdvancePositionOnly(
                    block.Slice(index, 1),
                    ref lineNumber,
                    ref utf16Column,
                    cancellationToken);
                index++;
                continue;
            }

            TryConsumeWithPolicy(
                current,
                lineNumber,
                utf16Column,
                coordinate,
                spans);
            AdvanceCoordinates(current, ref lineNumber, ref utf16Column);
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
            cancellationToken.ThrowIfCancellationRequested();
            ResolvePendingMatchWithNextWord(
                nextIsWord: false,
                spans,
                cancellationToken);
            TryConsumeWithPolicy(
                _deferredCharacter,
                _deferredLineNumber,
                _deferredUtf16Column,
                _deferredCoordinate,
                spans);
            _hasDeferredCharacter = false;
        }

        ResolvePendingMatch(
            ReadOnlySpan<char>.Empty,
            spans,
            cancellationToken);
    }

    public bool TryConsume(
        char current,
        long lineNumber,
        long utf16Column,
        out MatchSpan span)
    {
        return TryConsume(
            current,
            lineNumber,
            utf16Column,
            default,
            out span);
    }

    private bool TryConsume(
        char current,
        long lineNumber,
        long utf16Column,
        SearchCharCoordinate coordinate,
        out MatchSpan span)
    {
        var ringIndex = (int)(_characterOffset % _literal.Length);
        _matchStartLines[ringIndex] = lineNumber;
        _matchStartColumns[ringIndex] = utf16Column;
        _matchCoordinates[ringIndex] = coordinate;

        while (_matchedLength > 0 && _literal[_matchedLength] != current)
            _matchedLength = _prefixTable[_matchedLength - 1];

        if (_literal[_matchedLength] == current)
            _matchedLength++;

        if (_matchedLength == _literal.Length)
        {
            var start = _characterOffset - _literal.Length + 1;
            var startRingIndex = (int)(start % _literal.Length);
            span = new MatchSpan(
                start,
                _literal.Length,
                _matchStartLines[startRingIndex],
                _matchStartColumns[startRingIndex],
                null,
                null)
            {
                MatchText = _literal
            };
            if (TryCreateByteRangeFromRing(startRingIndex, out var byteOffset, out var byteLength))
            {
                span = span with
                {
                    ByteOffset = byteOffset,
                    ByteLength = byteLength
                };
            }
            _matchedLength = 0;
            _characterOffset++;
            return true;
        }

        _characterOffset++;
        span = default;
        return false;
    }

    private void TryConsumeWithPolicy(
        char current,
        long lineNumber,
        long utf16Column,
        SearchCharCoordinate coordinate,
        ICollection<MatchSpan> spans)
    {
        var ringIndex = (int)(_characterOffset % _literal.Length);
        _matchStartLines[ringIndex] = lineNumber;
        _matchStartColumns[ringIndex] = utf16Column;
        _matchCoordinates[ringIndex] = coordinate;
        StoreCharacter(current);

        while (_matchedLength > 0 &&
               !CharactersEqual(_literal[_matchedLength], current))
        {
            _matchedLength = _prefixTable[_matchedLength - 1];
        }

        if (_matchedLength < _literal.Length &&
            CharactersEqual(_literal[_matchedLength], current))
        {
            _matchedLength++;
        }

        if (_matchedLength == _literal.Length)
        {
            var start = _characterOffset - _literal.Length + 1;
            var startRingIndex = (int)(start % _literal.Length);
            var span = CreateMappedSpan(startRingIndex, start);
            _characterOffset = checked(_characterOffset + 1);

            if (!_wholeWord)
            {
                _matchedLength = 0;
                spans.Add(span);
                return;
            }

            if (!HasLeftWordBoundary(start, span.EndExclusive))
            {
                _matchedLength = _prefixTable[_literal.Length - 1];
                return;
            }

            _pendingMatch = span;
            _hasPendingMatch = true;
            return;
        }

        _characterOffset = checked(_characterOffset + 1);
    }

    private MatchSpan CreateMappedSpan(int startRingIndex, long start)
    {
        var span = new MatchSpan(
            start,
            _literal.Length,
            _matchStartLines[startRingIndex],
            _matchStartColumns[startRingIndex],
            null,
            null)
        {
            MatchText = ReadMatchText(start)
        };
        if (TryCreateByteRangeFromRing(startRingIndex, out var byteOffset, out var byteLength))
        {
            span = span with
            {
                ByteOffset = byteOffset,
                ByteLength = byteLength
            };
        }

        return span;
    }

    private void ResolvePendingMatch(
        ReadOnlySpan<char> lookahead,
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken)
    {
        if (!_hasPendingMatch)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var pending = _pendingMatch;
        _pendingMatch = default;
        _hasPendingMatch = false;
        if (HasRightWordBoundary(pending, lookahead))
        {
            _matchedLength = 0;
            spans.Add(pending);
            return;
        }

        _matchedLength = _prefixTable[_literal.Length - 1];
    }

    private void ResolvePendingMatchWithNextWord(
        bool nextIsWord,
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken)
    {
        if (!_hasPendingMatch)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var pending = _pendingMatch;
        _pendingMatch = default;
        _hasPendingMatch = false;
        if (HasRightWordBoundary(pending, nextIsWord))
        {
            _matchedLength = 0;
            spans.Add(pending);
            return;
        }

        _matchedLength = _prefixTable[_literal.Length - 1];
    }

    private bool TryCreateByteRangeFromRing(
        int startRingIndex,
        out long byteOffset,
        out long byteLength)
    {
        var first = _matchCoordinates[startRingIndex];
        var last = _matchCoordinates[
            (startRingIndex + _matchCoordinates.Length - 1) % _matchCoordinates.Length];
        byteOffset = 0;
        byteLength = 0;
        if (!first.IsMapped || !first.CanStart || !last.IsMapped || !last.CanEnd)
            return false;

        var end = first.ByteEndExclusive;
        for (var index = 1; index < _matchCoordinates.Length; index++)
        {
            var current = _matchCoordinates[(startRingIndex + index) % _matchCoordinates.Length];
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

    private static bool TryCreateByteRange(
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        out long byteOffset,
        out long byteLength)
    {
        byteOffset = 0;
        byteLength = 0;
        if (coordinates.Length == 0)
            return false;

        var first = coordinates[0];
        var last = coordinates[^1];
        if (!first.IsMapped || !first.CanStart || !last.IsMapped || !last.CanEnd)
            return false;

        var end = first.ByteEndExclusive;
        for (var index = 1; index < coordinates.Length; index++)
        {
            var current = coordinates[index];
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

    private void StoreCharacter(char current)
    {
        var ringIndex = (int)(_characterOffset % _matchCharacters.Length);
        _matchCharacters[ringIndex] = current;
    }

    private string ReadMatchText(long start)
    {
        var text = new char[_literal.Length];
        for (var index = 0; index < text.Length; index++)
            text[index] = CharacterAt(start + index);

        return new string(text);
    }

    private bool CharactersEqual(char left, char right)
    {
        return _caseMode == SearchCaseMode.Sensitive
            ? left == right
            : char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
    }

    private bool HasLeftWordBoundary(long start, long endExclusive)
    {
        if (!TryGetStartingWord(start, endExclusive, out var firstIsWord))
            return false;

        if (start == 0)
            return true;

        var previous = CharacterAt(start - 1);
        if (previous == '\n')
            return true;

        if (!TryGetWordAt(start - 1, endExclusive, out var previousIsWord))
            return false;

        return firstIsWord != previousIsWord;
    }

    private bool HasRightWordBoundary(
        MatchSpan match,
        ReadOnlySpan<char> lookahead)
    {
        if (!TryGetEndingWord(match.Start, match.EndExclusive, out var lastIsWord))
            return false;

        if (lookahead.Length == 0 || lookahead[0] == '\n')
            return true;

        if (!TryGetLookaheadWord(lookahead, out var nextIsWord))
            return false;

        return lastIsWord != nextIsWord;
    }

    private bool HasRightWordBoundary(MatchSpan match, bool nextIsWord)
    {
        if (!TryGetEndingWord(match.Start, match.EndExclusive, out var lastIsWord))
            return false;

        return lastIsWord != nextIsWord;
    }

    private bool TryGetStartingWord(
        long start,
        long endExclusive,
        out bool isWord)
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

    private bool TryGetEndingWord(
        long start,
        long endExclusive,
        out bool isWord)
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

    private bool TryGetWordAt(
        long position,
        long availableEndExclusive,
        out bool isWord)
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

    private char CharacterAt(long position)
    {
        return _matchCharacters[(int)(position % _matchCharacters.Length)];
    }

    private void AdvancePositionOnly(
        ReadOnlySpan<char> characters,
        ref long lineNumber,
        ref long utf16Column,
        CancellationToken cancellationToken)
    {
        const int cancellationCheckInterval = 256;
        var offset = 0;
        while (offset < characters.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(cancellationCheckInterval, characters.Length - offset);
            var segment = characters.Slice(offset, length);
            var newlineOffset = segment.IndexOf('\n');
            if (newlineOffset < 0)
            {
                _characterOffset = checked(_characterOffset + segment.Length);
                utf16Column = checked(utf16Column + segment.Length);
                offset += segment.Length;
                continue;
            }

            var consumed = newlineOffset + 1;
            _characterOffset = checked(_characterOffset + consumed);
            lineNumber = checked(lineNumber + 1);
            utf16Column = 0;
            offset += consumed;
        }
    }

    private static void AdvanceCoordinates(
        char current,
        ref long lineNumber,
        ref long utf16Column)
    {
        if (current == '\n')
        {
            lineNumber = checked(lineNumber + 1);
            utf16Column = 0;
        }
        else
        {
            utf16Column = checked(utf16Column + 1);
        }
    }

    private static int[] BuildPrefixTable(
        string literal,
        SearchCaseMode caseMode)
    {
        var prefixTable = new int[literal.Length];
        var prefixLength = 0;

        for (var index = 1; index < literal.Length; index++)
        {
            while (prefixLength > 0 && !CharactersEqual(
                       literal[prefixLength],
                       literal[index],
                       caseMode))
            {
                prefixLength = prefixTable[prefixLength - 1];
            }

            if (CharactersEqual(literal[prefixLength], literal[index], caseMode))
                prefixLength++;

            prefixTable[index] = prefixLength;
        }

        return prefixTable;
    }

    private static bool CharactersEqual(
        char left,
        char right,
        SearchCaseMode caseMode)
    {
        return caseMode == SearchCaseMode.Sensitive
            ? left == right
            : char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
    }
}
