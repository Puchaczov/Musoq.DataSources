using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class BlameRowsSource : AsyncRowsSourceBase<BlameHunkEntity>
{
    private readonly string _repositoryPath;
    private readonly string _filePath;
    private readonly string _revision;
    private readonly Func<string, Repository> _createRepository;

    public BlameRowsSource(
        string repositoryPath,
        string filePath,
        string revision,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken)
        : base(cancellationToken)
    {
        _repositoryPath = repositoryPath;
        _filePath = filePath;
        _revision = revision;
        _createRepository = createRepository;
    }

    protected override Task CollectChunksAsync(
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_repositoryPath))
        {
            throw new DirectoryNotFoundException($"Repository path '{_repositoryPath}' does not exist");
        }

        var repository = _createRepository(_repositoryPath);
        
        // Resolve the commit from the revision
        Commit? commit = null;
        
        try
        {
            // Try to lookup as a commit directly
            var gitObject = repository.Lookup(_revision);
            
            if (gitObject == null)
            {
                throw new ArgumentException($"Invalid revision '{_revision}': not found", nameof(_revision));
            }
            
            // Try to peel to a commit (handles tags and other references)
            var peeledCommit = gitObject.Peel<Commit>();
            if (peeledCommit != null)
            {
                commit = peeledCommit;
            }
            else
            {
                throw new ArgumentException($"Invalid revision '{_revision}': does not point to a commit", nameof(_revision));
            }
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Invalid revision '{_revision}': {ex.Message}", nameof(_revision), ex);
        }

        // Check if file exists at this revision
        var treeEntry = commit[_filePath];
        if (treeEntry == null)
        {
            throw new FileNotFoundException($"File '{_filePath}' does not exist at revision '{_revision}'");
        }

        // Check if it's a binary file
        if (treeEntry.TargetType == TreeEntryTargetType.Blob)
        {
            var blob = (Blob)treeEntry.Target;
            if (blob.IsBinary)
            {
                // Return empty for binary files
                return Task.CompletedTask;
            }
        }

        // Get blame information
        BlameHunkCollection blameHunks;
        try
        {
            blameHunks = repository.Blame(_filePath, new BlameOptions { StartingAt = commit });
        }
        catch
        {
            // Return empty on any blame errors
            return Task.CompletedTask;
        }

        var chunk = new List<IObjectResolver>(100);

        foreach (var hunk in blameHunks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var entity = new BlameHunkEntity(hunk, repository, _filePath);
            chunk.Add(new EntityResolver<BlameHunkEntity>(
                entity,
                BlameHunkEntity.NameToIndexMap,
                BlameHunkEntity.IndexToObjectAccessMap
            ));

            if (chunk.Count >= 100)
            {
                chunkedSource.Add(chunk.ToArray(), cancellationToken);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            chunkedSource.Add(chunk.ToArray(), cancellationToken);
        }

        return Task.CompletedTask;
    }
}
