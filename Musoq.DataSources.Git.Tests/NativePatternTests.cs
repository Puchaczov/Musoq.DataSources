using System;
using System.IO;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
namespace Musoq.DataSources.Git.Tests;
[TestClass]
[DoNotParallelize]
public sealed class NativePatternTests
{
    [TestMethod]
    public void AllHistoryAndReferenceStringColumns_MatchCore()
    {
        var path = Path.Combine(Path.GetTempPath(), "musoq-pattern-git-" + Guid.NewGuid().ToString("N"));
        Repository.Init(path);
        try
        {
            using (var repo = new Repository(path))
            {
                var author = new Signature("Alpha Author", "alpha@example.test", DateTimeOffset.Now);
                File.WriteAllText(Path.Combine(path, "alpha.txt"), "Alpha\n");
                Commands.Stage(repo, "alpha.txt"); repo.Commit("Alpha commit", author, author);
                File.AppendAllText(Path.Combine(path, "alpha.txt"), "Beta\n");
                Commands.Stage(repo, "alpha.txt"); repo.Commit("Beta commit", author, author);
                repo.ApplyTag("alpha-tag"); repo.Branches.Add("alpha-branch", repo.Head.Tip);
                repo.Network.Remotes.Add("origin", path);
                File.WriteAllText(Path.Combine(path, "stash.txt"), "Alpha");
                repo.Stashes.Add(author, "Alpha stash", StashModifiers.IncludeUntracked);
                File.WriteAllText(Path.Combine(path, "status.txt"), "Alpha");
            }
            var schema = new GitSchema();
            var args = SourceArguments.Create(path, SourceArguments.Empty);
            PatternContractVerifier.Verify<RepositoryEntity, SourceArguments<string, EmptySourceArguments>>(schema, "repository", args, [path]);
            PatternContractVerifier.Verify<CommitEntity, SourceArguments<string, EmptySourceArguments>>(schema, "commits", args, [path]);
            PatternContractVerifier.Verify<BranchEntity, SourceArguments<string, EmptySourceArguments>>(schema, "branches", args, [path]);
            PatternContractVerifier.Verify<TagEntity, SourceArguments<string, EmptySourceArguments>>(schema, "tags", args, [path]);
            PatternContractVerifier.Verify<StashEntity, SourceArguments<string, EmptySourceArguments>>(schema, "stashes", args, [path]);
            PatternContractVerifier.Verify<StatusEntity, SourceArguments<string, EmptySourceArguments>>(schema, "status", args, [path]);
            PatternContractVerifier.Verify<RemoteEntity, SourceArguments<string, EmptySourceArguments>>(schema, "remotes", args, [path]);
            var fileArgs = SourceArguments.Create(path, SourceArguments.Create("alpha.txt", SourceArguments.Empty));
            PatternContractVerifier.Verify<FileHistoryEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "filehistory", fileArgs, [path, "alpha.txt"]);
            PatternContractVerifier.Verify<BlameHunkEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "blame", fileArgs, [path, "alpha.txt"]);
            var remoteArgs = SourceArguments.Create(path, SourceArguments.Create("origin", SourceArguments.Empty));
            PatternContractVerifier.Verify<RemoteTagEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "remotetags", remoteArgs, [path, "origin"]);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }
    }
}
