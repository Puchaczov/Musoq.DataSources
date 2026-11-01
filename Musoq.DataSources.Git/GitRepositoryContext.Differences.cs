using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal sealed partial class GitRepositoryContext
{
    private static string[] DiffArguments(string format, string first, string second) =>
        ["diff", format, "--no-ext-diff", "--no-textconv", "--find-renames=50%", first, second, "--"];

    public IEnumerable<DifferenceEntity> Differences(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) yield break;
        var left = OptionalText("rev-parse", "--verify", "--quiet", "--end-of-options", first + "^{commit}")?.Trim();
        var right = OptionalText("rev-parse", "--verify", "--quiet", "--end-of-options", second + "^{commit}")?.Trim();
        if (left is null || right is null) yield break;
        var args = DiffArguments("--raw", left, right).ToList();
        args.InsertRange(2, ["-z", "--no-abbrev"]);
        using var fields = Tokens(args.ToArray()).GetEnumerator();
        while (fields.MoveNext())
        {
            var header = fields.Current.Split(' ');
            if (header.Length != 5 || !header[0].StartsWith(':')) throw new InvalidDataException("Invalid raw Git difference header.");
            var oldPath = Required(fields);
            var state = header[4][0];
            var path = state is 'R' or 'C' ? Required(fields) : oldPath;
            var kind = state switch
            {
                'A' => "Added", 'D' => "Deleted", 'M' => "Modified", 'R' => "Renamed", 'C' => "Copied",
                'T' => "TypeChanged", 'U' => "Unmerged", _ => throw new InvalidDataException("Unsupported raw Git difference kind.")
            };
            yield return new DifferenceEntity(Location.GitDirectory, path, state != 'D', kind, oldPath,
                ((Mode)Convert.ToInt32(header[0][1..], 8)).ToString(), ((Mode)Convert.ToInt32(header[1], 8)).ToString(),
                header[2], header[3], state == 'D') { ReadContext = this };
        }
    }

    internal (int Added, int Deleted, string Content, PatchEntryChangesEntity[] Changes) PatchData(string first, string second)
    {
        var changes = Differences(first, second).ToArray();
        var stats = NumericDifferences(first, second).ToDictionary(s => s.Path, StringComparer.Ordinal);
        var content = Text(DiffArguments("--patch", first, second));
        var blocks = content.Length == 0 ? [] : content.Split("\ndiff --git ", StringSplitOptions.None);
        var entries = new PatchEntryChangesEntity[changes.Length];
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            var stat = stats.GetValueOrDefault(change.Path);
            var patch = index < blocks.Length ? (index == 0 ? blocks[index] : "diff --git " + blocks[index]) : string.Empty;
            if (index + 1 < blocks.Length) patch += "\n";
            entries[index] = new PatchEntryChangesEntity(stat.Added, stat.Deleted, patch, change.Path,
                change.OldMode, change.NewMode, stat.Binary);
        }
        return (stats.Values.Sum(s => s.Added), stats.Values.Sum(s => s.Deleted), content, entries);
    }

    internal (int Added, int Deleted) PatchStatistics(string first, string second)
    {
        int added = 0, deleted = 0;
        foreach (var stat in NumericDifferences(first, second)) { added += stat.Added; deleted += stat.Deleted; }
        return (added, deleted);
    }

    private IEnumerable<(string Path, int Added, int Deleted, bool Binary)> NumericDifferences(string first, string second)
    {
        var arguments = DiffArguments("--numstat", first, second).ToList();
        arguments.Insert(2, "-z");
        using (var fields = Tokens(arguments.ToArray()).GetEnumerator())
        {
            while (fields.MoveNext())
            {
                var parts = fields.Current.Split('\t', 3);
                if (parts.Length != 3) throw new InvalidDataException("Invalid Git numeric difference record.");
                var path = parts[2];
                if (path.Length == 0) { _ = Required(fields); path = Required(fields); }
                var binary = parts[0] == "-";
                yield return (path, binary ? 0 : int.Parse(parts[0], CultureInfo.InvariantCulture),
                    binary ? 0 : int.Parse(parts[1], CultureInfo.InvariantCulture), binary);
            }
        }
    }

    public BranchEntity? ParentBranch(BranchEntity branch)
    {
        if (branch.TipSha is not { } tip) return null;
        BranchEntity? best = null;
        var bestCount = long.MaxValue;
        foreach (var candidate in Branches().Where(b => !b.IsRemote && b.TipSha is not null &&
                     b.FriendlyName != branch.FriendlyName && !b.FriendlyName.StartsWith("origin/", StringComparison.Ordinal))
                     .OrderBy(b => b.CanonicalName, StringComparer.Ordinal))
        {
            var merge = OptionalText("merge-base", tip, candidate.TipSha!)?.Trim();
            if (merge is null || merge == tip) continue;
            var count = long.Parse(Text("rev-list", "--count", tip, "^" + merge, "--"), CultureInfo.InvariantCulture);
            if (count >= bestCount) continue;
            bestCount = count; best = candidate;
        }
        return best;
    }

    public MergeBaseEntity? MergeBase(BranchEntity branch)
    {
        var parent = branch.ParentBranch;
        if (parent?.TipSha is not { } parentTip || branch.TipSha is not { } tip) return null;
        var merge = OptionalText("merge-base", tip, parentTip)?.Trim();
        var commit = CommitFrom(merge);
        return commit is null ? null : new MergeBaseEntity(commit, branch, parent, null!);
    }

    public IEnumerable<CommitEntity> BranchSpecificCommits(BranchEntity branch, bool excludeMergeBase)
    {
        var merge = MergeBase(branch);
        if (merge?.MergeBaseCommit.Sha is not { } sha) return [];
        return Commits(branch.CanonicalName, exclude: excludeMergeBase ? sha : merge.MergeBaseCommit.ParentShas?.FirstOrDefault(), topological: true);
    }
}
