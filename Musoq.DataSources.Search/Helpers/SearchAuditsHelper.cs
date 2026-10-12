#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchAuditsHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    public static readonly ISchemaColumn[] Columns;

    static SearchAuditsHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchAudit.Root), 0 },
            { nameof(SearchAudit.ScanId), 1 },
            { nameof(SearchAudit.ScopeFingerprint), 2 },
            { nameof(SearchAudit.Outcome), 3 },
            { nameof(SearchAudit.TerminalReason), 4 },
            { nameof(SearchAudit.Complete), 5 },
            { nameof(SearchAudit.ScopeExhausted), 6 },
            { nameof(SearchAudit.QuerySatisfied), 7 },
            { nameof(SearchAudit.CountsExact), 8 },
            { nameof(SearchAudit.ScopeResolved), 9 },
            { nameof(SearchAudit.VisitedFiles), 10 },
            { nameof(SearchAudit.EligibleFiles), 11 },
            { nameof(SearchAudit.FilesOpened), 12 },
            { nameof(SearchAudit.FilesRead), 13 },
            { nameof(SearchAudit.FilesCompleted), 14 },
            { nameof(SearchAudit.FilesFailed), 15 },
            { nameof(SearchAudit.BinaryFilesSkipped), 16 },
            { nameof(SearchAudit.BytesScanned), 17 },
            { nameof(SearchAudit.FilesMatched), 18 },
            { nameof(SearchAudit.MatchingLines), 19 },
            { nameof(SearchAudit.Occurrences), 20 },
            { nameof(SearchAudit.ObservedRows), 21 },
            { nameof(SearchAudit.FailureCode), 22 },
            { nameof(SearchAudit.FailurePath), 23 }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchAudit.Root), 0, typeof(string)),
            new SchemaColumn(nameof(SearchAudit.ScanId), 1, typeof(string)),
            new SchemaColumn(nameof(SearchAudit.ScopeFingerprint), 2, typeof(string)),
            new SchemaColumn(nameof(SearchAudit.Outcome), 3, typeof(SearchOutcome)),
            new SchemaColumn(nameof(SearchAudit.TerminalReason), 4, typeof(string)),
            new SchemaColumn(nameof(SearchAudit.Complete), 5, typeof(bool)),
            new SchemaColumn(nameof(SearchAudit.ScopeExhausted), 6, typeof(bool)),
            new SchemaColumn(nameof(SearchAudit.QuerySatisfied), 7, typeof(bool)),
            new SchemaColumn(nameof(SearchAudit.CountsExact), 8, typeof(bool)),
            new SchemaColumn(nameof(SearchAudit.ScopeResolved), 9, typeof(bool)),
            new SchemaColumn(nameof(SearchAudit.VisitedFiles), 10, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.EligibleFiles), 11, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FilesOpened), 12, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FilesRead), 13, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FilesCompleted), 14, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FilesFailed), 15, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.BinaryFilesSkipped), 16, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.BytesScanned), 17, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FilesMatched), 18, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.MatchingLines), 19, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.Occurrences), 20, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.ObservedRows), 21, typeof(long)),
            new SchemaColumn(nameof(SearchAudit.FailureCode), 22, typeof(string)),
            new SchemaColumn(nameof(SearchAudit.FailurePath), 23, typeof(string))
        ];
    }
}
