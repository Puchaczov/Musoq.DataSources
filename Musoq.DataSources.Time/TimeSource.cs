using System;
using System.Collections.Generic;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Time;

internal class TimeSource(
    DateTimeOffset startAt,
    DateTimeOffset stopAt,
    string resolution,
    SourceExecutionContext executionContext)
    : RowSourceBase<DateTimeOffset>
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

    protected override void CollectChunks(IChunkWriter<DateTimeOffset> writer)
    {
        executionContext.ReportDataSourceBegin(TimeSourceName);
        long totalRowsProcessed = 0;

        try
        {
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

            var chunk = new List<DateTimeOffset>();
            var currentTime = startAt;

            while (currentTime <= _stopAt)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                chunk.Add(currentTime);
                currentTime = modify(currentTime);
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
            executionContext.ReportDataSourceEnd(TimeSourceName, totalRowsProcessed);
        }
    }
}
