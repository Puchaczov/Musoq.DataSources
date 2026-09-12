using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Benchmarks;

public enum GitReferenceCorpusProfile
{
    Smoke,
    Verify,
    Scale
}

internal sealed record GitReferenceBenchmarkCorpus(
    string Root,
    string RepositoryPath,
    string RemotePath,
    string ClientPath,
    string RemoteAdvertisementPath,
    GitReferenceCorpusProfile Profile,
    bool Packed,
    long TagCount,
    long AnnotatedTagCount,
    long StashCount,
    string Fingerprint,
    string ExpectedTagChecksum,
    string ExpectedStashChecksum,
    string ExpectedRemoteChecksum);

internal static class GitReferenceBenchmarkCorpusFactory
{
    private const string CorpusVersion = "references-v9";
    private const int TargetCommitCount = 64;
    private const int InitialTimestamp = 1_750_000_000;

    public static GitReferenceBenchmarkCorpus Ensure(GitReferenceCorpusProfile profile, bool packed)
    {
        var specification = GitReferenceCorpusSpecification.For(profile);
        var root = Path.Combine(
            Path.GetTempPath(),
            "musoq-git-reference-benchmarks",
            CorpusVersion,
            profile.ToString().ToLowerInvariant(),
            packed ? "packed" : "loose");
        var manifestPath = Path.Combine(root, "reference-manifest.txt");
        var expectedPath = Path.Combine(root, "reference-expected.txt");

        if (!File.Exists(manifestPath) || !File.Exists(expectedPath))
            Create(root, manifestPath, expectedPath, specification, profile, packed);

        var manifest = File.ReadAllText(manifestPath, Encoding.UTF8).Trim();
        var expectedManifest = CreateManifest(specification, profile, packed);
        if (!string.Equals(manifest, expectedManifest, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Reference benchmark corpus '{root}' has an incompatible manifest. Delete only that corpus directory and rerun.");

        var expected = ReadExpected(expectedPath);
        var repositoryPath = Path.Combine(root, "repository");
        var remotePath = Path.Combine(root, "remote.git");
        var clientPath = Path.Combine(root, "client");
        var advertisementPath = Path.Combine(root, "remote-advertisement.txt");
        ValidateLayout(repositoryPath, remotePath, clientPath);
        if (!File.Exists(advertisementPath))
            throw new InvalidDataException($"Reference benchmark corpus is missing its captured remote advertisement: '{advertisementPath}'.");
        return new GitReferenceBenchmarkCorpus(
            root,
            repositoryPath,
            remotePath,
            clientPath,
            advertisementPath,
            profile,
            packed,
            ParseLong(expected, "tagCount"),
            ParseLong(expected, "annotatedTagCount"),
            ParseLong(expected, "stashCount"),
            Fingerprint(manifest, expectedPath, advertisementPath),
            expected["tagChecksum"],
            expected["stashChecksum"],
            expected["remoteChecksum"]);
    }

    public static string Describe(GitReferenceBenchmarkCorpus corpus)
    {
        var gitVersion = RunGit(corpus.RepositoryPath, ["--version"]);
        return $"profile={corpus.Profile}; packed={corpus.Packed}; tags={corpus.TagCount}; " +
               $"annotated={corpus.AnnotatedTagCount}; stashes={corpus.StashCount}; " +
               $"fingerprint={corpus.Fingerprint}; git={gitVersion}; root={corpus.Root}";
    }

    public static void Verify(GitReferenceBenchmarkCorpus corpus)
    {
        var beforeRefs = RunGit(corpus.ClientPath, ["for-each-ref", "--format=%(refname)"]);
        var beforeObjects = RunGit(corpus.ClientPath, ["count-objects", "-v"]);
        if (!string.IsNullOrWhiteSpace(beforeRefs))
            throw new InvalidDataException("The benchmark client unexpectedly contains local references before ls-remote.");

        var tagChecksum = ReadLocalTags(corpus.RepositoryPath, out var tagCount);
        if (tagCount != corpus.TagCount ||
            tagChecksum.ToString(CultureInfo.InvariantCulture) != corpus.ExpectedTagChecksum)
            throw new InvalidDataException(
                $"Reference tag verification failed for {corpus.Root}: expected {corpus.TagCount}/{corpus.ExpectedTagChecksum}, " +
                $"received {tagCount}/{tagChecksum}.");

        var stashChecksum = ReadStashes(corpus.RepositoryPath, out var stashCount);
        if (stashCount != corpus.StashCount ||
            stashChecksum.ToString(CultureInfo.InvariantCulture) != corpus.ExpectedStashChecksum)
            throw new InvalidDataException(
                $"Reference stash verification failed for {corpus.Root}: expected {corpus.StashCount}/{corpus.ExpectedStashChecksum}, " +
                $"received {stashCount}/{stashChecksum}.");

        var remoteChecksum = ReadRemoteTags(corpus.ClientPath, out var remoteCount);
        if (remoteCount != corpus.TagCount ||
            remoteChecksum.ToString(CultureInfo.InvariantCulture) != corpus.ExpectedRemoteChecksum)
            throw new InvalidDataException(
                $"Reference remote-tag verification failed for {corpus.Root}: expected {corpus.TagCount}/{corpus.ExpectedRemoteChecksum}, " +
                $"received {remoteCount}/{remoteChecksum}.");

        var afterRefs = RunGit(corpus.ClientPath, ["for-each-ref", "--format=%(refname)"]);
        var afterObjects = RunGit(corpus.ClientPath, ["count-objects", "-v"]);
        if (!string.Equals(beforeRefs, afterRefs, StringComparison.Ordinal) ||
            !string.Equals(beforeObjects, afterObjects, StringComparison.Ordinal))
            throw new InvalidDataException("Remote-tag verification mutated client refs or objects.");

        GitReferenceBenchmarkVerifier.VerifyStreamingInvariants(corpus);

        Console.WriteLine(
            $"reference-verified={corpus.Profile}; packed={corpus.Packed}; tags={tagCount}; stashes={stashCount}; " +
            $"tagChecksum={tagChecksum}; stashChecksum={stashChecksum}; remoteChecksum={remoteChecksum}; " +
            $"fingerprint={corpus.Fingerprint}");
    }

    private static long ParseLong(IReadOnlyDictionary<string, string> values, string key) =>
        long.TryParse(values[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"Reference corpus expected value '{key}' is not an integer.");

    private static long ReadLocalTags(string repositoryPath, out long count)
    {
        long localCount = 0;
        long checksum = 17;
        foreach (var tag in GitOperationReaders.CliTags.ReadStreaming(
                     repositoryPath,
                     GitReferenceBackendOptions.Default,
                     new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
                     GitTagReadQuery.Empty,
                     static path => new Repository(path),
                     CancellationToken.None))
        {
            localCount++;
            checksum = GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName);
        }
        count = localCount;
        return checksum;
    }

    private static long ReadStashes(string repositoryPath, out long count)
    {
        long localCount = 0;
        long checksum = 17;
        foreach (var stash in GitOperationReaders.CliStashes.ReadStreaming(
                     repositoryPath,
                     GitReferenceBackendOptions.Default,
                     GitProjection.NotAccepted,
                     GitStashReadQuery.Empty,
                     static path => new Repository(path),
                     CancellationToken.None))
        {
            localCount++;
            checksum = GitFileHistoryBenchmarks.Fold(
                checksum,
                stash.Selector,
                stash.Sha,
                stash.Message,
                stash.IndexSha,
                stash.WorkTreeSha,
                stash.UntrackedFilesSha);
        }
        count = localCount;
        return checksum;
    }

    private static long ReadRemoteTags(string clientPath, out long count)
    {
        long localCount = 0;
        long checksum = 17;
        GitOperationReaders.RemoteTags.Read(
            clientPath,
            "origin",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [
                nameof(RemoteTagEntity.FriendlyName),
                nameof(RemoteTagEntity.CanonicalName),
                nameof(RemoteTagEntity.ObjectSha)]),
            GitRemoteTagReadQuery.Empty,
            static path => new Repository(path),
            CancellationToken.None,
            tag =>
            {
                localCount++;
                checksum = GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName, tag.ObjectSha);
                return true;
            });
        count = localCount;
        return checksum;
    }

