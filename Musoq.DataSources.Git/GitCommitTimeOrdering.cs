using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Musoq.DataSources.Git;

/// <summary>Compact time ordering for native-compatible nested history, including equal-time heap behavior.</summary>
internal static class GitCommitTimeOrdering
{
    public static string CreateInput(string graph, CancellationToken token)
    {
        var heap = new List<(string Sha, long Time)>();
        foreach (var line in graph.AsSpan().EnumerateLines())
        {
            token.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;
            var separator = line.IndexOf(' ');
            if (separator < 0) throw new InvalidDataException("Invalid Git revision timestamp record.");
            var entry = (Sha: line[..separator].ToString(), Time: long.Parse(line[(separator + 1)..], CultureInfo.InvariantCulture));
            var index = heap.Count;
            heap.Add(entry);
            while (index > 0 && heap[(index - 1) / 2].Time < entry.Time)
            {
                heap[index] = heap[(index - 1) / 2];
                index = (index - 1) / 2;
            }
            heap[index] = entry;
        }
        var input = new StringBuilder();
        while (heap.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            input.Append(heap[0].Sha).Append('\n');
            var last = heap[^1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) break;
            var index = 0;
            while (index * 2 + 1 < heap.Count)
            {
                var child = index * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].Time > heap[child].Time) child++;
                if (last.Time >= heap[child].Time) break;
                heap[index] = heap[child];
                index = child;
            }
            heap[index] = last;
        }
        return input.ToString();
    }
}
