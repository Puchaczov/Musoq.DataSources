#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

internal static class SearchRegexBackend
{
    public const string Dialect = "portable-nonbacktracking-v1";

    public const int MaxPatternLength = 65_536;

    public const int MaxNestingDepth = 256;

    public const int MaxCachedPatterns = 128;

    public const int MaxCompilationMilliseconds = 5_000;

    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly object CacheLock = new();

    private static readonly Dictionary<RegexCacheKey, LinkedListNode<RegexCacheEntry>> Cache = [];

    private static readonly LinkedList<RegexCacheEntry> Lru = [];

    private static long _compilationCount;

    public static Regex Compile(
        string? pattern,
        SearchCaseMode caseMode = SearchCaseMode.Sensitive,
        bool wholeWord = false,
        int maxPatternLength = MaxPatternLength,
        int maxCompilationMilliseconds = MaxCompilationMilliseconds)
    {
        ValidatePattern(pattern, caseMode, maxPatternLength);
        var key = new RegexCacheKey(pattern!, caseMode, wholeWord);

        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                Lru.Remove(cached);
                Lru.AddFirst(cached);
                return cached.Value.Regex;
            }

            var regex = CreateRegex(
                key.Pattern,
                key.CaseMode,
                maxCompilationMilliseconds);
            var entry = Lru.AddFirst(new RegexCacheEntry(key, regex));
            Cache.Add(key, entry);
            _compilationCount = checked(_compilationCount + 1);

            if (Cache.Count > MaxCachedPatterns)
            {
                var evicted = Lru.Last ??
                              throw new InvalidOperationException(
                                  "The Search regex cache lost its least-recently-used entry.");
                Lru.RemoveLast();
                Cache.Remove(evicted.Value.Key);
            }