    private static void Create(
        string root,
        string manifestPath,
        string expectedPath,
        GitReferenceCorpusSpecification specification,
        GitReferenceCorpusProfile profile,
        bool packed)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidDataException(
                $"Reference benchmark corpus directory '{root}' is incomplete. Delete only that directory before recreating it.");

        Directory.CreateDirectory(root);
        var repositoryPath = Path.Combine(root, "repository");
        var remotePath = Path.Combine(root, "remote.git");
        var clientPath = Path.Combine(root, "client");
        Directory.CreateDirectory(repositoryPath);
        RunGit(root, ["init", "--quiet", "--initial-branch=main", repositoryPath]);

        using (var importer = StartGit(repositoryPath, ["fast-import", "--quiet"], redirectInput: true))
        {
            using (var input = new StreamWriter(importer.StandardInput.BaseStream, new UTF8Encoding(false), 16 * 1024,
                       leaveOpen: false)
            {
                NewLine = "\n"
            })
            {
                WriteFastImport(input, specification);
                input.Flush();
            }

            var error = importer.StandardError.ReadToEnd();
            importer.WaitForExit();
            if (importer.ExitCode != 0)
                throw new InvalidOperationException($"git fast-import failed: {error}");
        }

        CreateStashReflog(repositoryPath, specification.StashCount);
        RunGit(root, ["clone", "--quiet", "--bare", repositoryPath, remotePath]);
        RunGit(root, ["init", "--quiet", clientPath]);
        RunGit(clientPath, ["remote", "add", "origin", remotePath]);
        CaptureGitOutput(root, ["ls-remote", "--tags", remotePath], Path.Combine(root, "remote-advertisement.txt"));

