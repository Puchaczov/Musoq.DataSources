using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal interface IGitTagReader
{
    string Backend { get; }

    IEnumerable<GitTagRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitTagReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken);

    void Read(string repositoryPath, GitReferenceBackendOptions options, GitProjection projection, GitTagReadQuery query, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag);
}

internal readonly record struct GitTagReadQuery(
    string? ExactCanonicalName,
    string? ExactFriendlyName,
    IReadOnlyList<string>? ExactCanonicalNames,
    IReadOnlyList<string>? ExactFriendlyNames,
    string? CanonicalNameAfter,
    bool CanonicalNameAfterInclusive,
    Action? OnCursorSkipped)
{
    public static GitTagReadQuery Empty { get; } = new(null, null, null, null, null, false, null);
}

internal static class GitTagReaderExtensions
{
    public static void Read(
        this IGitTagReader reader,
        string repositoryPath,
        GitProjection projection,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag)
    {
        reader.Read(repositoryPath, GitReferenceBackendOptions.Default, projection, GitTagReadQuery.Empty,
            createRepository, cancellationToken, onTag);
    }
}

internal readonly record struct GitTagRecord(
    string RepositoryPath,
    string FriendlyName,
    string CanonicalName,
    string? Message,
    bool IsAnnotated,
    AnnotationEntity? Annotation,
    string? TargetSha,
    string? CommitSha);

internal sealed class LibGit2TagReader : IGitTagReader
{
    public string Backend => "libgit2";

    public IEnumerable<GitTagRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitTagReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken)
    {
        using var repository = createRepository(repositoryPath);
        foreach (var tag in EnumerateTags(repository, query))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (query.CanonicalNameAfter is not null)
            {
                var comparison = string.Compare(tag.CanonicalName, query.CanonicalNameAfter, StringComparison.Ordinal);
                if (comparison < 0 || comparison == 0 && !query.CanonicalNameAfterInclusive)
                {
                    query.OnCursorSkipped?.Invoke();
                    continue;
                }
            }
            var needsAnnotation = !projection.IsAccepted ||
                                  projection.Includes(nameof(TagEntity.Message)) ||
                                  projection.Includes(nameof(TagEntity.Annotation));
            var needsTarget = !projection.IsAccepted ||
                              projection.Includes(nameof(TagEntity.TargetSha)) ||
                              projection.Includes(nameof(TagEntity.Commit));
            var target = tag.PeeledTarget ?? tag.Target as GitObject;
            var targetSha = needsTarget ? target?.Sha : null;
            var annotation = needsAnnotation && tag.Annotation is { } value ? new AnnotationEntity(value, repository) : null;
            var record = new GitTagRecord(
                repository.Info.Path,
                tag.FriendlyName,
                tag.CanonicalName,
                annotation?.Message,
                !projection.IsAccepted || projection.Includes(nameof(TagEntity.IsAnnotated)) ? tag.IsAnnotated : false,
                annotation,
                targetSha,
                needsTarget ? (target as Commit)?.Sha : null);
            yield return record;
        }
    }

    private static IEnumerable<Tag> EnumerateTags(Repository repository, GitTagReadQuery query)
    {
        var candidates = ExactFriendlyNames(query).ToArray();
        if (candidates.Length == 0)
        {
            foreach (var tag in repository.Tags)
                yield return tag;
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var friendlyName = candidate.StartsWith("refs/tags/", StringComparison.Ordinal)
                ? candidate["refs/tags/".Length..]
                : candidate;
            if (!seen.Add(friendlyName))
                continue;

            var tag = repository.Tags[friendlyName];
            if (tag is not null)
                yield return tag;
        }
    }

    private static IEnumerable<string> ExactFriendlyNames(GitTagReadQuery query)
    {
        if (query.ExactFriendlyName is not null)
            yield return query.ExactFriendlyName;
        if (query.ExactCanonicalName is not null)
            yield return query.ExactCanonicalName;
        if (query.ExactFriendlyNames is not null)
        {
            foreach (var name in query.ExactFriendlyNames)
                yield return name;
        }
        if (query.ExactCanonicalNames is not null)
        {
            foreach (var name in query.ExactCanonicalNames)
                yield return name;
        }
    }

    public void Read(string repositoryPath, GitReferenceBackendOptions options, GitProjection projection, GitTagReadQuery query, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag)
    {
        foreach (var record in ReadStreaming(repositoryPath, options, projection, query, createRepository, cancellationToken))
        {
            if (!onTag(record))
                break;
        }
    }
}

