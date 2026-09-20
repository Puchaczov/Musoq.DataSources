#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Planning;

internal static class SearchPlanInspection
{
    public const int SchemaVersion = 1;

    public const string PropertyName = "search.plan.v1";

    public const string DiagnosticOptimization = "SearchPlanInspection";

    public static IReadOnlyList<SourceColumnRef> SanitizeRequiredColumns(
        IReadOnlyList<SourceColumnRef>? requestedColumns,
        out IReadOnlyList<string> malformedColumns)
    {
        if (requestedColumns is null)
        {
            malformedColumns = ["<null-list>"];
            return [];
        }

        var usable = new List<SourceColumnRef>(requestedColumns.Count);
        var malformed = new List<string>();
        for (var index = 0; index < requestedColumns.Count; index++)
        {
            var column = requestedColumns[index];
            if (column is null)
            {
                malformed.Add($"<null-at-{index}>");
                continue;
            }

            if (string.IsNullOrWhiteSpace(column.Name))
            {
                malformed.Add(string.IsNullOrEmpty(column.Name)
                    ? $"<empty-at-{index}>"
                    : $"<whitespace-at-{index}>");
                continue;
            }

            usable.Add(column);
        }

        malformedColumns = malformed;
        return usable;
    }

    public static IReadOnlyList<string> GetResidualColumnNames(
        IReadOnlyList<SourceColumnRef> requestedColumns,
        IReadOnlyList<SourceColumnRef> acceptedColumns)
    {
        ArgumentNullException.ThrowIfNull(requestedColumns);
        ArgumentNullException.ThrowIfNull(acceptedColumns);

        var accepted = new HashSet<string>(
            acceptedColumns.Select(static column => Normalize(column.Name)),
            StringComparer.OrdinalIgnoreCase);
        var residual = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requestedColumns)
        {
            var normalized = Normalize(column.Name);
            if (!accepted.Contains(normalized) && seen.Add(normalized))
                residual.Add(normalized);
        }