            return regex;
        }
    }

    public static void Validate(
        string? pattern,
        SearchCaseMode caseMode = SearchCaseMode.Sensitive,
        bool wholeWord = false,
        int maxPatternLength = MaxPatternLength,
        int maxCompilationMilliseconds = MaxCompilationMilliseconds)
    {
        _ = Compile(
            pattern,
            caseMode,
            wholeWord,
            maxPatternLength,
            maxCompilationMilliseconds);
    }

    internal static int CachedPatternCount
    {
        get
        {
            lock (CacheLock)
                return Cache.Count;
        }
    }

    internal static long CompilationCount
    {
        get
        {
            lock (CacheLock)
                return _compilationCount;
        }
    }

    internal static void ResetForTests()
    {
        lock (CacheLock)
        {
            Cache.Clear();
            Lru.Clear();
            _compilationCount = 0;
        }
    }

    private static void ValidatePattern(
        string? pattern,
        SearchCaseMode caseMode,
        int maxPatternLength)
    {
        if (pattern is null || pattern.Length == 0)
        {
            throw new SearchPatternException(
                SearchDiagnosticCatalog.InvalidArgument("pattern"));
        }

        if (maxPatternLength <= 0 || maxPatternLength > MaxPatternLength)
            throw new ArgumentOutOfRangeException(nameof(maxPatternLength));

        if (pattern.Length > maxPatternLength)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit("pattern", maxPatternLength),
                budgetCode: "pattern-size");
        }

        if (caseMode is not SearchCaseMode.Sensitive and not SearchCaseMode.Insensitive)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("case"));
        }

        ValidateNestingDepth(pattern);
    }

    private static Regex CreateRegex(
        string pattern,
        SearchCaseMode caseMode,
        int maxCompilationMilliseconds)
    {
        if (maxCompilationMilliseconds is < 0 or > MaxCompilationMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(maxCompilationMilliseconds));

        var unsupportedConstruct = FindUnsupportedConstruct(pattern);
        if (unsupportedConstruct is not null)
        {
            throw new SearchPatternException(
                SearchDiagnosticCatalog.UnsupportedRegexConstruct(unsupportedConstruct),
                new NotSupportedException(
                    $"The {Dialect} dialect does not support {unsupportedConstruct}."));
        }

        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        if (caseMode == SearchCaseMode.Insensitive)
            options |= RegexOptions.IgnoreCase;

        var started = Stopwatch.GetTimestamp();
        try
        {
            var regex = new Regex(pattern, options, MatchTimeout);
            if (Stopwatch.GetElapsedTime(started) >
                TimeSpan.FromMilliseconds(maxCompilationMilliseconds))
            {
                throw new SearchResourceLimitException(
                    SearchDiagnosticCatalog.ResourceLimit(
                        "compile-cost",
                        maxCompilationMilliseconds),
                    budgetCode: "compile-cost");
            }

            return regex;
        }
        catch (SearchResourceLimitException)
        {
            throw;
        }
        catch (NotSupportedException exception)
        {
            var construct = FindUnsupportedConstruct(pattern) ??
                            "a construct unsupported by the portable non-backtracking dialect";
            throw new SearchPatternException(
                SearchDiagnosticCatalog.UnsupportedRegexConstruct(construct),
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new SearchPatternException(
                SearchDiagnosticCatalog.InvalidRegex(),
                exception);
        }
    }

    private static void ValidateNestingDepth(string pattern)
    {
        var depth = 0;
        var inCharacterClass = false;
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (inCharacterClass)
            {
                if (character == '\\')
                    index++;
                else if (character == ']')
                    inCharacterClass = false;

                continue;
            }

            if (character == '\\')
            {
                index++;
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (character == '(')
            {
                depth = checked(depth + 1);
                if (depth > MaxNestingDepth)
                {
                    throw new SearchResourceLimitException(
                        SearchDiagnosticCatalog.ResourceLimit(
                            "pattern nesting depth",
                            MaxNestingDepth));
                }
            }
            else if (character == ')' && depth > 0)
            {
                depth--;
            }
        }
    }

    private static string? FindUnsupportedConstruct(string pattern)
    {
        var inCharacterClass = false;
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (inCharacterClass)
            {
                if (character == '\\')
                    index++;
                else if (character == ']')
                    inCharacterClass = false;

                continue;
            }

            if (character == '\\')
            {
                if (index + 1 >= pattern.Length)
                    continue;

                var escaped = pattern[++index];
                if (escaped is >= '1' and <= '9')
                    return "backreferences";

                if (escaped == 'G')
                    return "contiguous-match anchors (\\G)";

                if (escaped == 'k' &&
                    index + 1 < pattern.Length &&
                    (pattern[index + 1] == '<' || pattern[index + 1] == '\''))
                {
                    return "backreferences";
                }

                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (character != '(')
                continue;

            if (StartsWith(pattern, index, "(?=") ||
                StartsWith(pattern, index, "(?!"))
            {
                return "lookahead/lookaround";
            }

            if (StartsWith(pattern, index, "(?<=") ||
                StartsWith(pattern, index, "(?<!"))
            {
                return "lookbehind/lookaround";
            }

            if (StartsWith(pattern, index, "(?>"))
                return "atomic groups";

            if (StartsWith(pattern, index, "(?("))
                return "conditionals";

            if (StartsWith(pattern, index, "(?<"))
            {
                var nameEnd = pattern.IndexOf('>', index + 3);
                if (nameEnd > index &&
                    pattern.AsSpan(index + 3, nameEnd - index - 3).IndexOf('-') >= 0)
                {
                    return "balancing groups";
                }
            }
        }

        return null;
    }

    private static bool StartsWith(string value, int start, string token)
    {
        return start <= value.Length - token.Length &&
               value.AsSpan(start, token.Length).SequenceEqual(token.AsSpan());
    }

    private readonly record struct RegexCacheKey(
        string Pattern,
        SearchCaseMode CaseMode,
        bool WholeWord);

    private sealed record RegexCacheEntry(RegexCacheKey Key, Regex Regex);
}