/// <summary>Streaming CLI reader for local tag enumeration with projection-aware protocols.</summary>
internal sealed class GitCliTagReader : IGitTagReader
{
    private readonly IGitCliProcessFactory _processFactory;

    public GitCliTagReader()
        : this(GitCliProcessFactory.Default)
    {
    }

    internal GitCliTagReader(IGitCliProcessFactory processFactory)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    }

    public string Backend => "git-cli";

    public void Read(string repositoryPath, GitReferenceBackendOptions options, GitProjection projection, GitTagReadQuery query, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitTagRecord, bool> onTag)
    {
        foreach (var record in ReadStreaming(repositoryPath, options, projection, query, createRepository, cancellationToken))
        {
            if (!onTag(record))
                break;
        }
    }

    public IEnumerable<GitTagRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitTagReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken)
    {
        var needsMetadata = !projection.IsAccepted ||
                            projection.Includes(nameof(TagEntity.Message)) ||
                            projection.Includes(nameof(TagEntity.Annotation));
        var needsTarget = !projection.IsAccepted ||
                          projection.Includes(nameof(TagEntity.TargetSha)) ||
                          projection.Includes(nameof(TagEntity.Commit));
        var needsType = !projection.IsAccepted ||
                        projection.Includes(nameof(TagEntity.IsAnnotated)) ||
                        projection.RequiresNestedReferenceCapabilities;
        var mode = needsMetadata
            ? TagProtocolMode.Metadata
            : needsTarget
                ? TagProtocolMode.Target
                : needsType
                    ? TagProtocolMode.Type
                    : TagProtocolMode.Identity;
        var arguments = new List<string>
        {
            "for-each-ref",
            "--sort=refname",
            "--format=" + FormatFor(mode)
        };
        var exactPatterns = ExactPatterns(query).ToArray();
        if (exactPatterns.Length == 0)
            arguments.Add("refs/tags");
        else
            arguments.AddRange(exactPatterns);
        cancellationToken.ThrowIfCancellationRequested();
        using var process = _processFactory.Start(
            new GitCliProcessRequest(
                repositoryPath,
                options.Executable,
                GitReferenceBackendOptions.BackendSettingName,
                arguments,
                GitCliProcessEnvironment.Default),
            cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var completedNaturally = false;
        try
        {
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
                var objectType = mode == TagProtocolMode.Identity ? null : ReadRequired(reader, "tag object type");
                var objectSha = ReadRequired(reader, "tag object id");
                var peeledObjectType = mode is TagProtocolMode.Target or TagProtocolMode.Metadata
                    ? ReadRequired(reader, "peeled tag object type")
                    : null;
                var peeledObjectSha = mode is TagProtocolMode.Target or TagProtocolMode.Metadata
                    ? ReadRequired(reader, "peeled tag object id")
                    : null;
                var message = mode == TagProtocolMode.Metadata ? ReadRequired(reader, "tag message") : null;
                var taggerName = mode == TagProtocolMode.Metadata ? ReadRequired(reader, "tagger name") : null;
                var taggerEmail = mode == TagProtocolMode.Metadata ? ReadRequired(reader, "tagger email") : null;
                var taggerWhen = mode == TagProtocolMode.Metadata ? ReadRequired(reader, "tagger date") : null;

                if (query.CanonicalNameAfter is not null)
                {
                    var comparison = string.Compare(canonicalName, query.CanonicalNameAfter, StringComparison.Ordinal);
                    if (comparison < 0 || comparison == 0 && !query.CanonicalNameAfterInclusive)
                    {
                        query.OnCursorSkipped?.Invoke();
                        continue;
                    }
                }
                var isAnnotated = string.Equals(objectType, "tag", StringComparison.Ordinal);
                var annotation = isAnnotated
                    ? mode == TagProtocolMode.Metadata
                        ? new AnnotationEntity(
                            message,
                            friendlyName,
                            objectSha,
                            CreateTagger(taggerName!, taggerEmail!, taggerWhen!))
                        : new AnnotationEntity(null, friendlyName, objectSha, null)
                    : null;
                var targetSha = mode is TagProtocolMode.Target or TagProtocolMode.Metadata
                    ? string.IsNullOrWhiteSpace(peeledObjectSha) ? objectSha : peeledObjectSha
                    : null;
                var commitSha = string.Equals(objectType, "commit", StringComparison.Ordinal)
                    ? objectSha
                    : string.Equals(peeledObjectType, "commit", StringComparison.Ordinal) ? peeledObjectSha :
                    mode is TagProtocolMode.Identity or TagProtocolMode.Type ? objectSha : null;
                yield return new GitTagRecord(
                    repositoryPath,
                    friendlyName,
                    canonicalName,
                    isAnnotated && message is not null ? message : null,
                    mode is not TagProtocolMode.Identity && isAnnotated,
                    annotation,
                    targetSha,
                    string.IsNullOrWhiteSpace(commitSha) ? null : commitSha);
            }

            process.Complete();
            completedNaturally = true;
        }
        finally
        {
            if (!completedNaturally)
                process.Stop();
        }
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

    private static string FormatFor(TagProtocolMode mode) => mode switch
    {
        TagProtocolMode.Identity => "%1e%(refname)%00%(refname:short)%00%(objectname)%00",
        TagProtocolMode.Type => "%1e%(refname)%00%(refname:short)%00%(objecttype)%00%(objectname)%00",
        TagProtocolMode.Target => "%1e%(refname)%00%(refname:short)%00%(objecttype)%00%(objectname)%00%(*objecttype)%00%(*objectname)%00",
        _ => "%1e%(refname)%00%(refname:short)%00%(objecttype)%00%(objectname)%00%(*objecttype)%00%(*objectname)%00%(contents)%00%(taggername)%00%(taggeremail:trim)%00%(taggerdate:iso-strict)%00"
    };

    private static IEnumerable<string> ExactPatterns(GitTagReadQuery query)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Candidates(query))
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                candidate.All(character => character is not ('*' or '?' or '[')) &&
                seen.Add(candidate))
                yield return candidate;
        }

        static IEnumerable<string> Candidates(GitTagReadQuery value)
        {
            if (value.ExactCanonicalName is not null)
                yield return value.ExactCanonicalName;
            if (value.ExactFriendlyName is not null)
                yield return "refs/tags/" + value.ExactFriendlyName;
            if (value.ExactCanonicalNames is not null)
            {
                foreach (var item in value.ExactCanonicalNames)
                    yield return item;
            }
            if (value.ExactFriendlyNames is not null)
            {
                foreach (var item in value.ExactFriendlyNames)
                    yield return "refs/tags/" + item;
            }
        }
    }

    private enum TagProtocolMode
    {
        Identity,
        Type,
        Target,
        Metadata
    }
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