        return residual;
    }

    public static IReadOnlyList<SourceContractDiagnostic> CreateContractDiagnostics(
        string? sourceName,
        IReadOnlyList<SourceColumnRef>? requestedColumns,
        IReadOnlyList<SourceColumnRef> acceptedColumns,
        IReadOnlyList<string> malformedColumns,
        bool supported)
    {
        ArgumentNullException.ThrowIfNull(acceptedColumns);
        ArgumentNullException.ThrowIfNull(malformedColumns);

        var diagnostics = new List<SourceContractDiagnostic>();
        if (requestedColumns is null)
        {
            diagnostics.Add(SourceContractDiagnostic.Error(
                "Search received null required-column metadata and treated it as an empty request.",
                "MalformedRequiredColumns"));
        }

        foreach (var malformed in malformedColumns)
        {
            diagnostics.Add(SourceContractDiagnostic.Warning(
                $"Search ignored malformed required-column metadata '{malformed}'.",
                "MalformedRequiredColumn"));
        }

        var accepted = new HashSet<string>(
            acceptedColumns.Select(static column => Normalize(column.Name)),
            StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (supported && requestedColumns is not null)
        {
            foreach (var requested in requestedColumns)
            {
                if (requested is null || string.IsNullOrWhiteSpace(requested.Name))
                    continue;

                var normalized = Normalize(requested.Name);
                if (accepted.Contains(normalized) || !reported.Add(normalized))
                    continue;

                diagnostics.Add(SourceContractDiagnostic.Warning(
                    $"Search did not accept required column '{normalized}' for source '{sourceName ?? "<unknown>"}'; Core must handle it as residual work.",
                    "UnsupportedRequiredColumn") with
                {
                    ColumnName = normalized
                });
            }
        }

        return diagnostics;
    }

    public static SearchPlanInspectionSnapshot Create(
        string? sourceName,
        SourcePlanRequest request,
        IReadOnlyList<SourceColumnRef> requestedColumns,
        IReadOnlyList<SourceColumnRef> acceptedColumns,
        IReadOnlyList<string> residualColumnNames,
        SourcePredicateExpression? acceptedPredicate,
        SourcePredicateExpression? residualPredicate,
        int acceptedOrderByCount,
        int residualOrderByCount,
        long? acceptedSkip,
        long? residualSkip,
        long? acceptedTake,
        long? residualTake,
        int residualComputedProjectionCount,
        bool supported)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestedColumns);
        ArgumentNullException.ThrowIfNull(acceptedColumns);
        ArgumentNullException.ThrowIfNull(residualColumnNames);

        var profile = SourceProfile.For(sourceName, supported);
        var contextRequested = acceptedColumns.Any(static column =>
            string.Equals(
                Normalize(column.Name),
                nameof(SearchMatch.Context),
                StringComparison.OrdinalIgnoreCase));

        return new SearchPlanInspectionSnapshot(
            sourceName ?? "<unknown>",
            profile.Strategy,
            DescribeEffectiveScope(profile.ScopeAvailable),
            profile.Content,
            profile.Decoding,
            DescribeContext(contextRequested, profile.SupportsContext),
            profile.EarlyStop,
            request.RequiredColumns?.Count ?? requestedColumns.Count,
            acceptedColumns.Select(static column => Normalize(column.Name)).ToArray(),
            residualColumnNames.ToArray(),
            request.Predicate is not null,
            acceptedPredicate is not null,
            residualPredicate is not null,
            request.OrderBy?.Count ?? 0,
            acceptedOrderByCount,
            residualOrderByCount,
            request.Skip,
            acceptedSkip,
            residualSkip,
            request.Take,
            acceptedTake,
            residualTake,
            request.RequestedComputedProjections?.Count ?? 0,
            residualComputedProjectionCount,
            FormatTarget(request.Identity));
    }

    private static string DescribeEffectiveScope(bool scopeAvailable)
    {
        if (!scopeAvailable)
            return "runtime request scope; static planner metadata is unavailable; resolved-at-execution=yes";

        var scope = ScopePolicy.Default;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"root=declared source argument; recursive={scope.Recursive}; include={scope.Include.Count}; exclude={scope.Exclude.Count}; repository-ignores={scope.RepositoryIgnores}; global-ignores={scope.GlobalIgnores}; hidden-entries={scope.HiddenEntries}; follow-links={scope.FollowLinks}; inaccessible-entries={scope.InaccessibleEntries}; metadata-filters={CountMetadataFilters(scope.Metadata)}; resolved-at-execution=yes");
    }

    private static string DescribeContext(bool requested, bool supported)
    {
        if (!supported)
            return "not available at the static planner boundary";

        return requested
            ? "Context retained when execution enables bounded before/after lines; the public two-argument source defaults context to disabled"
            : "not required; context evidence is not retained";
    }

    private static int CountMetadataFilters(SearchMetadataPolicy metadata)
    {
        var count = metadata.NameIncludes.Count +
                    metadata.NameExcludes.Count +
                    metadata.ExtensionIncludes.Count +
                    metadata.ExtensionExcludes.Count;
        if (metadata.MinimumSizeBytes is not null)
            count++;
        if (metadata.MaximumSizeBytes is not null)
            count++;
        if (metadata.ModifiedAfterOrEqualUtc is not null)
            count++;
        if (metadata.ModifiedBeforeOrEqualUtc is not null)
            count++;

        return count;
    }

    private static string FormatTarget(SourceIdentity identity)
    {
        if (identity is null)
            return "#<unknown>";

        return $"#{identity.SchemaName}.{identity.MethodName}";
    }

    private static string Normalize(string columnName)
    {
        var separator = columnName.LastIndexOf('.');
        return separator < 0 ? columnName : columnName[(separator + 1)..];
    }

    private sealed record SourceProfile(
        string Strategy,
        string Content,
        string Decoding,
        string EarlyStop,
        bool ScopeAvailable,
        bool SupportsContext)
    {
        public static SourceProfile For(string? sourceName, bool supported)
        {
            if (!supported)
            {
                return new SourceProfile(
                    "unplanned-source-residual",
                    "source operation remains residual; execution details are not claimed by the Search planner",
                    "source-specific decoding is resolved by the runtime source contract",
                    "not claimed by the Search planner",
                    false,
                    false);
            }

            return sourceName?.ToLowerInvariant() switch
            {
                "paths" => new SourceProfile(
                    "filesystem-metadata-path-scan",
                    "enumerates eligible paths from filesystem metadata; never opens or decodes file content",
                    "none; content is never opened or decoded",
                    "global=no; complete scope enumeration is required",
                    true,
                    false),
                "files" => new SourceProfile(
                    "filesystem-text-any-match-scan",
                    "opens and decodes eligible files; stops after the first match within each file, but must visit every eligible file",
                    "auto (BOM-aware UTF-8/UTF-16) resolved per opened file; binary classification may probe content first",
                    "global=no; per-file=after-first-match; TAKE is an output window, not a scan budget",
                    true,
                    false),
                "counts" => new SourceProfile(
                    "filesystem-text-complete-count-scan",
                    "opens and decodes every eligible file to complete exact occurrence, matching-line and byte counts",
                    "auto (BOM-aware UTF-8/UTF-16) resolved per opened file; binary classification may probe content first",
                    "global=no; per-file=no; exact completion requires the full eligible file",
                    true,
                    false),
                "lines" => new SourceProfile(
                    "filesystem-text-line-scan",
                    "opens and decodes eligible files to aggregate occurrences into complete physical-line rows",
                    "auto (BOM-aware UTF-8/UTF-16) resolved per opened file; binary classification may probe content first",
                    "global=no; per-file=no; line completion requires scanning the eligible file",
                    true,
                    false),
                "matches" => new SourceProfile(
                    "filesystem-text-occurrence-scan",
                    "opens and decodes eligible files to emit one row per non-overlapping occurrence; optional fields are retained only when required",
                    "auto (BOM-aware UTF-8/UTF-16) resolved per opened file; binary classification may probe content first",
                    "global=no; per-file=no; TAKE is an output window, not a scan budget",
                    true,
                    true),
                _ => new SourceProfile(
                    "unplanned-source-residual",
                    "source operation remains residual; execution details are not claimed by the Search planner",
                    "source-specific decoding is resolved by the runtime source contract",
                    "not claimed by the Search planner",
                    false,
                    false)
            };
        }
    }
}

