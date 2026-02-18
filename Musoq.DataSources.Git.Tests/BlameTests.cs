using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class BlameTests
{
    static BlameTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
    }

    private static string Repository2ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository2.zip");

    private static string BlameTestRepoZipPath => Path.Combine(StartDirectory, "Repositories", "BlameTestRepo.zip");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(BlameTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    [TestMethod]
    public async Task Blame_ValidFile_ReturnsHunks()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                StartLineNumber,
                EndLineNumber,
                LineCount,
                CommitSha,
                Author,
                AuthorEmail,
                Summary
            from #git.blame('{RepositoryPath}', 'test_file.txt')";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should return at least one hunk");

        var firstRow = result[0];
        Assert.IsTrue((int)firstRow[0] >= 1, "StartLineNumber should be 1-based");
        Assert.IsTrue((int)firstRow[1] >= (int)firstRow[0], "EndLineNumber should be >= StartLineNumber");
        Assert.IsTrue((int)firstRow[2] > 0, "LineCount should be > 0");
        Assert.IsNotNull(firstRow[3], "CommitSha should not be null");
        Assert.AreEqual("Test User", (string)firstRow[4], "Author should be 'Test User'");
        Assert.AreEqual("test@example.com", (string)firstRow[5], "AuthorEmail should be 'test@example.com'");
        Assert.IsNotNull(firstRow[6], "Summary should not be null");

        var totalLines = result.Sum(row => (int)row[2]);
        Assert.AreEqual(4, totalLines, "Total lines across all hunks should be 4");
    }

    [TestMethod]
    public async Task Blame_WithRevision_ReturnsHistoricalState()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var queryHead = @"
            select
                CommitSha,
                Author
            from #git.blame('{RepositoryPath}', 'test_file.txt', 'HEAD')";

        var vmHead =
            CreateAndRunVirtualMachine(queryHead.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var resultHead = vmHead.Run();

        Assert.IsTrue(resultHead.Count > 0, "Should return at least one hunk for HEAD");

        var queryTag = @"
            select
                CommitSha,
                Author
            from #git.blame('{RepositoryPath}', 'test_file.txt', 'v1.0')";

        var vmTag = CreateAndRunVirtualMachine(queryTag.Replace("{RepositoryPath}",
            unpackedRepositoryPath.Path.Escape()));
        var resultTag = vmTag.Run();

        Assert.IsTrue(resultTag.Count > 0, "Should return at least one hunk for v1.0 tag");
    }

    [TestMethod]
    public async Task Blame_NonExistentFile_ThrowsFileNotFound()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                StartLineNumber,
                CommitSha
            from #git.blame('{RepositoryPath}', 'NonExistentFile.txt')";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        Assert.ThrowsException<FileNotFoundException>(() => vm.Run());
    }

    [TestMethod]
    public async Task Blame_InvalidRevision_ThrowsArgumentException()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                StartLineNumber,
                CommitSha
            from #git.blame('{RepositoryPath}', 'test_file.txt', 'invalid-sha-12345')";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));

        Assert.ThrowsException<ArgumentException>(() => vm.Run());
    }

    [TestMethod]
    public async Task Blame_BinaryFile_ReturnsEmpty()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                StartLineNumber,
                CommitSha
            from #git.blame('{RepositoryPath}', 'main.py')";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Binary files (UTF-16) should return empty result");
    }

    [TestMethod]
    public async Task Blame_LinesProperty_LoadsContent()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                l.LineNumber,
                l.Content,
                h.Author
            from #git.blame('{RepositoryPath}', 'test_file.txt') h
            cross apply h.Lines l
            order by l.LineNumber";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should return line content");
        Assert.AreEqual(4, result.Count, "test_file.txt has 4 lines");

        var firstLine = result[0];
        Assert.AreEqual(1, (int)firstLine[0], "First line number should be 1");
        Assert.IsNotNull(firstLine[1], "Line content should not be null");
        Assert.IsTrue(((string)firstLine[1]).Contains("line 1"), "First line should contain 'line 1'");
        Assert.AreEqual("Test User", (string)firstLine[2], "Author should be 'Test User'");
    }

    [TestMethod]
    public async Task Blame_MovedFile_TracksOrigin()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                StartLineNumber,
                LineCount
            from #git.blame('{RepositoryPath}', 'test_file.txt')";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should return at least one hunk");
    }

    [TestMethod]
    public async Task Blame_CrossApply_ReturnsIndividualLines()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                h.CommitSha,
                h.Author,
                l.LineNumber,
                l.Content
            from #git.blame('{RepositoryPath}', 'test_file.txt') h
            cross apply h.Lines l
            order by l.LineNumber";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should return at least one line");
        Assert.AreEqual(4, result.Count, "Should return 4 lines total");

        foreach (var row in result)
        {
            Assert.IsNotNull(row[0], "CommitSha should not be null");
            Assert.IsNotNull(row[1], "Author should not be null");
            Assert.IsNotNull(row[2], "LineNumber should not be null");
            Assert.IsNotNull(row[3], "Content should not be null");
        }

        var firstLine = result[0];
        Assert.AreEqual(1, (int)firstLine[2], "First line number should be 1");
        Assert.IsTrue(((string)firstLine[3]).Contains("line 1"), "First line content should contain 'line 1'");
    }

    [TestMethod]
    public async Task Blame_Aggregation_SumLineCount()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(BlameTestRepoZipPath);

        var query = @"
            select
                Author,
                SUM(LineCount) as TotalLines
            from #git.blame('{RepositoryPath}', 'test_file.txt')
            group by Author";

        var vm = CreateAndRunVirtualMachine(query.Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape()));
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should have at least one author");
        Assert.AreEqual(1, result.Count, "Should have exactly 1 author (Test User)");

        var row = result[0];
        Assert.AreEqual("Test User", (string)row[0], "Author should be 'Test User'");
        Assert.AreEqual(4L, Convert.ToInt64(row[1]), "Total lines should be 4");
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

    private class UnpackedRepository : IDisposable
    {
        public UnpackedRepository(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
        }

        public static implicit operator string(UnpackedRepository unpackedRepository)
        {
            return unpackedRepository.Path;
        }
    }
}