internal interface IGitRemoteTagReader
{
    string Backend { get; }

    void Read(
        string repositoryPath,
        string remoteName,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitRemoteTagReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitRemoteTagRecord, bool> onTag);
}

internal readonly record struct GitRemoteTagReadQuery(
    string? ExactCanonicalName,
    string? ExactFriendlyName,
    IReadOnlyList<string>? ExactCanonicalNames,
    IReadOnlyList<string>? ExactFriendlyNames,
    string? CanonicalNameAfter,
    bool CanonicalNameAfterInclusive,
    Action? OnCursorSkipped)
{
    public static GitRemoteTagReadQuery Empty { get; } = new(null, null, null, null, null, false, null);
}

internal readonly record struct GitRemoteTagRecord(
    string RepositoryPath,
    string RemoteName,
    string RemoteUrl,
    string FriendlyName,
    string CanonicalName,
    string ObjectSha,
    string? PeeledSha,
    bool IsAnnotated);

internal sealed class GitCliRemoteTagReader : IGitRemoteTagReader
{
    private readonly IGitCliProcessFactory _processFactory;

    public GitCliRemoteTagReader()
        : this(GitCliProcessFactory.Default)
    {
    }

    internal GitCliRemoteTagReader(IGitCliProcessFactory processFactory)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    }

    public string Backend => "git-cli";

    public void Read(
        string repositoryPath,
        string remoteName,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitRemoteTagReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitRemoteTagRecord, bool> onTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        var remoteUrl = ResolveRemoteUrl(repositoryPath, remoteName, options, cancellationToken);
        var needsPeel = !projection.IsAccepted ||
                        projection.Includes(nameof(RemoteTagEntity.PeeledSha)) ||
                        projection.Includes(nameof(RemoteTagEntity.IsAnnotated));
        var arguments = new List<string> { "ls-remote", "--tags" };
        if (!needsPeel)
            arguments.Add("--refs");
        arguments.Add(remoteUrl);
        arguments.AddRange(ExactPatterns(query, needsPeel));

        cancellationToken.ThrowIfCancellationRequested();
        using var process = _processFactory.Start(
            new GitCliProcessRequest(
                repositoryPath,
                options.Executable,
                GitReferenceBackendOptions.BackendSettingName,
                arguments,
                GitCliProcessEnvironment.Default),
            cancellationToken);
        using var output = new StreamReader(process.StandardOutput, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var completedNaturally = true;

        foreach (var tag in GitRemoteTagProtocolParser.Parse(
                     ReadLines(output),
                     repositoryPath,
                     remoteName,
                     remoteUrl,
                     needsPeel,
                     query,
                     cancellationToken))
        {
            if (onTag(tag))
                continue;

            completedNaturally = false;
            process.Stop();
            break;
        }

        if (completedNaturally)
            process.Complete();
    }

    private string ResolveRemoteUrl(
        string repositoryPath,
        string remoteName,
        GitReferenceBackendOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = _processFactory.Start(
            new GitCliProcessRequest(
                repositoryPath,
                options.Executable,
                GitReferenceBackendOptions.BackendSettingName,
                ["config", "--local", "--get", "remote." + remoteName + ".url"],
                GitCliProcessEnvironment.Default),
            cancellationToken);
        using var output = new StreamReader(process.StandardOutput, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var url = output.ReadToEnd().Trim();
        try
        {
            process.Complete();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Git remote '{remoteName}' could not be resolved from the local repository configuration.",
                exception);
        }
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException($"Git remote '{remoteName}' has no configured fetch URL.");
        return url;
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static IEnumerable<string> ExactPatterns(GitRemoteTagReadQuery query, bool includePeeledLines)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Candidates(query))
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                candidate.All(character => character is not ('*' or '?' or '[')) &&
                seen.Add(candidate))
            {
                yield return candidate;
                if (includePeeledLines)
                    yield return candidate + "^{}";
            }
        }

        static IEnumerable<string> Candidates(GitRemoteTagReadQuery value)
        {
            if (value.ExactCanonicalName is not null)
                yield return value.ExactCanonicalName;
            if (value.ExactFriendlyName is not null)
                yield return "refs/tags/" + value.ExactFriendlyName;
            if (value.ExactCanonicalNames is not null)
            {
                foreach (var item in value.ExactCanonicalNames)
                    yield return item;
            }
            if (value.ExactFriendlyNames is not null)
            {
                foreach (var item in value.ExactFriendlyNames)
                    yield return "refs/tags/" + item;
            }
        }
    }
}

