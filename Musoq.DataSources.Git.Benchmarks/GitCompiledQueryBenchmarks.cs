using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// End-to-end compiled-query smoke shapes. They deliberately execute the normal Musoq evaluator rather than
/// exercising row sources directly, so source improvements cannot hide compilation/runtime regressions.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public sealed class GitCompiledQueryBenchmarks
{
    private CompiledQuery _commits = null!;
    private CompiledQuery _fileHistory = null!;
    private CompiledQuery _refs = null!;
    private CompiledQuery _status = null!;

    [GlobalSetup]
    public void Setup()
    {
        var corpus = GitBenchmarkCorpusFactory.Ensure(GitBenchmarkProfile.Smoke);
        var repository = corpus.RepositoryPath.Replace("\\", "\\\\", StringComparison.Ordinal);
        _fileHistory = Compile($"select CommitSha, FilePath, ChangeType, OldPath from #git.filehistory('{repository}', '*.cs', 5000)");
        _commits = Compile($"select Sha, Author, CommittedWhen from #git.commits('{repository}')");
        _refs = Compile($"select FriendlyName, CanonicalName from #git.branches('{repository}')");
        _status = Compile($"select FilePath, State from #git.status('{repository}')");
    }

    [Benchmark]
    public int FileHistoryWildcard() => _fileHistory.Run().Count;

    [Benchmark]
    public int CommitsScan() => _commits.Run().Count;

    [Benchmark]
    public int Branches() => _refs.Run().Count;

    [Benchmark]
    public int Status() => _status.Run().Count;

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"GitBenchmark{Guid.NewGuid():N}",
            new BenchmarkGitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private sealed class BenchmarkGitSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => new GitSchema();
    }
}