        if (packed)
        {
            RunGit(repositoryPath, ["pack-refs", "--all", "--prune"]);
            RunGit(remotePath, ["pack-refs", "--all", "--prune"]);
        }

        var manifest = CreateManifest(specification, profile, packed);
        File.WriteAllText(manifestPath, manifest + Environment.NewLine, new UTF8Encoding(false));
        var expected = CreateExpected(repositoryPath, remotePath, specification);
        File.WriteAllLines(expectedPath, expected.Select(pair => pair.Key + "=" + pair.Value), new UTF8Encoding(false));
    }

    private static void WriteFastImport(StreamWriter writer, GitReferenceCorpusSpecification specification)
    {
        WriteBlob(writer, 1, "reference benchmark\n");
        var nextMark = 2;
        var targetMarks = new int[TargetCommitCount];
        int? parent = null;
        for (var index = 0; index < TargetCommitCount; index++)
        {
            var commitMark = nextMark++;
            targetMarks[index] = commitMark;
            writer.WriteLine("commit refs/heads/main");
            writer.WriteLine($"mark :{commitMark}");
            writer.WriteLine($"author Benchmark <benchmark@musoq.invalid> {InitialTimestamp + index} +0000");
            writer.WriteLine($"committer Benchmark <benchmark@musoq.invalid> {InitialTimestamp + index} +0000");
            WriteData(writer, $"target {index}\n");
            if (parent.HasValue)
                writer.WriteLine($"from :{parent.Value}");
            writer.WriteLine("M 100644 :1 reference.txt");
            writer.WriteLine();
            parent = commitMark;
        }

        for (var index = 0; index < specification.TagCount; index++)
        {
            var name = TagName(index, specification.AnnotatedTagCount);
            var targetMark = targetMarks[index % targetMarks.Length];
            if (index < specification.AnnotatedTagCount)
            {
                // fast-import's tag command supplies refs/tags/ itself; reset takes the fully-qualified ref.
                writer.WriteLine($"tag {name}");
                writer.WriteLine($"from :{targetMark}");
                writer.WriteLine($"tagger Benchmark <benchmark@musoq.invalid> {InitialTimestamp + index} +0000");
                WriteData(writer, $"annotation {index}\n");
            }
            else
            {
                writer.WriteLine($"reset refs/tags/{name}");
                writer.WriteLine($"from :{targetMark}");
                writer.WriteLine();
            }
        }

        for (var index = 0; index < specification.StashCount; index++)
        {
            var mark = nextMark++;
            var targetMark = targetMarks[index % targetMarks.Length];
            var secondaryMark = targetMarks[(index + 1) % targetMarks.Length];
            writer.WriteLine($"commit refs/bench/stash/{index:D6}");
            writer.WriteLine($"mark :{mark}");
            writer.WriteLine($"author Benchmark <benchmark@musoq.invalid> {InitialTimestamp + 10_000 + index} +0000");
            writer.WriteLine($"committer Benchmark <benchmark@musoq.invalid> {InitialTimestamp + 10_000 + index} +0000");
            WriteData(writer, $"stash {index}\n");
            writer.WriteLine($"from :{targetMark}");
            writer.WriteLine($"merge :{secondaryMark}");
            writer.WriteLine();
        }
    }

    private static void CreateStashReflog(string repositoryPath, int stashCount)
    {
        var refs = RunGit(repositoryPath, ["for-each-ref", "--format=%(objectname)", "refs/bench/stash"])
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse()
            .ToArray();
        if (refs.Length != stashCount)
            throw new InvalidDataException($"Expected {stashCount} generated stash commits, received {refs.Length}.");

        string? previous = null;
        for (var index = 0; index < refs.Length; index++)
        {
            var arguments = new List<string> { "update-ref", "--create-reflog", "-m", $"stash benchmark {index}", "refs/stash", refs[index] };
            if (previous is not null)
                arguments.Add(previous);
            RunGit(repositoryPath, arguments);
            previous = refs[index];
        }
    }

    private static IReadOnlyDictionary<string, string> CreateExpected(
        string repositoryPath,
        string remotePath,
        GitReferenceCorpusSpecification specification)
    {
        var tagChecksum = StreamNulRecords(
            repositoryPath,
            ["for-each-ref", "--sort=refname", "--format=%(refname:short)%00%(refname)", "refs/tags"],
            fieldsPerRecord: 2,
            out var tagCount);
        var stashChecksum = StreamStashRecords(
            repositoryPath,
            ["log", "-g", "--no-decorate", "--format=%gd%x00%H%x00%P%x00%gs%x00", "refs/stash"],
            out var stashCount);
        var remoteChecksum = StreamRemoteRecords(
            repositoryPath,
            ["ls-remote", "--tags", "--refs", remotePath],
            out var remoteCount);
        if (tagCount != specification.TagCount || stashCount != specification.StashCount || remoteCount != specification.TagCount)
            throw new InvalidDataException(
                $"Generated reference corpus counts do not match its specification: tags={tagCount}, stashes={stashCount}, remote={remoteCount}, " +
                $"expected tags={specification.TagCount}, stashes={specification.StashCount}.");

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tagCount"] = tagCount.ToString(CultureInfo.InvariantCulture),
            ["annotatedTagCount"] = specification.AnnotatedTagCount.ToString(CultureInfo.InvariantCulture),
            ["stashCount"] = stashCount.ToString(CultureInfo.InvariantCulture),
            ["tagChecksum"] = tagChecksum.ToString(CultureInfo.InvariantCulture),
            ["stashChecksum"] = stashChecksum.ToString(CultureInfo.InvariantCulture),
            ["remoteChecksum"] = remoteChecksum.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static long StreamNulRecords(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int fieldsPerRecord,
        out long count)
    {
        long localCount = 0;
        long checksum = 17;
        StreamGitLines(workingDirectory, arguments, line =>
        {
            var fields = line.Split('\0', StringSplitOptions.None);
            if (fields.Length < fieldsPerRecord)
                return;
            checksum = GitFileHistoryBenchmarks.Fold(checksum, fields.Take(fieldsPerRecord).ToArray());
            localCount++;
        });
        count = localCount;
        return checksum;
    }

    private static long StreamStashRecords(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        out long count)
    {
        long localCount = 0;
        long checksum = 17;
        StreamGitLines(workingDirectory, arguments, line =>
        {
            var fields = line.Split('\0', StringSplitOptions.None);
            if (fields.Length < 4)
                return;

            var parents = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            checksum = GitFileHistoryBenchmarks.Fold(
                checksum,
                fields[0],
                fields[1],
                fields[3],
                parents.Length > 1 ? parents[1] : null,
                fields[1],
                parents.Length > 2 ? parents[2] : null);
            localCount++;
        });
        count = localCount;
        return checksum;
    }

    private static long StreamRemoteRecords(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        out long count)
    {
        long localCount = 0;
        long checksum = 17;
        StreamGitLines(workingDirectory, arguments, line =>
        {
            var separator = line.IndexOf('\t');
            if (separator <= 0)
                return;
            var objectSha = line[..separator];
            var canonical = line[(separator + 1)..];
            var friendly = canonical.StartsWith("refs/tags/", StringComparison.Ordinal)
                ? canonical["refs/tags/".Length..]
                : canonical;
            checksum = GitFileHistoryBenchmarks.Fold(checksum, friendly, canonical, objectSha);
            localCount++;
        });
        count = localCount;
        return checksum;
    }

    private static string TagName(int index, int annotatedCount) =>
        index < annotatedCount
            ? $"annotated-{index:D7}"
            : $"lightweight-{index - annotatedCount:D7}";

    private static void WriteBlob(StreamWriter writer, int mark, string content)
    {
        writer.WriteLine("blob");
        writer.WriteLine($"mark :{mark}");
        WriteData(writer, content);
    }

    private static void WriteData(StreamWriter writer, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        writer.WriteLine($"data {bytes.Length}");
        writer.Flush();
        writer.BaseStream.Write(bytes);
        writer.BaseStream.WriteByte((byte)'\n');
    }

    private static string CreateManifest(
        GitReferenceCorpusSpecification specification,
        GitReferenceCorpusProfile profile,
        bool packed) =>
        $"{CorpusVersion}|profile={profile}|{specification}|packed={packed}|targets={TargetCommitCount}";

    private static Dictionary<string, string> ReadExpected(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new InvalidDataException($"Malformed reference corpus expected line '{line}'.");
            values[line[..separator]] = line[(separator + 1)..];
        }
        foreach (var key in new[] { "tagCount", "annotatedTagCount", "stashCount", "tagChecksum", "stashChecksum", "remoteChecksum" })
            if (!values.ContainsKey(key))
                throw new InvalidDataException($"Reference corpus expected file is missing '{key}'.");
        return values;
    }

    private static string Fingerprint(string manifest, string expectedPath, string advertisementPath)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(manifest + "\n"));
        AppendFile(hash, expectedPath);
        hash.AppendData(Encoding.UTF8.GetBytes("\n"));
        AppendFile(hash, advertisementPath);
        return Convert.ToHexString(hash.GetHashAndReset())
            .ToLowerInvariant();
    }

    private static void AppendFile(IncrementalHash hash, string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            hash.AppendData(buffer, 0, read);
    }

    private static void ValidateLayout(string repositoryPath, string remotePath, string clientPath)
    {
        if (!Directory.Exists(repositoryPath) || !Directory.Exists(remotePath) || !Directory.Exists(clientPath))
            throw new InvalidDataException("Reference benchmark corpus is missing one of its repositories.");
        var configuredUrl = RunGit(clientPath, ["config", "--local", "--get", "remote.origin.url"]);
        if (Path.IsPathRooted(configuredUrl))
            return;
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("file" or ""))
            throw new InvalidDataException("Reference benchmark remote configuration is not a local path or URI.");
    }

    private static string RunGit(string workingDirectory, IReadOnlyList<string> arguments)
    {
        using var process = StartGit(workingDirectory, arguments, redirectInput: false);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
        return output.TrimEnd('\r', '\n');
    }

    private static void StreamGitLines(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        Action<string> onLine)
    {
        using var process = StartGit(workingDirectory, arguments, redirectInput: false);
        var errorTask = process.StandardError.ReadToEndAsync();
        while (process.StandardOutput.ReadLine() is { } line)
            onLine(line);

        var error = errorTask.GetAwaiter().GetResult();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
    }

    private static void CaptureGitOutput(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string outputPath)
    {
        using var process = StartGit(workingDirectory, arguments, redirectInput: false);
        using (var output = new StreamWriter(outputPath, false, new UTF8Encoding(false))
        {
            NewLine = "\n"
        })
        {
            var errorTask = process.StandardError.ReadToEndAsync();
            while (process.StandardOutput.ReadLine() is { } line)
                output.WriteLine(line);

            output.Flush();
            var error = errorTask.GetAwaiter().GetResult();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
        }
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
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start local Git.");
    }

    private sealed record GitReferenceCorpusSpecification(
        int TagCount,
        int AnnotatedTagCount,
        int StashCount)
    {
        public static GitReferenceCorpusSpecification For(GitReferenceCorpusProfile profile) => profile switch
        {
            GitReferenceCorpusProfile.Smoke => new(1_000, 100, 8),
            GitReferenceCorpusProfile.Verify => new(100_000, 5_000, 256),
            GitReferenceCorpusProfile.Scale => new(1_000_000, 10_000, 1_024),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }
}
