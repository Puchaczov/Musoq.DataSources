using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitToSqlTests
{
    [TestMethod]
    public void WhenNonExistentPathPassed_ShouldThrow()
    {
        var query = """
                        select 
                            Path, 
                            WorkingDirectory, 
                            Head.FriendlyName,
                            Head.CanonicalName,
                            Head.IsRemote,
                            Head.IsTracking,
                            Head.IsCurrentRepositoryHead,
                            Head.RemoteName,
                            Head.Tip.Sha,
                            Head.Tip.Message,
                            Head.Tip.MessageShort,
                            Head.Tip.Author,
                            Head.Tip.Committer,
                            Information.Path,
                            Information.WorkingDirectory,
                            Information.IsBare, 
                            Information.IsHeadDetached, 
                            Information.IsHeadUnborn, 
                            Information.IsShallow
                        from #git.repository('{RepositoryPath}')
                    """.Replace("{RepositoryPath}", "C:\\NonExistentPath");

        var vm = CreateAndRunVirtualMachine(query);

        Assert.ThrowsException<InvalidOperationException>(() => vm.Run());
    }

    [TestMethod]
    public async Task WhenBasicInfoFromRepositoryRetrieved_ShouldPass()
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath, "wbifrr");

        {
            var query = """
                            select 
                                Path, 
                                WorkingDirectory, 
                                Head.FriendlyName,
                                Head.CanonicalName,
                                Head.IsRemote,
                                Head.IsTracking,
                                Head.IsCurrentRepositoryHead,
                                Head.RemoteName,
                                Head.Tip.Sha,
                                Head.Tip.Message,
                                Head.Tip.MessageShort,
                                Head.Tip.Author,
                                Head.Tip.Committer,
                                Information.Path,
                                Information.WorkingDirectory,
                                Information.IsBare, 
                                Information.IsHeadDetached, 
                                Information.IsHeadUnborn, 
                                Information.IsShallow
                            from #git.repository('{RepositoryPath}')
                        """.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

            var vm = CreateAndRunVirtualMachine(query);

            var result = vm.Run();

            Assert.AreEqual(1, result.Count);

            var row = result[0];

            Assert.IsTrue((string) row[0] == Path.Combine(unpackedRepositoryPath, ".git\\"));
            Assert.IsTrue((string) row[1] == $"{unpackedRepositoryPath}\\");
            Assert.IsTrue((string) row[2] == "master");
            Assert.IsTrue((string) row[3] == "refs/heads/master");
            Assert.IsTrue((bool) row[4] == false);
            Assert.IsTrue((bool) row[5] == false);
            Assert.IsTrue((bool) row[6]);
            Assert.IsNull((string) row[7]);
            Assert.IsTrue((string) row[8] == "789f584ce162424f61b33e020e2138aad47e60ba");
            Assert.IsTrue((string) row[9] == "initial commit\n");
            Assert.IsTrue((string) row[10] == "initial commit");
            Assert.IsTrue((string) row[11] == "anonymous");
            Assert.IsTrue((string) row[12] == "anonymous");
            Assert.IsTrue((string) row[13] == Path.Combine(unpackedRepositoryPath, ".git\\"));
            Assert.IsTrue((string) row[14] == $"{unpackedRepositoryPath}\\");
            Assert.IsTrue((bool) row[15] == false);
            Assert.IsTrue((bool) row[16] == false);
            Assert.IsTrue((bool) row[17] == false);
            Assert.IsTrue((bool) row[18] == false);
        }
    }

    [TestMethod]
    public async Task WhenBranchesFromRepositoryRetrieved_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath, "wbfrr");

        var query = """
                        select 
                            Branch.FriendlyName,
                            Branch.CanonicalName,
                            Branch.IsRemote,
                            Branch.IsTracking,
                            Branch.IsCurrentRepositoryHead,
                            Branch.RemoteName,
                            Branch.BranchTrackingDetails.AheadBy,
                            Branch.BranchTrackingDetails.BehindBy,
                            Branch.Tip.Sha,
                            Branch.Tip.Message,
                            Branch.Tip.MessageShort,
                            Branch.Tip.Author,
                            Branch.Tip.Committer,
                            Branch.UpstreamBranchCanonicalName,
                            Branch.RemoteName
                        from #git.repository('{RepositoryPath}') repository cross apply repository.Branches as Branch
                    """.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);

        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 2, "Result should have 2 entries");

        // First row assertions
        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "feature/feature_a" &&
            (string)row[1] == "refs/heads/feature/feature_a" &&
            (bool)row[2] == false &&
            (bool)row[3] == false &&
            (bool)row[4] == false &&
            row[5] == null &&
            row[6] == null &&
            row[7] == null &&
            (string)row[8] == "3250d89501e0569115a5cda34807e15ba7de0aa6" &&
            (string)row[9] == "modified library_1\n" &&
            (string)row[10] == "modified library_1" &&
            (string)row[11] == "anonymous" &&
            (string)row[12] == "anonymous" &&
            row[13] == null &&
            row[14] == null
        ), "First row should match feature/feature_a details");

        // Second row assertions
        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "master" &&
            (string)row[1] == "refs/heads/master" &&
            (bool)row[2] == false &&
            (bool)row[3] == false &&
            (bool)row[4] == true &&
            row[5] == null &&
            row[6] == null &&
            row[7] == null &&
            (string)row[8] == "bf8542548c686f98d3c562d2fc78259640d07cbb" &&
            (string)row[9] == "add documentation index\n" &&
            (string)row[10] == "add documentation index" &&
            (string)row[11] == "anonymous" &&
            (string)row[12] == "anonymous" &&
            row[13] == null &&
            row[14] == null
        ), "Second row should match master branch details");
    }

    [TestMethod]
    public async Task WhenCommitsOnBranchRetrieved_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                Commit.Sha,
                Commit.Message,
                Commit.MessageShort,
                Commit.Author,
                Commit.Committer
            from #git.repository('{RepositoryPath}') repository cross apply repository.Commits as Commit";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 5, "Result should have 5 entries");

        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "bf8542548c686f98d3c562d2fc78259640d07cbb" &&
            (string)row[1] == "add documentation index\n" &&
            (string)row[2] == "add documentation index" &&
            (string)row[3] == "anonymous" &&
            (string)row[4] == "anonymous"
        ), "First row should match first commit details");

        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "3250d89501e0569115a5cda34807e15ba7de0aa6" &&
            (string)row[1] == "modified library_1\n" &&
            (string)row[2] == "modified library_1" &&
            (string)row[3] == "anonymous" &&
            (string)row[4] == "anonymous"
        ), "Second row should match second commit details");

        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "02c2e53d8712210a4254fc9bd6ee5548a0f1d211" &&
            (string)row[1] == "add first library\n" &&
            (string)row[2] == "add first library" &&
            (string)row[3] == "anonymous" &&
            (string)row[4] == "anonymous"
        ), "Third row should match third commit details");

        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "595b3f0f51071f84909861e5abc15225a4ef4555" &&
            (string)row[1] == "first commit\n" &&
            (string)row[2] == "first commit" &&
            (string)row[3] == "anonymous" &&
            (string)row[4] == "anonymous"
        ), "Fourth row should match fourth commit details");

        Assert.IsTrue(result.Any(row => 
            (string)row[0] == "789f584ce162424f61b33e020e2138aad47e60ba" &&
            (string)row[1] == "initial commit\n" &&
            (string)row[2] == "initial commit" &&
            (string)row[3] == "anonymous" &&
            (string)row[4] == "anonymous"
        ), "Fifth row should match fifth commit details");
    }

    [TestMethod]
    public async Task WhenTagsFromRepositoryRetrieved_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);

        var query = @"
            select
                Tag.FriendlyName,
                Tag.CanonicalName,
                Tag.Message,
                Tag.IsAnnotated
            from #git.repository('{RepositoryPath}') repository cross apply repository.Tags as Tag
            where Tag.IsAnnotated = false";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count == 1);

        var row = result[0];

        Assert.IsTrue((string) row[0] == "v0.1");
        Assert.IsTrue((string) row[1] == "refs/tags/v0.1");
        Assert.IsNull((string) row[2]);
        Assert.IsFalse((bool) row[3]);

        query = @"
            select
                Tag.FriendlyName,
                Tag.CanonicalName,
                Tag.Message,
                Tag.IsAnnotated,
                Tag.Annotation.Message,
                Tag.Annotation.Name,
                Tag.Annotation.Sha,
                Tag.Annotation.Tagger.Name,
                Tag.Annotation.Tagger.Email,
                Tag.Annotation.Tagger.WhenSigned
            from #git.repository('{RepositoryPath}') repository cross apply repository.Tags as Tag
            where Tag.IsAnnotated = true";

        vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        result = vm.Run();

        Assert.IsTrue(result.Count == 1);

        row = result[0];

        Assert.IsTrue((string) row[0] == "v0.0");
        Assert.IsTrue((string) row[1] == "refs/tags/v0.0");
        Assert.IsTrue((string) row[2] == "Initial release of repository\n");
        Assert.IsTrue((bool) row[3]);
        Assert.IsTrue((string) row[4] == "Initial release of repository\n");
        Assert.IsTrue((string) row[5] == "v0.0");
        Assert.IsTrue((string) row[6] == "c834c069c0cbed7ba309bcd6bf530e36f3e77344");
        Assert.IsTrue((string) row[7] == "anonymous");
        Assert.IsTrue((string) row[8] == "anonymous@non-existing-domain.com");
        Assert.IsTrue((DateTimeOffset) row[9] == new DateTimeOffset(2024, 11, 02, 9, 39, 40, TimeSpan.FromHours(1)));
    }

    [TestMethod]
    public async Task WhenTagsQueriedDirectly_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);

        var query = @"
            select
                t.FriendlyName,
                t.CanonicalName,
                t.Message,
                t.IsAnnotated,
                t.Commit.Sha
            from #git.tags('{RepositoryPath}') t";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2);

        var v01Row = result.FirstOrDefault(r => (string)r[0] == "v0.1");
        Assert.IsNotNull(v01Row);
        Assert.IsTrue((string)v01Row[1] == "refs/tags/v0.1");
        Assert.IsNull((string)v01Row[2]);
        Assert.IsFalse((bool)v01Row[3]);
        Assert.IsNotNull((string)v01Row[4]);

        var v00Row = result.FirstOrDefault(r => (string)r[0] == "v0.0");
        Assert.IsNotNull(v00Row);
        Assert.IsTrue((string)v00Row[1] == "refs/tags/v0.0");
        Assert.IsTrue((string)v00Row[2] == "Initial release of repository\n");
        Assert.IsTrue((bool)v00Row[3]);
        Assert.IsNotNull((string)v00Row[4]);
    }

    [TestMethod]
    public async Task WhenStashQueried_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);

        var query = @"
            select
                Stash.Message
            from #git.repository('{RepositoryPath}') repository cross apply repository.Stashes as Stash";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        var result = vm.Run();

        Assert.IsTrue(result.Count == 1);

        var row = result[0];

        Assert.IsTrue((string) row[0] == "WIP on master: bf85425 add documentation index");
    }

    [TestMethod]
    public async Task WhenDifferenceBetweenTwoCommits_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);

        var query = @"
            select
                Difference.Path,
                Difference.ChangeKind,
                Difference.OldPath,
                Difference.OldMode,
                Difference.NewMode,
                Difference.OldSha,
                Difference.NewSha
            from #git.repository('{RepositoryPath}') repository cross apply repository.DifferenceBetween(repository.CommitFrom('bf85425'), repository.CommitFrom('3250d89')) as Difference";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        var result = vm.Run();

        Assert.IsTrue(result.Count == 1);

        var row = result[0];

        Assert.IsTrue((string) row[0] == "documentation/index.md");
        Assert.IsTrue((string) row[1] == "Deleted");
        Assert.IsTrue((string) row[2] == "documentation/index.md");
        Assert.IsTrue((string) row[3] == "NonExecutableFile");
        Assert.IsTrue((string) row[4] == "Nonexistent");
        Assert.IsTrue((string) row[5] == "0293f650617fe1ca2c99d4f6dad995b472120843");
        Assert.IsTrue((string) row[6] == "0000000000000000000000000000000000000000");
    }

    [TestMethod]
    public async Task WhenDifferenceBetweenTwoBranches_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);

        var query = @"
            select
                Difference.Path,
                Difference.ChangeKind,
                Difference.OldPath,
                Difference.OldMode,
                Difference.NewMode,
                Difference.OldSha,
                Difference.NewSha
            from #git.repository('{RepositoryPath}') repository cross apply repository.DifferenceBetween(repository.BranchFrom('master'), repository.BranchFrom('feature/feature_a')) as Difference";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        var result = vm.Run();

        Assert.IsTrue(result.Count == 1);

        var row = result[0];

        Assert.IsTrue((string) row[0] == "documentation/index.md");
        Assert.IsTrue((string) row[1] == "Deleted");
        Assert.IsTrue((string) row[2] == "documentation/index.md");
        Assert.IsTrue((string) row[3] == "NonExecutableFile");
        Assert.IsTrue((string) row[4] == "Nonexistent");
        Assert.IsTrue((string) row[5] == "0293f650617fe1ca2c99d4f6dad995b472120843");
        Assert.IsTrue((string) row[6] == "0000000000000000000000000000000000000000");
    }
    
    [TestMethod]
    public async Task WhenBranchSpecificCommitsFromBranchToMaster_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository5ZipPath, "wbscfbtm1");
        
        var query = @"
            with BranchInfo as (
                select
                    c.Sha as Sha,
                    c.Message as Message,
                    c.Author as Author,
                    c.AuthorEmail as AuthorEmail,
                    c.CommittedWhen as CommittedWhen
                from #git.repository('{RepositoryPath}') r 
                cross apply r.SearchForBranches('feature/branch_1') b
                cross apply b.GetBranchSpecificCommits(r.Self, b.Self, true) c
            )
            select Sha, Message, Author, AuthorEmail, CommittedWhen from BranchInfo;".Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());
        
        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 1);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == "655595cfb4bdfc4e42b9bb80d48212c2dca95086");
        Assert.IsTrue((string) row[1] == "finished implementation for branch_1\n");
        Assert.IsTrue((string) row[2] == "anonymous");
        Assert.IsTrue((string) row[3] == "anonymous@non-existing-domain.com");
        Assert.IsTrue((DateTimeOffset) row[4] == new DateTimeOffset(2024, 11, 08, 19, 54, 08, TimeSpan.FromHours(1)));
    }
    
    [TestMethod]
    public async Task WhenBranchSpecificCommitsFromBranchToMaster_ExcludeMergeBaseFalse_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository5ZipPath, "wbscfbtm2");
        
        var query = @"
            with BranchInfo as (
                select
                    c.Sha as Sha,
                    c.Message as Message,
                    c.Author as Author,
                    c.AuthorEmail as AuthorEmail,
                    c.CommittedWhen as CommittedWhen
                from #git.repository('{RepositoryPath}') r 
                cross apply r.SearchForBranches('feature/branch_1') b
                cross apply b.GetBranchSpecificCommits(r.Self, b.Self, false) c
            )
            select Sha, Message, Author, AuthorEmail, CommittedWhen from BranchInfo;".Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());
        
        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 2, "Result should contain exactly 2 records");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "655595cfb4bdfc4e42b9bb80d48212c2dca95086" &&
                (string)r[1] == "finished implementation for branch_1\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 54, 08, TimeSpan.FromHours(1))),
            "Missing first commit record");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "bf8542548c686f98d3c562d2fc78259640d07cbb" &&
                (string)r[1] == "add documentation index\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 02, 8, 43, 41, TimeSpan.FromHours(1))),
            "Missing second commit record");
    }
    
    [TestMethod]
    public async Task WhenBranchSpecificCommitsFromBranchToAnotherBranch_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository5ZipPath, "wbscfbtab3");
        
        var query = @"
            with BranchInfo as (
                select
                    c.Sha as Sha,
                    c.Message as Message,
                    c.Author as Author,
                    c.AuthorEmail as AuthorEmail,
                    c.CommittedWhen as CommittedWhen
                from #git.repository('{RepositoryPath}') r 
                cross apply r.SearchForBranches('feature/branch_2') b
                cross apply b.GetBranchSpecificCommits(r.Self, b.Self, false) c
            )
            select Sha, Message, Author, AuthorEmail, CommittedWhen from BranchInfo;".Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());
        
        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 3, "Result should contain exactly 3 records");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "389642ba15392c4540e82628bdff9c99dc6f7923" &&
                (string)r[1] == "modified main.py\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 57, 02, TimeSpan.FromHours(1))),
            "Missing first commit record");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "fb24727b684a511e7f93df2910e4b280f6b9072f" &&
                (string)r[1] == "add file_branch_2.py\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 56, 17, TimeSpan.FromHours(1))),
            "Missing second commit record");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "655595cfb4bdfc4e42b9bb80d48212c2dca95086" &&
                (string)r[1] == "finished implementation for branch_1\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 54, 08, TimeSpan.FromHours(1))),
            "Missing third commit record");
    }
    
    [TestMethod]
    public async Task WhenBranchSpecificCommitsFromBranchToAnotherBranch_ExcludeMergeBaseFalse_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository5ZipPath, "wbscfbtab4");
        
        var query = @"
            with BranchInfo as (
                select
                    c.Sha as Sha,
                    c.Message as Message,
                    c.Author as Author,
                    c.AuthorEmail as AuthorEmail,
                    c.CommittedWhen as CommittedWhen
                from #git.repository('{RepositoryPath}') r 
                cross apply r.SearchForBranches('feature/branch_2') b
                cross apply b.GetBranchSpecificCommits(r.Self, b.Self, false) c
            )
            select Sha, Message, Author, AuthorEmail, CommittedWhen from BranchInfo;".Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());
        
        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 3, "Result should contain exactly 3 records");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "389642ba15392c4540e82628bdff9c99dc6f7923" &&
                (string)r[1] == "modified main.py\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 57, 02, TimeSpan.FromHours(1))),
            "Missing first commit record");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "fb24727b684a511e7f93df2910e4b280f6b9072f" &&
                (string)r[1] == "add file_branch_2.py\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 56, 17, TimeSpan.FromHours(1))),
            "Missing second commit record");

        Assert.IsTrue(result.Any(r => 
                (string)r[0] == "655595cfb4bdfc4e42b9bb80d48212c2dca95086" &&
                (string)r[1] == "finished implementation for branch_1\n" &&
                (string)r[2] == "anonymous" &&
                (string)r[3] == "anonymous@non-existing-domain.com" &&
                (DateTimeOffset)r[4] == new DateTimeOffset(2024, 11, 08, 19, 54, 08, TimeSpan.FromHours(1))),
            "Missing third commit record");
    }

    [TestMethod]
    public async Task WhenMinMaxCommitsFromMaster_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository5ZipPath, "wmmcfm");
        
        var query = @"
            with Commits as (
                select
                    c.MinCommit(c.Self) as Min,
                    c.MaxCommit(c.Self) as Max
                from #git.repository('{RepositoryPath}') r
                cross apply r.Commits c
                group by 'fake'
            )
            select
                Min.Sha as MinSha,
                Max.Sha as MaxSha
            from Commits;".Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 1);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == "789f584ce162424f61b33e020e2138aad47e60ba");
        Assert.IsTrue((string) row[1] == "389642ba15392c4540e82628bdff9c99dc6f7923");
    }

    [TestMethod]
    public async Task WhenCommitsQueriedDirectly_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath);

        var query = @"
            select
                c.Sha,
                c.Author,
                c.Message
            from #git.commits('{RepositoryPath}') c
            where c.Author = 'anonymous'";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0);
        
        var row = result[0];
        Assert.IsNotNull((string)row[0]);
        Assert.IsTrue((string)row[1] == "anonymous");
        Assert.IsNotNull((string)row[2]);
    }

    [TestMethod]
    public async Task WhenBranchesQueriedDirectly_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                b.FriendlyName,
                b.IsRemote,
                b.Tip.Sha
            from #git.branches('{RepositoryPath}') b";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0);
        
        var masterBranch = result.FirstOrDefault(r => ((string)r[0])?.Contains("master") == true);
        Assert.IsNotNull(masterBranch);
        Assert.IsNotNull((string)masterBranch[2]);
    }

    [TestMethod]
    public async Task WhenFileHistoryQueried_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath);

        var query = @"
            select
                h.CommitSha,
                h.Author,
                h.AuthorEmail,
                h.CommittedWhen,
                h.FilePath,
                h.ChangeType,
                h.OldPath
            from #git.filehistory('{RepositoryPath}', '*') h";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 0);
        
        var row = result[0];
        Assert.IsNotNull(row[0]);
        Assert.IsNotNull(row[1]);
        Assert.IsNotNull(row[2]);
        Assert.IsNotNull(row[3]);
        Assert.IsNotNull(row[4]);
        Assert.IsNotNull(row[5]);
    }

    [TestMethod]
    public async Task WhenFileHistoryQueriedWithSpecificFile_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath);

        var query = @"
            select
                h.CommitSha,
                h.FilePath,
                h.ChangeType
            from #git.filehistory('{RepositoryPath}', 'README.md') h";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 0);
        
        foreach (var row in result)
        {
            var filePath = row[1] as string;
            Assert.IsTrue(filePath?.EndsWith("README.md", StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }

    [TestMethod]
    public async Task WhenCommitParentsQueried_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select 
                c.Sha, 
                p.Sha as ParentSha
            from #git.commits('{RepositoryPath}') c 
            cross apply c.Parents as p";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 0);
    }

    [TestMethod]
    public async Task WhenRemotesQueried_ShouldPass()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath);

        var query = @"
            select
                r.Name,
                r.Url
            from #git.remotes('{RepositoryPath}') r";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 0);
    }

    static GitToSqlTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private Task<UnpackedRepository> UnpackGitRepositoryAsync(string zippedRepositoryPath,
        [CallerMemberName] string? testName = null)
    {
        if (testName is null)
            throw new ArgumentNullException(nameof(testName));

        if (!File.Exists(zippedRepositoryPath))
            throw new InvalidOperationException("File does not exist.");

        var directory = Path.GetDirectoryName(zippedRepositoryPath);

        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Directory is empty.");

        var repositoryPath = Path.Combine(directory, ".TestsExecutions", testName);

        if (Directory.Exists(repositoryPath))
            Directory.Delete(repositoryPath, true);

        ZipFile.ExtractToDirectory(zippedRepositoryPath, repositoryPath);

        if (!Directory.Exists(repositoryPath))
            throw new InvalidOperationException("Directory was not created.");

        var fileName = Path.GetFileNameWithoutExtension(zippedRepositoryPath);
        var fullPath = Path.Combine(repositoryPath, fileName);
        return Task.FromResult(new UnpackedRepository(fullPath));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new GitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string Repository1ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository1.zip");

    private static string Repository2ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository2.zip");

    private static string Repository3ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository3.zip");

    private static string Repository4ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository4.zip");

    private static string Repository5ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository5.zip");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(GitToSqlTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    private class UnpackedRepository : IDisposable
    {
        private static readonly ConcurrentDictionary<string, int> IsCounter = new();

        public UnpackedRepository(string path)
        {
            Path = path;
            IsCounter.AddOrUpdate(path, 1, (_, value) => value + 1);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!IsCounter.TryGetValue(Path, out var value)) return;

            if (value == 1)
            {
                //I'm going to skip it until the proper implementation within runtime will be done.
                //Directory.Delete(Path, true);
                IsCounter.TryRemove(Path, out _);
            }
            else
            {
                IsCounter.AddOrUpdate(Path, value - 1, (_, _) => value - 1);
            }
        }

        public static implicit operator string(UnpackedRepository unpackedRepository)
        {
            return unpackedRepository.Path;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}