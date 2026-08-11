using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Benchmarks;

public enum GitBenchmarkProfile
{
    Smoke,
    Verify,
    Reference
}

public sealed record GitBenchmarkCorpus(
    string RepositoryPath,
    GitBenchmarkProfile Profile,
    int CommitCount,
    int WorkingTreePathCount,
    bool HasCommitGraph,
    string Fingerprint,
    int ExpectedFileHistoryChangeCount,
    string ExpectedFileHistoryChecksum);

internal static class GitBenchmarkCorpusFactory
{
    private const string CorpusVersion = "v12";
    private const int InitialTimestamp = 1_700_000_000;

    public static GitBenchmarkCorpus Ensure(GitBenchmarkProfile profile, bool withCommitGraph = true)
    {
        var specification = GitCorpusSpecification.For(profile);
        var root = Path.Combine(
            Path.GetTempPath(),
            "musoq-git-benchmarks",
            $"{CorpusVersion}-{profile.ToString().ToLowerInvariant()}-{(withCommitGraph ? "graph" : "no-graph")}");
        var manifestPath = Path.Combine(root, "benchmark-manifest.txt");
        var expectedChangesPath = Path.Combine(root, "expected-filehistory.tsv");

        if (!File.Exists(manifestPath) || !File.Exists(expectedChangesPath))
            Create(root, manifestPath, specification, withCommitGraph);

        var manifest = File.ReadAllText(manifestPath, Encoding.UTF8).Trim();
        var expectedManifest = CreateManifest(specification, withCommitGraph);
        if (!string.Equals(manifest, expectedManifest, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Benchmark corpus '{root}' has an incompatible manifest. Delete only that corpus directory and rerun.");

        var expectedChanges = File.ReadAllLines(expectedChangesPath, Encoding.UTF8);
        var expectedChecksum = GitBenchmarkOracle.Checksum(expectedChanges);
        return new GitBenchmarkCorpus(
            root,
            profile,
            specification.CommitCount,
            specification.WorkingTreePathCount,
            withCommitGraph,
            Fingerprint(root, manifest),
            expectedChanges.Length,
            expectedChecksum);
    }

    public static string Describe(GitBenchmarkCorpus corpus)
    {
        var gitVersion = RunGit(corpus.RepositoryPath, ["--version"]);
        var count = RunGit(corpus.RepositoryPath, ["rev-list", "--count", "HEAD"]);
        var objectStats = RunGit(corpus.RepositoryPath, ["count-objects", "-vH"])
            .Replace(Environment.NewLine, "; ", StringComparison.Ordinal);

        return $"profile={corpus.Profile}; commits={count}; paths={corpus.WorkingTreePathCount}; " +
               $"commitGraph={corpus.HasCommitGraph}; fingerprint={corpus.Fingerprint}; " +
               $"expectedChanges={corpus.ExpectedFileHistoryChangeCount}; expectedChecksum={corpus.ExpectedFileHistoryChecksum}; " +
               $"git={gitVersion}; objects={objectStats}";
    }

    private static void Create(
        string root,
        string manifestPath,
        GitCorpusSpecification specification,
        bool withCommitGraph)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidDataException(
                $"Benchmark corpus directory '{root}' is incomplete. Delete only that directory before recreating it.");

        Directory.CreateDirectory(root);
        RunGit(root, ["init", "--quiet", "--initial-branch=main"]);
        var expectedChanges = new List<string>();

        using (var importer = StartGit(root, ["fast-import", "--quiet"], redirectInput: true))
        {
            using (var input = new StreamWriter(importer.StandardInput.BaseStream, new UTF8Encoding(false), 4096, leaveOpen: false)
            {
                NewLine = "\n"
            })
            {
                WriteHistory(input, specification, expectedChanges);
                input.Flush();
            }

            var error = importer.StandardError.ReadToEnd();
            importer.WaitForExit();

            if (importer.ExitCode != 0)
                throw new InvalidOperationException($"git fast-import failed: {error}");
        }

        RunGit(root, ["reset", "--hard", "--quiet", "main"]);
        CreateLargeWorkingTree(root, specification);

        if (withCommitGraph)
            RunGit(root, ["commit-graph", "write", "--reachable", "--changed-paths"]);

        File.WriteAllText(manifestPath, CreateManifest(specification, withCommitGraph), new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(root, "expected-filehistory.tsv"), expectedChanges, new UTF8Encoding(false));
    }

    private static void WriteHistory(
        StreamWriter writer,
        GitCorpusSpecification specification,
        List<string> expectedChanges)
    {
        var nextMark = 1;
        var mainPaths = new HashSet<string>(StringComparer.Ordinal);
        var initialContentMark = nextMark++;
        WriteBlob(writer, initialContentMark, "initial\n");
        // Keep the two paths which participate in every rename and deletion disjoint from the generic initial blob.
        // Otherwise Git's content-based rename detector can legitimately pair a deleted fixture file with a rename
        // destination, making a manifest derived from the fast-import operations ambiguous.
        var renameContentMark = nextMark++;
        WriteBlob(writer, renameContentMark, "rename chain\n");
        var deletedContentMark = nextMark++;
        WriteBlob(writer, deletedContentMark, "deletion pool\n");
        var attributesMark = nextMark++;
        WriteBlob(writer, attributesMark, "*.bin filter=lfs diff=lfs merge=lfs -text\n");
        var ignoreMark = nextMark++;
        WriteBlob(writer, ignoreMark, ".ignored/\n");
        var lfsPointerMark = nextMark++;
        WriteBlob(writer, lfsPointerMark,
            "version https://git-lfs.github.com/spec/v1\noid sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\nsize 1048576\n");

        var currentMainMark = nextMark++;
        var initialOperations = InitialOperations(
            specification,
            initialContentMark,
            renameContentMark,
            deletedContentMark,
            attributesMark,
            ignoreMark,
            lfsPointerMark).ToArray();
        WriteCommit(
            writer,
            "refs/heads/main",
            currentMainMark,
            parentMark: null,
            mergeMark: null,
            timestamp: InitialTimestamp,
            message: "initial benchmark history",
            operations: initialOperations);
        RecordExpectedChanges(expectedChanges, InitialTimestamp, initialOperations, mainPaths, includeEntry: true);

        var mainCommitCount = CalculateMainCommitCount(specification.CommitCount, specification.MergeEvery);
        for (var index = 1; index <= mainCommitCount; index++)
        {
            int? mergeMark = null;
            if (index % specification.MergeEvery == 0)
            {
                var sideContentMark = nextMark++;
                WriteBlob(writer, sideContentMark, $"side {index}\n");
                var sideCommitMark = nextMark++;
                var sideOperations = new[] { $"M 100644 :{sideContentMark} side/Side-{index:D6}.cs" };
                WriteCommit(
                    writer,
                    "refs/heads/benchmark-side",
                    sideCommitMark,
                    currentMainMark,
                    mergeMark: null,
                    InitialTimestamp + index * 2,
                    $"side {index}",
                    sideOperations);
                RecordExpectedChanges(
                    expectedChanges,
                    InitialTimestamp + index * 2,
                    sideOperations,
                    new HashSet<string>(mainPaths, StringComparer.Ordinal),
                    includeEntry: true);
                mergeMark = sideCommitMark;
            }

            var contentMark = nextMark++;
            WriteBlob(writer, contentMark, $"commit {index}\n");
            var commitMark = nextMark++;
            var operations = OperationsFor(index, specification, contentMark, lfsPointerMark).ToArray();
            WriteCommit(
                writer,
                "refs/heads/main",
                commitMark,
                currentMainMark,
                mergeMark,
                InitialTimestamp + index * 2 + 1,
                $"benchmark {index}",
                operations);
            // The state advances for every first-parent commit, but filehistory excludes merge commits themselves.
            RecordExpectedChanges(
                expectedChanges,
                InitialTimestamp + index * 2 + 1,
                operations,
                mainPaths,
                includeEntry: mergeMark is null);
            currentMainMark = commitMark;
        }
    }

    private static IEnumerable<string> InitialOperations(
        GitCorpusSpecification specification,
        int initialContentMark,
        int renameContentMark,
        int deletedContentMark,
        int attributesMark,
        int ignoreMark,
        int lfsPointerMark)
    {
        yield return $"M 100644 :{attributesMark} .gitattributes";
        yield return $"M 100644 :{ignoreMark} .gitignore";
        yield return $"M 100644 :{lfsPointerMark} assets/lfs-pointer.bin";
        yield return $"M 100644 :{renameContentMark} legacy/Renamed-000000.cs";
        yield return $"M 100644 :{initialContentMark} unicode/żółw.cs";

        for (var path = 0; path < specification.WorkingTreePathCount; path++)
        {
            var extension = path % 3 == 0 ? "cs" : "txt";
            yield return $"M 100644 :{initialContentMark} {TrackedPath(path, extension)}";
        }

        for (var path = 0; path < specification.DeletedPathCount; path++)
            yield return $"M 100644 :{deletedContentMark} deleted/Deleted-{path:D5}.cs";
    }

    private static IEnumerable<string> OperationsFor(
        int index,
        GitCorpusSpecification specification,
        int contentMark,
        int lfsPointerMark)
    {
        yield return $"M 100644 :{contentMark} {TrackedPath(index % specification.WorkingTreePathCount, "cs")}";

        if (index % specification.RenameEvery == 0)
        {
            var previous = index == specification.RenameEvery
                ? "legacy/Renamed-000000.cs"
                : $"renamed/Renamed-{index - specification.RenameEvery:D6}.cs";
            yield return $"R {previous} renamed/Renamed-{index:D6}.cs";
        }

        if (index % specification.CopyEvery == 0)
            yield return $"C {TrackedPath(index % specification.WorkingTreePathCount, "cs")} copies/Copy-{index:D6}.cs";

        if (index % specification.DeleteEvery == 0)
        {
            var deletedPath = (index / specification.DeleteEvery - 1) % specification.DeletedPathCount;
            yield return $"D deleted/Deleted-{deletedPath:D5}.cs";
        }

        if (index % specification.LfsEvery == 0)
            yield return $"M 100644 :{lfsPointerMark} assets/lfs-pointer.bin";
    }

    private static void WriteBlob(StreamWriter writer, int mark, string content)
    {
        writer.WriteLine("blob");
        writer.WriteLine($"mark :{mark}");
        WriteData(writer, content);
    }

    private static void WriteCommit(
        StreamWriter writer,
        string reference,
        int mark,
        int? parentMark,
        int? mergeMark,
        int timestamp,
        string message,
        IEnumerable<string> operations)
    {
        writer.WriteLine($"commit {reference}");
        writer.WriteLine($"mark :{mark}");
        writer.WriteLine($"author Benchmark <benchmark@musoq.dev> {timestamp} +0000");
        writer.WriteLine($"committer Benchmark <benchmark@musoq.dev> {timestamp} +0000");
        WriteData(writer, message);

        if (parentMark.HasValue)
            writer.WriteLine($"from :{parentMark.Value}");
        if (mergeMark.HasValue)
            writer.WriteLine($"merge :{mergeMark.Value}");

        foreach (var operation in operations)
            writer.WriteLine(operation);

        writer.WriteLine();
    }

    private static void WriteData(StreamWriter writer, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        writer.WriteLine($"data {bytes.Length}");
        writer.Flush();
        writer.BaseStream.Write(bytes);
        writer.BaseStream.WriteByte((byte)'\n');
    }

    private static void CreateLargeWorkingTree(string root, GitCorpusSpecification specification)
    {
        for (var index = 0; index < specification.IgnoredPathCount; index++)
        {
            var path = Path.Combine(root, ".ignored", $"ignored-{index:D6}.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "ignored", new UTF8Encoding(false));
        }

        for (var index = 0; index < specification.UntrackedPathCount; index++)
        {
            var path = Path.Combine(root, "untracked", $"untracked-{index:D6}.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "untracked", new UTF8Encoding(false));
        }
    }

    private static int CalculateMainCommitCount(int totalCommitCount, int mergeEvery)
    {
        for (var main = totalCommitCount - 1; main >= 0; main--)
            if (1 + main + main / mergeEvery == totalCommitCount)
                return main;

        throw new InvalidOperationException("Unable to derive the requested deterministic commit count.");
    }

    private static string TrackedPath(int index, string extension) =>
        $"src/partition-{index % 64:D2}/File-{index:D6}.{extension}";

    private static void RecordExpectedChanges(
        List<string> expectedChanges,
        int timestamp,
        IEnumerable<string> operations,
        ISet<string> paths,
        bool includeEntry)
    {
        foreach (var operation in operations)
        {
            var parts = operation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidDataException($"Unsupported benchmark fast-import operation '{operation}'.");

            switch (parts[0])
            {
                case "M":
                    if (parts.Length < 4)
                        throw new InvalidDataException($"Unsupported benchmark fast-import operation '{operation}'.");
                    var modifiedPath = parts[^1];
                    var modifiedStatus = paths.Add(modifiedPath) ? "Added" : "Modified";
                    if (includeEntry)
                        expectedChanges.Add($"{timestamp}\t{modifiedStatus}\t{modifiedPath}\t");
                    break;
                case "D":
                    paths.Remove(parts[1]);
                    if (includeEntry)
                        expectedChanges.Add($"{timestamp}\tDeleted\t{parts[1]}\t");
                    break;
                case "R":
                case "C":
                    if (parts.Length < 3)
                        throw new InvalidDataException($"Unsupported benchmark fast-import operation '{operation}'.");
                    if (parts[0] == "R")
                        paths.Remove(parts[1]);
                    paths.Add(parts[2]);
                    if (includeEntry)
                        expectedChanges.Add($"{timestamp}\t{(parts[0] == "R" ? "Renamed" : "Copied|Added")}\t{parts[2]}\t{parts[1]}");
                    break;
                default:
                    throw new InvalidDataException($"Unsupported benchmark fast-import operation '{operation}'.");
            }
        }
    }

    private static string CreateManifest(GitCorpusSpecification specification, bool withCommitGraph) =>
        $"{CorpusVersion}|{specification}|commitGraph={withCommitGraph}";

    private static string Fingerprint(string repositoryPath, string manifest)
    {
        var head = RunGit(repositoryPath, ["rev-parse", "HEAD"]);
        var payload = Encoding.UTF8.GetBytes($"{manifest}\n{head}\n");
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static string RunGit(string workingDirectory, IReadOnlyList<string> arguments)
    {
        using var process = StartGit(workingDirectory, arguments, redirectInput: false);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");

        return output.Trim();
    }

    private static Process StartGit(string workingDirectory, IReadOnlyList<string> arguments, bool redirectInput)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // The corpus deliberately contains a canonical LFS pointer. Fixture creation must never let attributes launch
        // an installed git-lfs filter process while checking out that pointer.
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.process=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.smudge=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.required=false");
        startInfo.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
    }

    private sealed record GitCorpusSpecification(
        int CommitCount,
        int WorkingTreePathCount,
        int IgnoredPathCount,
        int UntrackedPathCount,
        int DeletedPathCount,
        int RenameEvery,
        int CopyEvery,
        int DeleteEvery,
        int LfsEvery,
        int MergeEvery)
    {
        public static GitCorpusSpecification For(GitBenchmarkProfile profile) => profile switch
        {
            GitBenchmarkProfile.Smoke => new(400, 600, 100, 100, 16, 40, 50, 25, 20, 100),
            GitBenchmarkProfile.Verify => new(10_000, 10_000, 2_000, 2_000, 128, 100, 125, 80, 50, 500),
            GitBenchmarkProfile.Reference => new(200_000, 100_000, 20_000, 20_000, 2_048, 250, 300, 160, 100, 5_000),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }
}

/// <summary>
/// Validates filehistory output against the deterministic fast-import input, rather than against production or a
/// legacy reader. Each tuple is unique because corpus commit timestamps are unique.
/// </summary>
internal static class GitBenchmarkOracle
{
    public static string Checksum(IEnumerable<string> lines)
    {
        long checksum = 17;
        foreach (var line in lines)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, line);
        return checksum.ToString(CultureInfo.InvariantCulture);
    }

    public static void AssertExpectedRows(GitBenchmarkCorpus corpus, IReadOnlyCollection<FileHistoryEntity> rows, int expectedCount)
    {
        if (rows.Count != expectedCount)
            throw new InvalidDataException($"Expected {expectedCount} filehistory rows, but received {rows.Count}.");

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(Path.Combine(corpus.RepositoryPath, "expected-filehistory.tsv"), Encoding.UTF8))
        {
            var fields = line.Split('\t');
            foreach (var status in fields[1].Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var oldPath = status == "Added" && fields[1].Contains('|') ? string.Empty : fields[3];
                expected.Add($"{fields[0]}\t{status}\t{fields[2]}\t{oldPath}");
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var actual = $"{row.CommittedWhen.ToUnixTimeSeconds()}\t{row.ChangeType}\t{row.FilePath}\t{row.OldPath ?? string.Empty}";
            if (!expected.Contains(actual))
                throw new InvalidDataException($"Filehistory row was absent from the seeded oracle: '{actual}'.");
            if (!seen.Add(actual))
                throw new InvalidDataException($"Filehistory emitted a duplicate seeded change: '{actual}'.");
        }
    }

    public static int ExpectedCsRowCount(GitBenchmarkCorpus corpus)
    {
        return File.ReadLines(Path.Combine(corpus.RepositoryPath, "expected-filehistory.tsv"), Encoding.UTF8)
            .Count(static line =>
            {
                var fields = line.Split('\t');
                return fields.Length >= 4 &&
                       (fields[2].EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                        fields[3].EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            });
    }
}