internal interface IGitStashReader
{
    string Backend { get; }

    IEnumerable<GitStashRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken);

    void Read(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitStashRecord, bool> onStash);
}

internal readonly record struct GitStashReadQuery(
    string? ExactSelector,
    string? ExactSha,
    IReadOnlyList<string>? ExactSelectors,
    IReadOnlyList<string>? ExactShas)
{
    public static GitStashReadQuery Empty { get; } = new(null, null, null, null);

    public bool HasSelectorFilter => ExactSelector is not null || ExactSelectors is { Count: > 0 };

    public bool HasShaFilter => ExactSha is not null || ExactShas is { Count: > 0 };

    public bool IsDirectSelectorLookup => ExactSelector is not null;
}

internal static class GitStashReaderExtensions
{
    public static void Read(
        this IGitStashReader reader,
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitStashRecord, bool> onStash)
    {
        reader.Read(repositoryPath, options, projection, GitStashReadQuery.Empty, createRepository,
            cancellationToken, onStash);
    }
}

internal readonly record struct GitStashRecord(
    string RepositoryPath,
    string Selector,
    string Sha,
    string Message,
    string? IndexSha,
    string? WorkTreeSha,
    string? UntrackedFilesSha);

internal static class GitStashReadQueryMatcher
{
    public static bool Matches(GitStashReadQuery query, GitStashRecord record)
    {
        var selectorMatches = !query.HasSelectorFilter ||
                               query.ExactSelector is not null && SelectorsEqual(query.ExactSelector, record.Selector) ||
                               query.ExactSelectors is { } selectors && selectors.Any(candidate => SelectorsEqual(candidate, record.Selector));
        var shaMatches = !query.HasShaFilter ||
                         query.ExactSha is not null && string.Equals(query.ExactSha, record.Sha, StringComparison.OrdinalIgnoreCase) ||
                         query.ExactShas is { } shas && shas.Contains(record.Sha, StringComparer.OrdinalIgnoreCase);
        return selectorMatches && shaMatches;
    }

