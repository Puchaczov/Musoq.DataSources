using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Musoq.DataSources.OsAndGitTests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.OsAndGitTests;

[TestClass]
public class OsAndGitToSqlTests
{
    static OsAndGitToSqlTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private static string RepositoriesDirectory => Path.Combine(StartDirectory, "Repositories");

    private static string Repository1ZipPath => Path.Combine(RepositoriesDirectory, "Repository1.zip");

    private static string Repository5ZipPath => Path.Combine(RepositoriesDirectory, "Repository5.zip");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(OsAndGitToSqlTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    [TestMethod]
    public async Task WhenSelectCommitsFromMultipleRepositories_ShouldPass()
    {
        using var firstRepository = await UnpackGitRepositoryAsync(Repository1ZipPath, 1, "tm11");
        using var secondRepository = await UnpackGitRepositoryAsync(Repository5ZipPath, 1, "tm12");

        var query = """
                    with ProjectsToAnalyze as (
                        select 
                            dir.FullName as FullName,
                            dir.Parent.Name as Name
                        from #os.directories('{RepositoriesDirectory}', true) dir
                        where 
                            dir.Name = '.git'
                    )
                    select 
                        p.Name,
                        c.Sha
                    from ProjectsToAnalyze p cross apply #git.repository(p.FullName) r cross apply r.Commits c
                    order by c.CommittedWhen, p.Name
                    """;

        query = query.Replace("{RepositoriesDirectory}",
            new FileInfo(firstRepository).Directory!.Parent!.FullName.Escape());

        {
            var vm = CreateAndRunVirtualMachine(query);

            var table = vm.Run();

            Assert.AreEqual(9, table.Count);

            Assert.AreEqual("Repository1", table[0][0]);
            Assert.AreEqual("789f584ce162424f61b33e020e2138aad47e60ba", table[0][1]);

            Assert.AreEqual("Repository5", table[1][0]);
            Assert.AreEqual("789f584ce162424f61b33e020e2138aad47e60ba", table[1][1]);

            Assert.AreEqual("Repository5", table[2][0]);
            Assert.AreEqual("595b3f0f51071f84909861e5abc15225a4ef4555", table[2][1]);

            Assert.AreEqual("Repository5", table[3][0]);
            Assert.AreEqual("02c2e53d8712210a4254fc9bd6ee5548a0f1d211", table[3][1]);

            Assert.AreEqual("Repository5", table[4][0]);
            Assert.AreEqual("3250d89501e0569115a5cda34807e15ba7de0aa6", table[4][1]);

            Assert.AreEqual("Repository5", table[5][0]);
            Assert.AreEqual("bf8542548c686f98d3c562d2fc78259640d07cbb", table[5][1]);

            Assert.AreEqual("Repository5", table[6][0]);
            Assert.AreEqual("655595cfb4bdfc4e42b9bb80d48212c2dca95086", table[6][1]);

            Assert.AreEqual("Repository5", table[7][0]);
            Assert.AreEqual("fb24727b684a511e7f93df2910e4b280f6b9072f", table[7][1]);

            Assert.AreEqual("Repository5", table[8][0]);
            Assert.AreEqual("389642ba15392c4540e82628bdff9c99dc6f7923", table[8][1]);
        }
    }

    [TestMethod]
    public async Task WhenCommitsCountPerRepositoryCalculated_ShouldPass()
    {
        using var firstRepository = await UnpackGitRepositoryAsync(Repository1ZipPath, 2, "tm21");
        using var secondRepository = await UnpackGitRepositoryAsync(Repository5ZipPath, 2, "tm22");

        var query = """
                    with ProjectsToAnalyze as (
                        select 
                            dir.FullName as FullName,
                            dir.Parent.Name as Name
                        from #os.directories('{RepositoriesDirectory}', true) dir
                        where 
                            dir.Name = '.git'
                    )
                    select 
                        p.Name,
                        r.Count(c.Sha) as CommitsCount
                    from ProjectsToAnalyze p cross apply #git.repository(p.FullName) r cross apply r.Commits c
                    group by p.Name
                    order by p.Name
                    """;

        query = query.Replace("{RepositoriesDirectory}",
            new FileInfo(firstRepository).Directory!.Parent!.FullName.Escape());

        {
            var vm = CreateAndRunVirtualMachine(query);

            var table = vm.Run();

            Assert.AreEqual(2, table.Count);

            Assert.AreEqual("Repository1", table[0][0]);
            Assert.AreEqual(1, table[0][1]);

            Assert.AreEqual("Repository5", table[1][0]);
            Assert.AreEqual(8, table[1][1]);
        }
    }

    private Task<UnpackedRepository> UnpackGitRepositoryAsync(string zippedRepositoryPath, int method,
        [CallerMemberName] string? testName = null)
    {
        if (testName is null)
            throw new ArgumentNullException(nameof(testName));

        if (!File.Exists(zippedRepositoryPath))
            throw new InvalidOperationException("File does not exist.");

        var repositoryPath = Path.Combine(Path.GetTempPath(), "mqogt", method.ToString(), testName);

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
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(),
            new OsAndGitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private class UnpackedRepository : IDisposable
    {
        private static readonly ConcurrentDictionary<string, int> IsCounter = new();

        public UnpackedRepository(string path)
        {
            Path = path;
            IsCounter.AddOrUpdate(path, 1, (_, value) => value + 1);
        }

        private string Path { get; }

        public void Dispose()
        {
            if (!IsCounter.TryGetValue(Path, out var value)) return;

            if (value == 1)
                IsCounter.TryRemove(Path, out _);
            else
                IsCounter.AddOrUpdate(Path, value - 1, (_, _) => value - 1);
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