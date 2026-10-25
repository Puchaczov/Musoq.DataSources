#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Git;

/// <summary>Predicate fields precede projection fields in the same streaming command.</summary>
internal sealed class GitCommitPatternReader
{
    private static readonly (string Name, string Format)[] Fields =
    [ ("Message", "%B"), ("MessageShort", "%s"), ("Author", "%an"), ("AuthorEmail", "%ae"),
      ("Committer", "%cn"), ("CommitterEmail", "%ce"), ("CommittedWhen", "%cI") ];
    private readonly string[] _fields;
    private readonly int _predicateFields;
    private readonly NativePatternExecution _patterns;
    internal GitCommitPatternReader(GitProjection projection)
    {
        _patterns = projection.NativePatterns!;
        var fields = Fields.Where(field => projection.Includes(field.Name) || _patterns.Requires(field.Name))
            .OrderByDescending(field => _patterns.Requires(field.Name)).ToArray();
        _fields = fields.Select(field => field.Name).ToArray();
        _predicateFields = fields.Count(field => _patterns.Requires(field.Name));
        Format = "tformat:%x1e%H%x00" + string.Concat(fields.Select(field => field.Format + "%x00"));
    }
    internal string Format { get; }
    internal bool Read(string path, string header, GitNulDelimitedUtf8Reader reader, CancellationToken token, out GitCommitRecord record)
    {
        record = new(path, header[1..], null, null, null, null, null, null, default);
        for (var i = 0; i < _predicateFields; i++) record = ReadField(record, _fields[i], reader);
        var admitted = _patterns.Matches(record, GitNativePatterns.Commits);
        for (var i = _predicateFields; i < _fields.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            if (admitted) record = ReadField(record, _fields[i], reader);
            else if (!reader.SkipToken(token)) throw new InvalidDataException("Git commit output ended inside a rejected record.");
        }
        return admitted;
    }
    private GitCommitRecord ReadField(GitCommitRecord row, string name, GitNulDelimitedUtf8Reader reader)
    {
        _patterns.RichFieldLoaded();
        var value = reader.ReadToken() ?? throw new InvalidDataException($"Git commit output ended while reading {name}.");
        return Set(row, name, value);
    }
    private static GitCommitRecord Set(GitCommitRecord row, string name, string value) => name switch
    {
        "Message" => row with { Message = value }, "MessageShort" => row with { MessageShort = value },
        "Author" => row with { Author = value }, "AuthorEmail" => row with { AuthorEmail = value },
        "Committer" => row with { Committer = value }, "CommitterEmail" => row with { CommitterEmail = value },
        "CommittedWhen" => row with { CommittedWhen = DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? when : throw new InvalidDataException("Git returned an invalid commit timestamp.") },
        _ => throw new InvalidOperationException("Unknown commit field.")
    };
    internal bool Read(string path, Commit commit, out GitCommitRecord record)
    {
        record = new(path, commit.Sha, null, null, null, null, null, null, default);
        for (var i = 0; i < _predicateFields; i++) record = ReadField(record, _fields[i], commit);
        if (!_patterns.Matches(record, GitNativePatterns.Commits)) return false;
        for (var i = _predicateFields; i < _fields.Length; i++) record = ReadField(record, _fields[i], commit);
        return true;
    }
    private GitCommitRecord ReadField(GitCommitRecord row, string name, Commit commit)
    {
        _patterns.RichFieldLoaded();
        return name switch
        {
            "Message" => row with { Message = commit.Message }, "MessageShort" => row with { MessageShort = commit.MessageShort },
            "Author" => row with { Author = commit.Author.Name }, "AuthorEmail" => row with { AuthorEmail = commit.Author.Email },
            "Committer" => row with { Committer = commit.Committer.Name }, "CommitterEmail" => row with { CommitterEmail = commit.Committer.Email },
            "CommittedWhen" => row with { CommittedWhen = commit.Committer.When },
            _ => throw new InvalidOperationException("Unknown commit field.")
        };
    }
}