    public static bool ShouldStopAfterMatch(
        GitStashReadQuery query,
        GitStashRecord record,
        IReadOnlySet<string>? foundShas = null)
    {
        if (query.IsDirectSelectorLookup)
            return true;

        if (query.HasShaFilter)
        {
            var requested = query.ExactShas ?? (query.ExactSha is null ? [] : [query.ExactSha]);
            if (requested.Count == 1)
                return true;

            return foundShas is not null &&
                   requested.All(candidate => foundShas.Contains(candidate));
        }

        if (query.ExactSelectors is not { Count: > 0 })
            return false;

        var indexes = query.ExactSelectors
            .Select(ParseStashIndex)
            .ToArray();
        return indexes.All(static index => index >= 0) &&
               ParseStashIndex(record.Selector) >= indexes.Max();
    }

    private static bool SelectorsEqual(string left, string right) =>
        string.Equals(NormalizeSelectorForGit(left), NormalizeSelectorForGit(right), StringComparison.Ordinal);

    internal static string NormalizeSelectorForGit(string selector) =>
        selector.StartsWith("refs/stash@", StringComparison.Ordinal)
            ? "stash" + selector["refs/stash".Length..]
            : selector;

    private static int ParseStashIndex(string selector)
    {
        var open = selector.IndexOf("@{", StringComparison.Ordinal);
        return open < 0 || !selector.EndsWith('}') ||
               !int.TryParse(selector[(open + 2)..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? -1
            : index;
    }
}

internal sealed class LibGit2StashReader : IGitStashReader
{
    public string Backend => "libgit2";

    public IEnumerable<GitStashRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken)
    {
        using var repository = createRepository(repositoryPath);
        var index = 0;
        var needsMessage = !projection.IsAccepted || projection.Includes(nameof(StashEntity.Message));
        var needsParents = !projection.IsAccepted ||
                           projection.Includes(nameof(StashEntity.Index)) ||
                           projection.Includes(nameof(StashEntity.WorkTree)) ||
                           projection.Includes(nameof(StashEntity.UntrackedFiles));
        foreach (var stash in repository.Stashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workTreeSha = stash.WorkTree?.Sha;
            var record = new GitStashRecord(
                repository.Info.Path,
                $"stash@{{{index++}}}",
                workTreeSha ?? string.Empty,
                needsMessage ? stash.Message : string.Empty,
                needsParents ? stash.Index?.Sha : null,
                needsParents ? workTreeSha : null,
                needsParents ? stash.Untracked?.Sha : null);
            if (!GitStashReadQueryMatcher.Matches(query, record))
                continue;
            yield return record;
        }
    }

    public void Read(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitStashRecord, bool> onStash)
    {
        foreach (var record in ReadStreaming(repositoryPath, options, projection, query, createRepository, cancellationToken))
        {
            if (!onStash(record))
                break;
        }
    }
}

internal sealed class GitCliStashReader : IGitStashReader
{
    private readonly IGitCliProcessFactory _processFactory;

