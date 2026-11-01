using System;
using System.Collections.Generic;
using System.Threading;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git;

/// <summary>One lazy immutable file and line index shared by all hunks from a blame operation.</summary>
internal sealed class GitBlameFileContent(Func<string> load, CancellationToken token, SourceDiagnostics diagnostics)
{
    private readonly GitNestedSnapshot<Snapshot> _snapshot = new();

    public IEnumerable<BlameLineEntity> Lines(int start, int count)
    {
        token.ThrowIfCancellationRequested();
        var snapshot = _snapshot.GetOrCreate(Load)!;
        for (var index = start; index < Math.Min((long)start + count, snapshot.Starts.Length); index++)
        {
            token.ThrowIfCancellationRequested();
            var offset = snapshot.Starts[index];
            var end = index + 1 < snapshot.Starts.Length ? snapshot.Starts[index + 1] : snapshot.Content.Length;
            if (end > offset && snapshot.Content[end - 1] == '\n') end--;
            if (end > offset && snapshot.Content[end - 1] == '\r') end--;
            yield return new BlameLineEntity(index + 1, snapshot.Content.Substring(offset, end - offset));
        }
    }

    private Snapshot Load()
    {
        token.ThrowIfCancellationRequested();
        var content = load();
        diagnostics.AddMetric("Git.Blame.BlobReads", 1);
        diagnostics.AddMetric("Git.Blame.DecodedCharacters", content.Length);
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if ((index & 4095) == 0) token.ThrowIfCancellationRequested();
            if (content[index] == '\r')
            {
                if (index + 1 < content.Length && content[index + 1] == '\n') index++;
                starts.Add(index + 1);
            }
            else if (content[index] == '\n') starts.Add(index + 1);
        }
        return new Snapshot(content, starts.ToArray());
    }

    private sealed record Snapshot(string Content, int[] Starts);
}
