using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Musoq.Converter;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitToSqlTests
{
    [TestMethod]
    public async Task WhenBasicInfoFromRepositoryRetrieved_ShouldPass()
    {
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository1ZipPath);
        
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
        """.Replace("{RepositoryPath}", unpackedRepositoryPath);

        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 1);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == Path.Combine(unpackedRepositoryPath, ".git\\"));
        Assert.IsTrue((string) row[1] == $"{unpackedRepositoryPath}\\");
        Assert.IsTrue((string) row[2] == "master");
        Assert.IsTrue((string) row[3] == "refs/heads/master");
        Assert.IsTrue((bool) row[4] == false);
        Assert.IsTrue((bool) row[5] == false);
        Assert.IsTrue((bool) row[6]);
        Assert.IsNull((string) row[7]);
        Assert.IsTrue((string) row[8] == "7f8eb49e5d872e92c5705a13ba3f04b7c2a1af48");
        Assert.IsTrue((string) row[9] == "initial commit\n");
        Assert.IsTrue((string) row[10] == "initial commit");
        Assert.IsTrue((string) row[11] == "puchacz");
        Assert.IsTrue((string) row[12] == "puchacz");
        Assert.IsTrue((string) row[13] == Path.Combine(unpackedRepositoryPath, ".git\\"));
        Assert.IsTrue((string) row[14] == $"{unpackedRepositoryPath}\\");
        Assert.IsTrue((bool) row[15] == false);
        Assert.IsTrue((bool) row[16] == false);
        Assert.IsTrue((bool) row[17] == false);
        Assert.IsTrue((bool) row[18] == false);
    }

    [TestMethod]
    public async Task WhenBranchesFromRepositoryRetrieved_ShouldPass()
    {
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

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
                    """.Replace("{RepositoryPath}", unpackedRepositoryPath);

        var vm = CreateAndRunVirtualMachine(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == "feature/feature_a");
        Assert.IsTrue((string) row[1] == "refs/heads/feature/feature_a");
        Assert.IsTrue((bool) row[2] == false);
        Assert.IsTrue((bool) row[3] == false);
        Assert.IsTrue((bool) row[4] == false);
        Assert.IsNull((string) row[5]);
        Assert.IsNull((int?) row[6]);
        Assert.IsNull((int?) row[7]);
        Assert.IsTrue((string) row[8] == "99cadbdc90b612de0d82fb2d0450b536b826c3f6");
        Assert.IsTrue((string) row[9] == "modified library_1\n");
        Assert.IsTrue((string) row[10] == "modified library_1");
        Assert.IsTrue((string) row[11] == "puchacz");
        Assert.IsTrue((string) row[12] == "puchacz");
        Assert.IsNull((string) row[13]);
        Assert.IsNull((string) row[14]);
        
        row = result[1];
        
        Assert.IsTrue((string) row[0] == "master");
        Assert.IsTrue((string) row[1] == "refs/heads/master");
        Assert.IsTrue((bool) row[2] == false);
        Assert.IsTrue((bool) row[3] == false);
        Assert.IsTrue((bool) row[4] == true);
        Assert.IsNull((string) row[5]);
        Assert.IsNull((int?) row[6]);
        Assert.IsNull((int?) row[7]);
        Assert.IsTrue((string) row[8] == "e89dc323bbc6099e8314e5dfb4825f530f69caa8");
        Assert.IsTrue((string) row[9] == "add documentation index\n");
        Assert.IsTrue((string) row[10] == "add documentation index");
        Assert.IsTrue((string) row[11] == "puchacz");
        Assert.IsTrue((string) row[12] == "puchacz");
        Assert.IsNull((string) row[13]);
        Assert.IsNull((string) row[14]);
    }

    [TestMethod]
    public async Task WhenCommitsOnBranchRetrieved_ShouldPass()
    {
        var query = @"
            select
                Commit.Sha,
                Commit.Message,
                Commit.MessageShort,
                Commit.Author,
                Commit.Committer
            from #git.repository('{RepositoryPath}') repository cross apply repository.Commits as Commit";
        
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);
        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 5);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == "e89dc323bbc6099e8314e5dfb4825f530f69caa8");
        Assert.IsTrue((string) row[1] == "add documentation index\n");
        Assert.IsTrue((string) row[2] == "add documentation index");
        Assert.IsTrue((string) row[3] == "puchacz");
        Assert.IsTrue((string) row[4] == "puchacz");
        
        row = result[1];
        
        Assert.IsTrue((string) row[0] == "99cadbdc90b612de0d82fb2d0450b536b826c3f6");
        Assert.IsTrue((string) row[1] == "modified library_1\n");
        Assert.IsTrue((string) row[2] == "modified library_1");
        Assert.IsTrue((string) row[3] == "puchacz");
        Assert.IsTrue((string) row[4] == "puchacz");
        
        row = result[2];
        
        Assert.IsTrue((string) row[0] == "f386d45b1c4349314a9def8fa6a48c12b2f923e9");
        Assert.IsTrue((string) row[1] == "add first library\n");
        Assert.IsTrue((string) row[2] == "add first library");
        Assert.IsTrue((string) row[3] == "puchacz");
        Assert.IsTrue((string) row[4] == "puchacz");
        
        row = result[3];
        
        Assert.AreEqual((string) row[0], "9e9333e648c78ac7d5303c18f79e1ae71e1239ec");
        Assert.IsTrue((string) row[1] == "first commit\n");
        Assert.IsTrue((string) row[2] == "first commit");
        Assert.IsTrue((string) row[3] == "puchacz");
        Assert.IsTrue((string) row[4] == "puchacz");
        
        row = result[4];
        
        Assert.IsTrue((string) row[0] == "7f8eb49e5d872e92c5705a13ba3f04b7c2a1af48");
        Assert.IsTrue((string) row[1] == "initial commit\n");
        Assert.IsTrue((string) row[2] == "initial commit");
        Assert.IsTrue((string) row[3] == "puchacz");
        Assert.IsTrue((string) row[4] == "puchacz");
    }
    
    [TestMethod]
    public async Task WhenTagsFromRepositoryRetrieved_ShouldPass()
    {
        var query = @"
            select
                Tag.FriendlyName,
                Tag.CanonicalName,
                Tag.Message,
                Tag.IsAnnotated
            from #git.repository('{RepositoryPath}') repository cross apply repository.Tags as Tag
            where Tag.IsAnnotated = false";
        
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);
        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
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
        
        vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
        
        result = vm.Run();
        
        Assert.IsTrue(result.Count == 1);
        
        row = result[0];
        
        Assert.IsTrue((string) row[0] == "v0.0");
        Assert.IsTrue((string) row[1] == "refs/tags/v0.0");
        Assert.IsTrue((string) row[2] == "Initial release of repository\n");
        Assert.IsTrue((bool) row[3]);
        Assert.IsTrue((string) row[4] == "Initial release of repository\n");
        Assert.IsTrue((string) row[5] == "v0.0");
        Assert.IsTrue((string) row[6] == "7c788313af0638c3ec385440604a7794f7b998e5");
        Assert.IsTrue((string) row[7] == "puchacz");
        Assert.IsTrue((string) row[8] == "puchala.czwa@gmail.com");
        Assert.IsTrue((DateTimeOffset) row[9] == new DateTimeOffset(2024, 11, 02, 9, 39, 40, TimeSpan.FromHours(1)));
    }

    private Task<string> UnpackGitRepositoryAsync(string zippedRepositoryPath, [CallerMemberName] string? testName = null)
    {
        if (testName is null)
            throw new ArgumentNullException(nameof(testName));
        
        if (!File.Exists(zippedRepositoryPath))
            throw new InvalidOperationException("File does not exist.");
        
        var directory = Path.GetDirectoryName(zippedRepositoryPath);
        
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Directory is empty.");
        
        var repositoryPath = Path.Combine(directory, "Repositories", ".TestsExecutions", testName);
        
        if (Directory.Exists(repositoryPath))
            Directory.Delete(repositoryPath, true);
        
        ZipFile.ExtractToDirectory(zippedRepositoryPath, repositoryPath);
        
        if (!Directory.Exists(repositoryPath))
            throw new InvalidOperationException("Directory was not created.");
        
        var fileName = Path.GetFileNameWithoutExtension(zippedRepositoryPath);
        return Task.FromResult(Path.Combine(repositoryPath, fileName));
    }
    
    [TestMethod]
    public async Task WhenStashQueried_ShouldPass()
    {
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);
        
        var query = @"
            select
                Stash.Message
            from #git.repository('{RepositoryPath}') repository cross apply repository.Stashes as Stash";
        
        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
        
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 1);
        
        var row = result[0];
        
        Assert.IsTrue((string) row[0] == "WIP on master: e89dc32 add documentation index");
    }

    [TestMethod]
    public async Task WhenDifferenceBetweenTwoCommits_ShouldPass()
    {
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);
        
        var query = @"
            select
                Difference.Path,
                Difference.ChangeKind,
                Difference.OldPath,
                Difference.OldMode,
                Difference.NewMode,
                Difference.OldSha,
                Difference.NewSha
            from #git.repository('{RepositoryPath}') repository cross apply repository.DifferenceBetween(repository.CommitFrom('e89dc32'), repository.CommitFrom('99cadbd')) as Difference";
        
        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
        
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
        var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository4ZipPath);
        
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
        
        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath));
        
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

    static GitToSqlTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new GitSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string Repository1ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository1.zip");
    
    private static string Repository2ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository2.zip");
    
    private static string Repository3ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository3.zip");
    
    private static string Repository4ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository4.zip");

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
}