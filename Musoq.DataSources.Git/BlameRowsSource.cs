using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

/// <summary>Streams detached blame hunk metadata; file content remains lazy on <see cref="BlameHunkEntity.Lines"/>.</summary>
internal sealed class BlameRowsSource : GitDiagnosticRowsSourceBase<BlameHunkEntity>
{
    private readonly Func<string, Repository> _createRepository;
    private readonly string _filePath;
    private readonly string _repositoryPath;
    private readonly string _revision;
    private readonly GitProjection _projection;

    public BlameRowsSource(
        string repositoryPath,
        string filePath,
        string revision,
        Func<string, Repository> createRepository,
        SourceExecutionContext executionContext)
        : base(executionContext, "git.blame", repositoryPath)
    {
        _repositoryPath = repositoryPath;
        _filePath = filePath;
        _revision = revision;
        _createRepository = createRepository;
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
    }

    protected override long CollectRows(DiagnosticChunkWriter<BlameHunkEntity> writer, CancellationToken cancellationToken)
    {
        if (ReadContext?.RequiresCli == true)
        {
            var rows = new List<BlameHunkEntity>(128);
            long written = 0;
            foreach (var hunk in ReadContext.Blame(_filePath, _revision))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_projection.NativePatterns?.Matches(hunk, GitPatternColumns.BlameHunkEntity) == false) continue;
                rows.Add(hunk);
                if (rows.Count == 128) written += WriteChunk(writer, rows, written);
            }
            return written + WriteChunk(writer, rows, written);
        }
        if (!Directory.Exists(ReadContext?.Location.ExecutionRoot ?? _repositoryPath))
            throw new DirectoryNotFoundException($"Repository path '{_repositoryPath}' does not exist.");

        using var repository = _createRepository(_repositoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        var gitObject = repository.Lookup(_revision) ??
                        throw new ArgumentException($"Invalid revision '{_revision}': not found.", nameof(_revision));
        var commit = gitObject.Peel<Commit>() ??
                     throw new ArgumentException($"Invalid revision '{_revision}': does not point to a commit.", nameof(_revision));
        var treeEntry = commit[_filePath] ??
                        throw new FileNotFoundException($"File '{_filePath}' does not exist at revision '{_revision}'.");

        if (treeEntry.TargetType == TreeEntryTargetType.Blob && ((Blob)treeEntry.Target).IsBinary)
            return 0;

        BlameHunkCollection blameHunks;
        try
        {
            blameHunks = repository.Blame(_filePath, new BlameOptions { StartingAt = commit });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Git could not calculate blame for '{_filePath}' at revision '{_revision}'.", exception);
        }

        var chunk = new List<BlameHunkEntity>(128);
        var blobSha = treeEntry.Target.Sha;
        var fileContent = new GitBlameFileContent(() =>
        {
            using var contentRepository = _createRepository(_repositoryPath);
            var blob = contentRepository.Lookup<Blob>(blobSha) ?? throw new FileNotFoundException("Blamed blob is no longer available.");
            using var content = blob.GetContentStream();
            using var text = new StreamReader(content);
            return text.ReadToEnd();
        }, Context.EndWorkToken, Context.Diagnostics);
        long rowsRead = 0;
        foreach (var hunk in blameHunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_projection.NativePatterns?.Matches(new GitNativePatterns.BlameInput(hunk, _filePath), GitNativePatterns.Blame) == false) continue;
            _projection.NativePatterns?.Constructed();
            chunk.Add(new BlameHunkEntity(hunk, _repositoryPath, _filePath, _projection, commit.Sha, fileContent));
            if (chunk.Count == 128)
                rowsRead += WriteChunk(writer, chunk, rowsRead);
        }

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        return rowsRead;
    }
}
