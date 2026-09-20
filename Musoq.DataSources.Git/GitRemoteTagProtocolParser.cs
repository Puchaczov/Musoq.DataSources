using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Musoq.DataSources.Git;

/// <summary>Parses line-oriented remote advertisement records without retaining the complete advertisement.</summary>
internal static class GitRemoteTagProtocolParser
{
    public static IEnumerable<GitRemoteTagRecord> Parse(
        IEnumerable<string> lines,
        string repositoryPath,
        string remoteName,
        string remoteUrl,
        bool needsPeel,
        GitRemoteTagReadQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        string? pendingName = null;
        string? pendingObjectSha = null;

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryParseLine(line, out var objectSha, out var canonicalName))
                continue;

            if (!needsPeel)
            {
                if (!canonicalName.EndsWith("^{}", StringComparison.Ordinal) &&
                    CreateRecord(repositoryPath, remoteName, remoteUrl, query, canonicalName, objectSha, null, false) is
                    { } record)
                    yield return record;

                continue;
            }

            if (canonicalName.EndsWith("^{}", StringComparison.Ordinal))
            {
                var baseName = canonicalName[..^3];
                if (pendingName is not null && string.Equals(pendingName, baseName, StringComparison.Ordinal))
                {
                    if (CreateRecord(repositoryPath, remoteName, remoteUrl, query, pendingName, pendingObjectSha!, objectSha, true) is
                        { } record)
                        yield return record;

                    pendingName = null;
                    pendingObjectSha = null;
                    continue;
                }

                if (pendingName is not null &&
                    CreateRecord(repositoryPath, remoteName, remoteUrl, query, pendingName, pendingObjectSha!, null, false) is
                    { } pendingRecord)
                    yield return pendingRecord;

                pendingName = null;
                pendingObjectSha = null;
                continue;
            }

            if (pendingName is not null && string.Equals(pendingName, canonicalName, StringComparison.Ordinal))
            {
                // A malformed advertisement may repeat a base line before its peel line. Keep one bounded pending
                // record so the duplicate cannot become both an unannotated and an annotated output row.
                pendingObjectSha = objectSha;
                continue;
            }

            if (pendingName is not null &&
                CreateRecord(repositoryPath, remoteName, remoteUrl, query, pendingName, pendingObjectSha!, null, false) is
                { } previousRecord)
                yield return previousRecord;

            pendingName = canonicalName;
            pendingObjectSha = objectSha;
        }

        if (pendingName is not null &&
            CreateRecord(repositoryPath, remoteName, remoteUrl, query, pendingName, pendingObjectSha!, null, false) is
            { } finalRecord)
            yield return finalRecord;
    }

    private static GitRemoteTagRecord? CreateRecord(
        string repositoryPath,
        string remoteName,
        string remoteUrl,
        GitRemoteTagReadQuery query,
        string canonicalName,
        string objectSha,
        string? peeledSha,
        bool isAnnotated)
    {
        if (!PassesCursor(query, canonicalName))
            return null;

        var friendlyName = canonicalName.StartsWith("refs/tags/", StringComparison.Ordinal)
            ? canonicalName["refs/tags/".Length..]
            : canonicalName;
        return new GitRemoteTagRecord(
            repositoryPath,
            remoteName,
            remoteUrl,
            friendlyName,
            canonicalName,
            objectSha,
            peeledSha,
            isAnnotated);
    }

    private static bool TryParseLine(string line, out string objectSha, out string canonicalName)
    {
        var separator = line.IndexOf('\t');
        if (separator <= 0 || separator == line.Length - 1)
        {
            objectSha = string.Empty;
            canonicalName = string.Empty;
            return false;
        }

        objectSha = line[..separator].Trim();
        canonicalName = line[(separator + 1)..].Trim();
        return objectSha.Length > 0 && canonicalName.StartsWith("refs/tags/", StringComparison.Ordinal);
    }

    private static bool PassesCursor(GitRemoteTagReadQuery query, string canonicalName)
    {
        if (query.CanonicalNameAfter is null)
            return true;

        var comparison = string.Compare(canonicalName, query.CanonicalNameAfter, StringComparison.Ordinal);
        if (comparison > 0 || comparison == 0 && query.CanonicalNameAfterInclusive)
            return true;

        query.OnCursorSkipped?.Invoke();
        return false;
    }
}
