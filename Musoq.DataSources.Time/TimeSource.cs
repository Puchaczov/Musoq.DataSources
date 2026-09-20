using System;
using System.Collections.Generic;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Time;

internal class TimeSource(
    DateTimeOffset startAt,
    DateTimeOffset stopAt,
    string resolution,
    SourceExecutionContext executionContext)
    : RowSourceBase<TimeEntity>
{
    private const string TimeSourceName = "time";
    private readonly string _resolution = resolution.ToLowerInvariant();
    private readonly DateTimeOffset _stopAt = resolution.ToLowerInvariant() switch
    {
        "seconds" => stopAt.Add(TimeSpan.FromMilliseconds(1)),
        "minutes" => stopAt.AddSeconds(1),
        "hours" => stopAt.AddMinutes(1),
        "days" => stopAt.AddHours(1),
        "months" => stopAt.AddDays(1),
        "years" => stopAt.AddMonths(1),
        _ => throw new NotSupportedException($"Chosen resolution '{resolution.ToLowerInvariant()}' is not supported.")
    };

    protected override void CollectChunks(IChunkWriter<TimeEntity> writer)
    {
        var progress = new DataSourceProgressReporter(executionContext, TimeSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            if (executionContext.EndWorkToken.IsCancellationRequested)
                return;

            var modify = _resolution switch
            {
                "seconds" => (Func<DateTimeOffset, DateTimeOffset>)(offset => offset.AddSeconds(1)),
                "minutes" => offset => offset.AddMinutes(1),
                "hours" => offset => offset.AddHours(1),
                "days" => offset => offset.AddDays(1),
                "months" => offset => offset.AddMonths(1),
                "years" => offset => offset.AddYears(1),
                _ => throw new NotSupportedException($"Chosen resolution '{_resolution}' is not supported.")
            };

            var chunk = new List<TimeEntity>();
            var currentTime = startAt;
            var plan = executionContext.Plan;
            long skipped = 0;
            long emitted = 0;

            while (currentTime <= _stopAt)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                var entity = new TimeEntity(currentTime);
                currentTime = modify(currentTime);
                progress.RowRead();

                if (!TimeSourcePlanner.Matches(plan.AcceptedPredicate, entity))
                    continue;

                if (plan.AcceptedSkip.HasValue && skipped < plan.AcceptedSkip.Value)
                {
                    skipped++;
                    continue;
                }

                if (plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value)
                    break;

                chunk.Add(entity);
                emitted++;
                totalRowsProcessed++;

                if (chunk.Count < RowChunking.DefaultChunkSize)
                    continue;

                writer.Write(chunk);
                chunk = [];
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }
    }
}
