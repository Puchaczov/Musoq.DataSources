using System;
using System.Collections.Generic;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class WorktreesRowsSource(string path, SourceExecutionContext context)
    : GitDiagnosticRowsSourceBase<WorktreeEntity>(context, "git.worktrees")
{
    protected override long CollectRows(DiagnosticChunkWriter<WorktreeEntity> writer, CancellationToken cancellationToken)
    {
        var options = GitHistoryBackendOptions.FromExecutable(Context.SourceRuntimeSettings);
        var predicate = TypedPredicatePlan.Get<WorktreeEntity>(Context.Plan);
        var chunk = new List<WorktreeEntity>(128);
        long rows = 0, examined = 0, skipped = 0;
        var maximumBuffered = 0;
        foreach (var row in new GitWorktreeReader().Read(path, options, cancellationToken, RepositoryLocation))
        {
            examined++;
            if (predicate?.Invoke(row) == false) continue;
            if (skipped < Context.Plan.AcceptedSkip.GetValueOrDefault()) { skipped++; continue; }
            chunk.Add(row);
            maximumBuffered = Math.Max(maximumBuffered, chunk.Count);
            if (Context.Plan.AcceptedTake is { } take && rows + chunk.Count >= take) break;
            if (chunk.Count == 128) rows += WriteChunk(writer, chunk, rows);
        }
        rows += WriteChunk(writer, chunk, rows);
        Context.Diagnostics.AddMetric("Git.Worktrees.RowsExamined", examined);
        Context.Diagnostics.AddMetric("Git.Worktrees.RowsEmitted", rows);
        Context.Diagnostics.AddMetric("Git.Worktrees.MaximumBufferedRows", maximumBuffered);
        return rows;
    }
}
