using System;
using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Process;

internal static class ProcessHelper
{
    public static readonly IReadOnlyDictionary<string, int> ProcessNameToIndexMap;

    public static readonly ISchemaColumn[] ProcessColumns;

    static ProcessHelper()
    {
        ProcessNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(System.Diagnostics.Process.BasePriority), 0 },
            { nameof(System.Diagnostics.Process.EnableRaisingEvents), 1 },
            { nameof(System.Diagnostics.Process.ExitCode), 2 },
            { nameof(System.Diagnostics.Process.ExitTime), 3 },
            { nameof(System.Diagnostics.Process.Handle), 4 },
            { nameof(System.Diagnostics.Process.HandleCount), 5 },
            { nameof(System.Diagnostics.Process.HasExited), 6 },
            { nameof(System.Diagnostics.Process.Id), 7 },
            { nameof(System.Diagnostics.Process.MachineName), 8 },
            { nameof(System.Diagnostics.Process.MainWindowTitle), 9 },
            { nameof(System.Diagnostics.Process.PagedMemorySize64), 10 },
            { nameof(System.Diagnostics.Process.ProcessName), 11 },
            { nameof(System.Diagnostics.Process.ProcessorAffinity), 12 },
            { nameof(System.Diagnostics.Process.Responding), 13 },
            { nameof(System.Diagnostics.Process.StartTime), 14 },
            { nameof(System.Diagnostics.Process.TotalProcessorTime), 15 },
            { nameof(System.Diagnostics.Process.UserProcessorTime), 16 },
            { "Directory", 17 },
            { "FileName", 18 }
        };

        ProcessColumns =
        [
            new SchemaColumn(nameof(System.Diagnostics.Process.BasePriority), 0, typeof(int)),
            new SchemaColumn(nameof(System.Diagnostics.Process.EnableRaisingEvents), 1, typeof(bool)),
            new SchemaColumn(nameof(System.Diagnostics.Process.ExitCode), 2, typeof(int)),
            new SchemaColumn(nameof(System.Diagnostics.Process.ExitTime), 3, typeof(DateTime)),
            // IntPtr is a CLR primitive, but it is an opaque process handle rather than a
            // supported scalar query value. Keep the schema entry while exposing it as an
            // intentionally non-star-expandable value.
            new SchemaColumn(nameof(System.Diagnostics.Process.Handle), 4, typeof(object)),
            new SchemaColumn(nameof(System.Diagnostics.Process.HandleCount), 5, typeof(int)),
            new SchemaColumn(nameof(System.Diagnostics.Process.HasExited), 6, typeof(bool)),
            new SchemaColumn(nameof(System.Diagnostics.Process.Id), 7, typeof(int)),
            new SchemaColumn(nameof(System.Diagnostics.Process.MachineName), 8, typeof(string)),
            new SchemaColumn(nameof(System.Diagnostics.Process.MainWindowTitle), 9, typeof(string)),
            new SchemaColumn(nameof(System.Diagnostics.Process.PagedMemorySize64), 10, typeof(long)),
            new SchemaColumn(nameof(System.Diagnostics.Process.ProcessName), 11, typeof(string)),
            // See Handle above. This remains addressable as schema metadata but is omitted
            // from SELECT * under primitive result validation.
            new SchemaColumn(nameof(System.Diagnostics.Process.ProcessorAffinity), 12, typeof(object)),
            new SchemaColumn(nameof(System.Diagnostics.Process.Responding), 13, typeof(bool)),
            new SchemaColumn(nameof(System.Diagnostics.Process.StartTime), 14, typeof(DateTime)),
            new SchemaColumn(nameof(System.Diagnostics.Process.TotalProcessorTime), 15, typeof(TimeSpan)),
            new SchemaColumn(nameof(System.Diagnostics.Process.UserProcessorTime), 16, typeof(TimeSpan)),
            new SchemaColumn("Directory", 17, typeof(string)),
            new SchemaColumn("FileName", 18, typeof(string))
        ];
    }
}
