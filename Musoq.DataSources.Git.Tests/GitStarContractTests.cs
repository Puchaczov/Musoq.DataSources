using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitStarContractTests
{
    private static string StartDirectory
    {
        get
        {
            var directory = Path.GetDirectoryName(typeof(GitStarContractTests).Assembly.Location);
            return string.IsNullOrWhiteSpace(directory)
                ? throw new InvalidOperationException("The test assembly directory is unavailable.")
                : directory;
        }
    }

    [TestMethod]
    public async Task EveryGitConstructor_HasOneExactStarContract()
    {
        using var repository = Unpack("Repository5.zip");
        using var blameRepository = Unpack("BlameTestRepo.zip");
        var cases = CreateCases(repository.Path, blameRepository.Path);
        var schema = new GitSchema();
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), cases);

        foreach (var contract in cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    [TestMethod]
    public void RepositoryCollections_RemainCrossApplyAddressable()
    {
        using var repository = Unpack("Repository5.zip");
        var escapedPath = repository.Path.Escape();
        var queries = new Dictionary<string, string>
        {
            ["branches"] = $"select b.FriendlyName from git.repository('{escapedPath}') r cross apply r.Branches b",
            ["tags"] = $"select t.FriendlyName from git.repository('{escapedPath}') r cross apply r.Tags t",
            ["commits"] = $"select c.Sha from git.repository('{escapedPath}') r cross apply r.Commits c",
            ["configuration"] = $"select c.Key, c.Value from git.repository('{escapedPath}') r cross apply r.Configuration c",
            ["stashes"] = $"select s.Message from git.repository('{escapedPath}') r cross apply r.Stashes s",
            ["commit parents"] = $"select p.Sha from git.commits('{escapedPath}') c cross apply c.Parents p"
        };

        foreach (var pair in queries)
        {
            var result = Compile(pair.Value).Run();
            Assert.IsTrue(result.Count > 0, $"Git apply '{pair.Key}' returned no rows.");
        }
    }

    [TestMethod]
    public void BlameLines_AndDifferenceBytes_ProjectPrimitiveValues()
    {
        using var blameRepository = Unpack("BlameTestRepo.zip");
        var escapedBlamePath = blameRepository.Path.Escape();
        var blameQuery = $"select l.LineNumber, l.Content from git.blame('{escapedBlamePath}', 'test_file.txt') h cross apply h.Lines l order by l.LineNumber";
        var blameResult = Compile(blameQuery).Run();

        Assert.AreEqual(4, blameResult.Count);
        Assert.AreEqual(1, blameResult[0][0]);
        Assert.IsInstanceOfType(blameResult[0][1], typeof(string));

        using var differenceRepository = Unpack("Repository4.zip");
        var escapedDifferencePath = differenceRepository.Path.Escape();
        var oldBytesQuery = $"select b.Value from git.repository('{escapedDifferencePath}') r cross apply r.DifferenceBetween(r.CommitFrom('bf85425'), r.CommitFrom('3250d89')) d cross apply d.OldContentBytes b";
        var newBytesQuery = $"select b.Value from git.repository('{escapedDifferencePath}') r cross apply r.DifferenceBetween(r.CommitFrom('3250d89'), r.CommitFrom('bf85425')) d cross apply d.NewContentBytes b";

        var oldBytes = Compile(oldBytesQuery).Run();
        var newBytes = Compile(newBytesQuery).Run();

        Assert.IsTrue(oldBytes.Count > 0, "OldContentBytes should expose bytes for a deleted blob.");
        Assert.IsTrue(newBytes.Count > 0, "NewContentBytes should expose bytes for an added blob.");
        foreach (var row in oldBytes.Concat(newBytes))
            Assert.IsInstanceOfType(row[0], typeof(byte));
    }

    private static StarContractCase[] CreateCases(string repositoryPath, string blameRepositoryPath)
    {
        var path = repositoryPath.Escape();
        var blamePath = blameRepositoryPath.Escape();

        return
        [
            new(
                "repository",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.repository('{path}')",
                [Column("Path", typeof(string)), Column("WorkingDirectory", typeof(string))],
                ["Branches", "Tags", "Commits", "Head", "Configuration", "Information", "Stashes", "Self"]),
            new(
                "tags",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.tags('{path}')",
                [Column("FriendlyName", typeof(string)), Column("CanonicalName", typeof(string)),
                    Column("Message", typeof(string)), Column("IsAnnotated", typeof(bool))],
                ["Annotation", "Commit"]),
            new(
                "commits",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.commits('{path}')",
                [Column("Sha", typeof(string)), Column("Message", typeof(string)), Column("MessageShort", typeof(string)),
                    Column("Author", typeof(string)), Column("AuthorEmail", typeof(string)), Column("Committer", typeof(string)),
                    Column("CommitterEmail", typeof(string)), Column("CommittedWhen", typeof(DateTimeOffset))],
                ["Parents", "Self"]),
            new(
                "branches",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.branches('{path}')",
                [Column("FriendlyName", typeof(string)), Column("CanonicalName", typeof(string)), Column("IsRemote", typeof(bool)),
                    Column("IsTracking", typeof(bool)), Column("IsCurrentRepositoryHead", typeof(bool)),
                    Column("UpstreamBranchCanonicalName", typeof(string)), Column("RemoteName", typeof(string))],
                ["TrackedBranch", "BranchTrackingDetails", "Tip", "Commits", "ParentBranch", "Self"]),
            new(
                "filehistory",
                [typeof(string), typeof(string)],
                [repositoryPath, "*"],
                $"select * from git.filehistory('{path}', '*')",
                FileHistoryColumns(),
                []),
            new(
                "filehistory",
                [typeof(string), typeof(string), typeof(int)],
                [repositoryPath, "*", 1],
                $"select * from git.filehistory('{path}', '*', 1)",
                FileHistoryColumns(),
                []),
            new(
                "filehistory",
                [typeof(string), typeof(string), typeof(int), typeof(int)],
                [repositoryPath, "*", 0, 1],
                $"select * from git.filehistory('{path}', '*', 0, 1)",
                FileHistoryColumns(),
                []),
            new(
                "status",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.status('{path}')",
                [Column("FilePath", typeof(string)), Column("State", typeof(string)),
                    Column("IndexStatus", typeof(string)), Column("WorkDirStatus", typeof(string))],
                []),
            new(
                "remotes",
                [typeof(string)],
                [repositoryPath],
                $"select * from git.remotes('{path}')",
                [Column("Name", typeof(string)), Column("Url", typeof(string)), Column("PushUrl", typeof(string))],
                []),
            new(
                "blame",
                [typeof(string), typeof(string)],
                [blameRepositoryPath, "test_file.txt"],
                $"select * from git.blame('{blamePath}', 'test_file.txt')",
                BlameColumns(),
                ["Lines", "Self"]),
            new(
                "blame",
                [typeof(string), typeof(string), typeof(string)],
                [blameRepositoryPath, "test_file.txt", "HEAD"],
                $"select * from git.blame('{blamePath}', 'test_file.txt', 'HEAD')",
                BlameColumns(),
                ["Lines", "Self"])
        ];
    }

    private static StarContractColumn[] FileHistoryColumns()
    {
        return
        [
            Column("CommitSha", typeof(string)), Column("Author", typeof(string)), Column("AuthorEmail", typeof(string)),
            Column("CommittedWhen", typeof(DateTimeOffset)), Column("FilePath", typeof(string)),
            Column("ChangeType", typeof(string)), Column("OldPath", typeof(string))
        ];
    }

    private static StarContractColumn[] BlameColumns()
    {
        return
        [
            Column("StartLineNumber", typeof(int)), Column("EndLineNumber", typeof(int)), Column("LineCount", typeof(int)),
            Column("CommitSha", typeof(string)), Column("Author", typeof(string)), Column("AuthorEmail", typeof(string)),
            Column("AuthorDate", typeof(DateTimeOffset)), Column("Committer", typeof(string)),
            Column("CommitterEmail", typeof(string)), Column("CommitterDate", typeof(DateTimeOffset)),
            Column("Summary", typeof(string)), Column("OriginalStartLineNumber", typeof(int?)),
            Column("OriginalFilePath", typeof(string))
        ];
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new GitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "git-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);

    private static UnpackedRepository Unpack(string fileName, [CallerMemberName] string? testName = null)
    {
        var zipPath = Path.Combine(StartDirectory, "Repositories", fileName);
        if (!File.Exists(zipPath))
            throw new InvalidOperationException($"Git fixture '{zipPath}' does not exist.");

        var root = Path.Combine(Path.GetTempPath(), "mqgt", testName ?? "GitStarContract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ZipFile.ExtractToDirectory(zipPath, root);
        return new UnpackedRepository(root, Path.Combine(root, Path.GetFileNameWithoutExtension(fileName)));
    }

    private sealed class UnpackedRepository : IDisposable
    {
        public UnpackedRepository(string root, string path)
        {
            Root = root;
            Path = path;
        }

        public string Root { get; }
        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
