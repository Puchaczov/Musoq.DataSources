using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal interface IGitTagReader
{
    string Backend { get; }

    void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag);
}

internal readonly record struct GitTagRecord(
    string RepositoryPath,
    string FriendlyName,
    string CanonicalName,
    string? Message,
    bool IsAnnotated,
    AnnotationEntity? Annotation,
    string? CommitSha);

internal sealed class LibGit2TagReader : IGitTagReader
{
    public string Backend => "libgit2";

    public void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag)
    {
        using var repository = createRepository(repositoryPath);
        foreach (var tag in repository.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The runtime can report Commit.Sha without TagEntity.Commit. Keep tag capabilities until nested
            // dependency reporting becomes exact, while still skipping the public message scalar when unselected.
            var needsAnnotation = true;
            var annotation = needsAnnotation && tag.Annotation is { } value ? new AnnotationEntity(value, repository) : null;
            var record = new GitTagRecord(
                repository.Info.Path,
                tag.FriendlyName,
                tag.CanonicalName,
                annotation?.Message,
                needsAnnotation || projection.Includes(nameof(TagEntity.IsAnnotated)) ? tag.IsAnnotated : false,
                annotation,
                (tag.Target as Commit)?.Sha);
            if (!onTag(record))
                break;
        }
    }
}

/// <summary>Safe CLI candidate for tag enumeration. Production selection remains benchmark-qualified.</summary>
internal sealed class GitCliTagReader : IGitTagReader
{
    public string Backend => "git-cli";

    public void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag)
    {
        var arguments = new[]
        {
            "for-each-ref",
            "--format=%1e%(refname)%00%(refname:short)%00%(objecttype)%00%(objectname)%00%(*objecttype)%00%(*objectname)%00%(contents)%00%(taggername)%00%(taggeremail:trim)%00%(taggerdate:iso-strict)%00",
            "refs/tags"
        };
        using var process = GitCliProcess.Start(repositoryPath, GitHistoryBackendOptions.Default, arguments, cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var completedNaturally = true;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = reader.ReadToken();
            if (header is null)
                break;
            header = NormalizeRecordToken(header);
            if (header.Length == 0)
                continue;
            if (!header.StartsWith('\u001e'))
                throw new InvalidDataException("Git tag protocol lost its record delimiter.");

            var canonicalName = header[1..];
            var friendlyName = ReadRequired(reader, "tag friendly name");
            var objectType = ReadRequired(reader, "tag object type");
            var objectSha = ReadRequired(reader, "tag object id");
            var peeledObjectType = ReadRequired(reader, "peeled tag object type");
            var peeledObjectSha = ReadRequired(reader, "peeled tag object id");
            var message = ReadRequired(reader, "tag message");
            var taggerName = ReadRequired(reader, "tagger name");
            var taggerEmail = ReadRequired(reader, "tagger email");
            var taggerWhen = ReadRequired(reader, "tagger date");
            var isAnnotated = string.Equals(objectType, "tag", StringComparison.Ordinal);
            var annotation = isAnnotated
                ? new AnnotationEntity(
                    message,
                    friendlyName,
                    objectSha,
                    CreateTagger(taggerName, taggerEmail, taggerWhen))
                : null;
            var commitSha = string.Equals(objectType, "commit", StringComparison.Ordinal)
                ? objectSha
                : string.Equals(peeledObjectType, "commit", StringComparison.Ordinal) ? peeledObjectSha : null;
            if (!onTag(new GitTagRecord(
                    repositoryPath,
                    friendlyName,
                    canonicalName,
                    isAnnotated ? message : null,
                    isAnnotated,
                    annotation,
                    string.IsNullOrWhiteSpace(commitSha) ? null : commitSha)))
            {
                completedNaturally = false;
                process.Stop();
                break;
            }
        }

        if (completedNaturally)
            process.Complete();
    }

    private static TaggerEntity? CreateTagger(string name, string email, string when) =>
        string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email)
            ? null
            : new TaggerEntity(name, email,
                DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : default);

    private static string NormalizeRecordToken(string token) => token.TrimStart('\r', '\n');

    private static string ReadRequired(GitNulDelimitedUtf8Reader reader, string field) =>
        reader.ReadToken() ?? throw new InvalidDataException($"Git tag output ended while reading the {field}.");
}

internal interface IGitRemoteReader
{
    string Backend { get; }

    void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitRemoteRecord, bool> onRemote);
}

internal readonly record struct GitRemoteRecord(string Name, string Url, string? PushUrl);

internal sealed class LibGit2RemoteReader : IGitRemoteReader
{
    public string Backend => "libgit2";

    public void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitRemoteRecord, bool> onRemote)
    {
        using var repository = createRepository(repositoryPath);
        foreach (var remote in repository.Network.Remotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!onRemote(new GitRemoteRecord(remote.Name, remote.Url, remote.PushUrl)))
                break;
        }
    }
}

/// <summary>Safe CLI candidate that reads local configuration only and never alters it.</summary>
internal sealed class GitCliRemoteReader : IGitRemoteReader
{
    public string Backend => "git-cli";

    public void Read(string repositoryPath, GitProjection projection, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitRemoteRecord, bool> onRemote)
    {
        using var process = GitCliProcess.Start(
            repositoryPath,
            GitHistoryBackendOptions.Default,
            ["config", "--null", "--local", "--list"],
            cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var remotes = new Dictionary<string, (string? Url, string? PushUrl)>(StringComparer.Ordinal);
        while (reader.ReadToken() is { } item)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var separator = item.IndexOf('\n');
            if (separator <= 0)
                continue;
            var key = item[..separator];
            var value = item[(separator + 1)..];
            const string prefix = "remote.";
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var propertySeparator = key.LastIndexOf('.');
            if (propertySeparator <= prefix.Length || propertySeparator == key.Length - 1)
                continue;
            var name = key[prefix.Length..propertySeparator];
            var property = key[(propertySeparator + 1)..];
            if (!property.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                !property.Equals("pushurl", StringComparison.OrdinalIgnoreCase))
                continue;
            remotes.TryGetValue(name, out var remote);
            remotes[name] = property.Equals("url", StringComparison.OrdinalIgnoreCase)
                ? (remote.Url ?? value, remote.PushUrl)
                : (remote.Url, remote.PushUrl ?? value);
        }
        process.Complete();

        foreach (var (name, remote) in remotes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remote.Url is not null && !onRemote(new GitRemoteRecord(name, remote.Url, remote.PushUrl)))
                break;
        }
    }
}
