#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchProximityRecipeTests
{
    private const string ProximityRequest =
        "{\"version\":1,\"patterns\":[" +
        "{\"id\":\"left\",\"pattern\":\"LEFT\",\"mode\":\"literal\"}," +
        "{\"id\":\"right\",\"pattern\":\"RIGHT\",\"mode\":\"literal\"}]}";

    [TestMethod]
    public void ProximityRecipe_ShouldBoundPairsAndKeepFileIdentity()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "alpha.txt", "LEFT RIGHT RIGHT\nLEFT\nRIGHT\nLEFT RIGHT\n");
            Write(root, "a-boundary.txt", "LEFT\n");
            Write(root, "b-boundary.txt", "RIGHT\n");
            Write(root, "right-only.txt", "RIGHT\n");

            var occurrences = ReadOccurrences(root);
            var allPairs = FindPairs(
                occurrences,
                ProximityMode.AllPairs,
                new ProximityWindow(MaxFollowingLines: 2, MaxSameLineGap: 7, MaxPairsPerLeft: 3));
            var alphaPairs = allPairs
                .Where(static pair => pair.Left.Path == "alpha.txt")
                .Select(static pair =>
                    (pair.Left.LineNumber, pair.Right.LineNumber, pair.Right.Utf16Column, pair.LineDistance,
                        pair.SameLineGap))
                .ToArray();

            Assert.AreEqual(6, alphaPairs.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    (1L, 1L, 5L, 0L, (long?)1L),
                    (1L, 1L, 11L, 0L, (long?)7L),
                    (1L, 3L, 0L, 2L, (long?)null),
                    (2L, 3L, 0L, 1L, (long?)null),
                    (2L, 4L, 5L, 2L, (long?)null),
                    (4L, 4L, 5L, 0L, (long?)1L)
                },
                alphaPairs);
            Assert.IsFalse(allPairs.Any(static pair =>
                pair.Left.Path == "a-boundary.txt" && pair.Right.Path == "b-boundary.txt"));
            Assert.IsFalse(allPairs.Any(static pair => pair.Left.Path == "right-only.txt"));

            var nearest = FindPairs(
                occurrences,
                ProximityMode.NearestRight,
                new ProximityWindow(MaxFollowingLines: 2, MaxSameLineGap: 7, MaxPairsPerLeft: 1));
            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", 1L, 5L),
                    ("alpha.txt", 2L, 0L),
                    ("alpha.txt", 4L, 5L)
                },
                nearest.Select(static pair => (pair.Left.Path, pair.Left.LineNumber, pair.Right.Utf16Column)).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ProximityRecipe_ShouldRejectUnboundedAllPairsAndInvalidWindows()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "fixture.txt", "LEFT RIGHT RIGHT\n");
            var occurrences = ReadOccurrences(root);

            Assert.ThrowsException<InvalidOperationException>(() =>
                FindPairs(
                    occurrences,
                    ProximityMode.AllPairs,
                    new ProximityWindow(MaxFollowingLines: 0, MaxSameLineGap: 7, MaxPairsPerLeft: 1)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                FindPairs(
                    occurrences,
                    ProximityMode.NearestRight,
                    new ProximityWindow(MaxFollowingLines: -1, MaxSameLineGap: 7, MaxPairsPerLeft: 1)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                FindPairs(
                    occurrences,
                    ProximityMode.NearestRight,
                    new ProximityWindow(MaxFollowingLines: 0, MaxSameLineGap: -1, MaxPairsPerLeft: 1)));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static IReadOnlyList<ProximityPair> FindPairs(
        IReadOnlyList<ProximityOccurrence> occurrences,
        ProximityMode mode,
        ProximityWindow window)
    {
        if (window.MaxFollowingLines < 0)
            throw new ArgumentOutOfRangeException(nameof(window.MaxFollowingLines));
        if (window.MaxSameLineGap < 0)
            throw new ArgumentOutOfRangeException(nameof(window.MaxSameLineGap));
        if (window.MaxPairsPerLeft < 1)
            throw new ArgumentOutOfRangeException(nameof(window.MaxPairsPerLeft));

        var lefts = occurrences
            .Where(static occurrence => occurrence.PatternId == "left")
            .OrderBy(static occurrence => occurrence.Path, StringComparer.Ordinal)
            .ThenBy(static occurrence => occurrence.LineNumber)
            .ThenBy(static occurrence => occurrence.Utf16Column)
            .ThenBy(static occurrence => occurrence.MatchIndex)
            .ToArray();
        var rights = occurrences
            .Where(static occurrence => occurrence.PatternId == "right")
            .ToArray();
        var pairs = new List<ProximityPair>();

        foreach (var left in lefts)
        {
            var candidates = rights
                .Where(right => IsWithinWindow(left, right, window))
                .OrderBy(static occurrence => occurrence.Path, StringComparer.Ordinal)
                .ThenBy(static occurrence => occurrence.LineNumber)
                .ThenBy(static occurrence => occurrence.Utf16Column)
                .ThenBy(static occurrence => occurrence.MatchIndex)
                .ToArray();

            if (mode == ProximityMode.AllPairs && candidates.Length > window.MaxPairsPerLeft)
            {
                throw new InvalidOperationException(
                    $"The all-pairs proximity cap of {window.MaxPairsPerLeft} was exceeded for '{left.Path}'.");
            }

            foreach (var right in mode == ProximityMode.NearestRight
                         ? candidates.Take(1)
                         : candidates)
            {
                pairs.Add(new ProximityPair(
                    left,
                    right,
                    checked(right.LineNumber - left.LineNumber),
                    right.LineNumber == left.LineNumber
                        ? checked(right.Utf16Column - (left.Utf16Column + left.Utf16Length))
                        : null));
            }
        }

        return pairs;
    }

    private static bool IsWithinWindow(
        ProximityOccurrence left,
        ProximityOccurrence right,
        ProximityWindow window)
    {
        if (!string.Equals(left.Path, right.Path, StringComparison.Ordinal) ||
            !string.Equals(right.PatternId, "right", StringComparison.Ordinal))
        {
            return false;
        }

        var lineDistance = right.LineNumber - left.LineNumber;
        if (lineDistance < 0 || lineDistance > window.MaxFollowingLines)
            return false;

        if (lineDistance != 0)
            return true;

        var gap = right.Utf16Column - (left.Utf16Column + left.Utf16Length);
        return gap >= 0 && gap <= window.MaxSameLineGap;
    }

    private static ProximityOccurrence[] ReadOccurrences(string root)
    {
        var escapedRoot = EscapeSql(root);
        var escapedRequest = EscapeSql(ProximityRequest);
        var result = Compile(
                $"select m.Path, m.PatternId, m.MatchIndex, m.LineNumber, " +
                $"m.Utf16Column, m.Utf16Length, m.MatchText " +
                $"from search.many('{escapedRoot}', '{escapedRequest}') m " +
                "order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchIndex")
            .Run();

        return result.Rows
            .Select(row => new ProximityOccurrence(
                NormalizePath((string)row[0]),
                (string)row[1],
                (long)row[2],
                (long)row[3],
                (long)row[4],
                (long)row[5],
                (string?)row[6]))
            .ToArray();
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-proximity-{Guid.NewGuid():N}");
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private enum ProximityMode
    {
        AllPairs,
        NearestRight
    }

    private sealed record ProximityWindow(
        int MaxFollowingLines,
        long MaxSameLineGap,
        int MaxPairsPerLeft);

    private sealed record ProximityOccurrence(
        string Path,
        string PatternId,
        long MatchIndex,
        long LineNumber,
        long Utf16Column,
        long Utf16Length,
        string? MatchText);

    private sealed record ProximityPair(
        ProximityOccurrence Left,
        ProximityOccurrence Right,
        long LineDistance,
        long? SameLineGap);
}