    public GitCliStashReader()
        : this(GitCliProcessFactory.Default)
    {
    }

    internal GitCliStashReader(IGitCliProcessFactory processFactory)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    }

    public string Backend => "git-cli";

    public void Read(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitStashRecord, bool> onStash)
    {
        foreach (var record in ReadStreaming(repositoryPath, options, projection, query, createRepository, cancellationToken))
        {
            if (!onStash(record))
                break;
        }
    }

    public IEnumerable<GitStashRecord> ReadStreaming(
        string repositoryPath,
        GitReferenceBackendOptions options,
        GitProjection projection,
        GitStashReadQuery query,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var needsMessage = !projection.IsAccepted || projection.Includes(nameof(StashEntity.Message));
        var needsParents = !projection.IsAccepted ||
                           projection.Includes(nameof(StashEntity.Index)) ||
                           projection.Includes(nameof(StashEntity.WorkTree)) ||
                           projection.Includes(nameof(StashEntity.UntrackedFiles));
        var format = FormatFor(needsMessage, needsParents);
        var arguments = new List<string> { "log", "-g" };
        if (query.IsDirectSelectorLookup)
            arguments.Add("-1");
        arguments.Add("--no-decorate");
        arguments.Add("--format=" + format);
        arguments.Add(query.IsDirectSelectorLookup
            ? GitStashReadQueryMatcher.NormalizeSelectorForGit(query.ExactSelector!)
            : "refs/stash");
        using var process = _processFactory.Start(
            new GitCliProcessRequest(
                repositoryPath,
                options.Executable,
                GitReferenceBackendOptions.BackendSettingName,
                arguments,
                GitCliProcessEnvironment.Default),
            cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var completedNaturally = false;
        var foundShas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selector = reader.ReadToken();
                if (selector is null)
                    break;

                selector = selector.TrimStart('\r', '\n');
                if (selector.Length == 0)
                    continue;

                var sha = ReadRequired(reader, "stash SHA");
                var parents = needsParents
                    ? ReadRequired(reader, "stash parents")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : [];
                var message = needsMessage ? ReadRequired(reader, "stash message") : string.Empty;
                var record = new GitStashRecord(
                    repositoryPath,
                    selector,
                    sha,
                    message,
                    parents.Length > 1 ? parents[1] : null,
                    sha,
                    parents.Length > 2 ? parents[2] : null);
                if (!GitStashReadQueryMatcher.Matches(query, record))
                    continue;

                if (query.HasShaFilter)
                    foundShas.Add(record.Sha);
                yield return record;

                if (GitStashReadQueryMatcher.ShouldStopAfterMatch(query, record, foundShas))
                    break;
            }

            process.Complete();
            completedNaturally = true;
        }
        finally
        {
            if (!completedNaturally)
                process.Stop();
        }
    }

    private static string ReadRequired(GitNulDelimitedUtf8Reader reader, string field) =>
        reader.ReadToken() ?? throw new InvalidDataException($"Git stash output ended while reading the {field}.");

    private static string FormatFor(bool needsMessage, bool needsParents) =>
        needsParents
            ? needsMessage
                ? "%gd%x00%H%x00%P%x00%gs%x00"
                : "%gd%x00%H%x00%P%x00"
            : needsMessage
                ? "%gd%x00%H%x00%gs%x00"
                : "%gd%x00%H%x00";

}