internal sealed record SearchPlanInspectionSnapshot(
    string SourceName,
    string Strategy,
    string EffectiveScope,
    string Content,
    string Decoding,
    string Context,
    string EarlyStop,
    int RequestedColumnCount,
    IReadOnlyList<string> AcceptedColumns,
    IReadOnlyList<string> ResidualColumns,
    bool PredicateRequested,
    bool PredicateAccepted,
    bool PredicateResidual,
    int RequestedOrderByCount,
    int AcceptedOrderByCount,
    int ResidualOrderByCount,
    long? RequestedSkip,
    long? AcceptedSkip,
    long? ResidualSkip,
    long? RequestedTake,
    long? AcceptedTake,
    long? ResidualTake,
    int RequestedComputedProjectionCount,
    int ResidualComputedProjectionCount,
    string Target)
{
    public IReadOnlyDictionary<string, object?> ToProperties()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = SearchPlanInspection.SchemaVersion,
            ["source"] = SourceName,
            ["strategy"] = Strategy,
            ["effectiveScope"] = EffectiveScope,
            ["content"] = Content,
            ["decoding"] = Decoding,
            ["context"] = Context,
            ["earlyStop"] = EarlyStop,
            ["requested"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["columns"] = RequestedColumnCount,
                ["predicate"] = PredicateRequested,
                ["orderBy"] = RequestedOrderByCount,
                ["skip"] = RequestedSkip,
                ["take"] = RequestedTake,
                ["computedProjections"] = RequestedComputedProjectionCount
            },
            ["accepted"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["columns"] = AcceptedColumns.ToArray(),
                ["predicate"] = Decision(PredicateRequested, PredicateAccepted, PredicateResidual),
                ["orderBy"] = AcceptedOrderByCount,
                ["skip"] = AcceptedSkip,
                ["take"] = AcceptedTake,
                ["computedProjections"] = 0
            },
            ["residual"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["columns"] = ResidualColumns.ToArray(),
                ["predicate"] = Decision(PredicateRequested, PredicateAccepted, PredicateResidual),
                ["orderBy"] = ResidualOrderByCount,
                ["skip"] = ResidualSkip,
                ["take"] = ResidualTake,
                ["computedProjections"] = ResidualComputedProjectionCount
            }
        };
    }

    public OptimizationDiagnostic ToDiagnostic()
    {
        return OptimizationDiagnostic.Info(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"strategy={Strategy}; effective-scope={EffectiveScope}; content={Content}; decoding={Decoding}; context={Context}; early-stop={EarlyStop}; accepted={FormatAcceptedOperations()}; residual={FormatResidualOperations()}"))
            with
            {
                Optimization = SearchPlanInspection.DiagnosticOptimization,
                Target = Target,
                Reason = "All fields are derived from source metadata; root resolution and content reads remain deferred to execution."
            };
    }

    private static string Decision(bool requested, bool accepted, bool residual)
    {
        if (!requested)
            return "not-requested";

        if (accepted && !residual)
            return "accepted";

        return accepted ? "partial" : "residual";
    }

    private string FormatAcceptedOperations()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"columns={FormatNames(AcceptedColumns)}; predicate={Decision(PredicateRequested, PredicateAccepted, PredicateResidual)}; orderBy={AcceptedOrderByCount}/{RequestedOrderByCount}; skip={FormatWindow(RequestedSkip, AcceptedSkip)}; take={FormatWindow(RequestedTake, AcceptedTake)}; computed=0/{RequestedComputedProjectionCount}");
    }

    private string FormatResidualOperations()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"columns={FormatNames(ResidualColumns)}; predicate={(PredicateResidual ? "residual" : "none")}; orderBy={ResidualOrderByCount}/{RequestedOrderByCount}; skip={FormatResidualWindow(ResidualSkip)}; take={FormatResidualWindow(ResidualTake)}; computed={ResidualComputedProjectionCount}");
    }

    private static string FormatNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return "none";

        var displayed = names
            .Take(8)
            .Select(static name => name.Length <= 64 ? name : name[..61] + "...")
            .ToArray();
        var suffix = names.Count > displayed.Length
            ? $", ...(+{names.Count - displayed.Length})"
            : string.Empty;
        return $"[{string.Join(", ", displayed)}{suffix}]";
    }

    private static string FormatWindow(long? requested, long? accepted)
    {
        if (!requested.HasValue)
            return "not-requested";

        return accepted.HasValue
            ? $"accepted({accepted.Value.ToString(CultureInfo.InvariantCulture)})"
            : "residual";
    }

    private static string FormatResidualWindow(long? residual)
    {
        return residual.HasValue
            ? $"residual({residual.Value.ToString(CultureInfo.InvariantCulture)})"
            : "none";
    }
}
