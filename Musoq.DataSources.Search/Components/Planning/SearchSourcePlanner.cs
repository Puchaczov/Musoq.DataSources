#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Components.Planning;

internal static class SearchSourcePlanner
{
    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supported = SearchPredicatePlanning.IsSupportedSource(name);
        var requestedColumns = SearchPlanInspection.SanitizeRequiredColumns(
            request.RequiredColumns,
            out var malformedColumns);
        var requestedComputedProjections = request.RequestedComputedProjections ?? [];
        var residualOrderBy = request.OrderBy ?? [];

        if (!supported)
        {
            var residualColumns = SearchPlanInspection.GetResidualColumnNames(
                requestedColumns,
                []);
            var residualInspection = SearchPlanInspection.Create(
                name,
                request,
                requestedColumns,
                [],
                residualColumns,
                acceptedPredicate: null,
                residualPredicate: request.Predicate,
                acceptedOrderByCount: 0,
                residualOrderByCount: residualOrderBy.Count,
                acceptedSkip: null,
                residualSkip: request.Skip,
                acceptedTake: null,
                residualTake: request.Take,
                residualComputedProjectionCount: requestedComputedProjections.Count,
                supported: false);

            return new SourcePlanResult
            {
                ExecutionPlan = SourceExecutionPlan.Empty(request.Identity) with
                {
                    Replayability = request.Replayability,
                    Properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [SearchPlanInspection.PropertyName] = residualInspection.ToProperties()
                    }
                },
                ResidualPredicate = request.Predicate,
                ResidualComputedProjections = requestedComputedProjections,
                ResidualOrderBy = residualOrderBy,
                ResidualSkip = request.Skip,
                ResidualTake = request.Take,
                Replayability = request.Replayability,
                Cardinality = CardinalityEstimate.Unknown(
                    "Search source planner does not support this source operation."),
                Diagnostics = [residualInspection.ToDiagnostic()],
                ContractDiagnostics = SearchPlanInspection.CreateContractDiagnostics(
                    name,
                    request.RequiredColumns,
                    [],
                    malformedColumns,
                    supported: false)
            };
        }

        var partition = SearchPredicatePlanning.Split(name, request.Predicate);
        var acceptedColumns = SearchPredicatePlanning.AcceptProjectionColumns(
            name,
            requestedColumns);
        var acceptsSlice = partition.Residual is null &&
                           residualOrderBy.Count == 0 &&
                           IsNonNegativeWindow(request.Skip, request.Take);
        var residualColumnNames = SearchPlanInspection.GetResidualColumnNames(
            requestedColumns,
            acceptedColumns);
        var inspection = SearchPlanInspection.Create(
            name,
            request,
            requestedColumns,
            acceptedColumns,
            residualColumnNames,
            partition.Accepted,
            partition.Residual,
            acceptedOrderByCount: 0,
            residualOrderByCount: residualOrderBy.Count,
            acceptedSkip: acceptsSlice ? request.Skip : null,
            residualSkip: acceptsSlice ? null : request.Skip,
            acceptedTake: acceptsSlice ? request.Take : null,
            residualTake: acceptsSlice ? null : request.Take,
            residualComputedProjectionCount: requestedComputedProjections.Count,
            supported: true);

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = acceptedColumns,
                Replayability = request.Replayability,
                AcceptedPredicate = partition.Accepted,
                AcceptedOrderBy = [],
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [SearchPlanInspection.PropertyName] = inspection.ToProperties()
                }
            },
            AcceptedColumns = acceptedColumns,
            AcceptedComputedProjections = [],
            ResidualComputedProjections = requestedComputedProjections,
            AcceptedPredicate = partition.Accepted,
            ResidualPredicate = partition.Residual,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Replayability = request.Replayability,
            // Counts remains a complete file scan. An accepted predicate is
            // applied only after its exact per-file count row is complete.
            Cardinality = CardinalityEstimate.Unknown("Search source cardinality depends on filesystem contents."),
            Diagnostics = [inspection.ToDiagnostic()],
            ContractDiagnostics = SearchPlanInspection.CreateContractDiagnostics(
                name,
                request.RequiredColumns,
                acceptedColumns,
                malformedColumns,
                supported: true)
        };
    }

    private static bool IsNonNegativeWindow(long? skip, long? take)
    {
        return (!skip.HasValue || skip.Value >= 0) &&
               (!take.HasValue || take.Value >= 0);
    }
}
