#nullable enable

using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Docker.Images;
using Musoq.DataSources.Docker.Networks;
using Musoq.DataSources.Docker.Volumes;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker;

internal static class DockerSourcePlanner
{
    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        var tableName = name.ToLowerInvariant();
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => IsSupported(tableName, expression));

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = [],
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                Properties = new Dictionary<string, object?>()
            },
            AcceptedColumns = [],
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = request.OrderBy ?? [],
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown("Docker API cardinality depends on local Docker state."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public static bool Matches(SourcePredicateExpression? predicate, object entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, entity) && Matches(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, entity),
            _ => true
        };
    }

    private static (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual) SplitPredicate(
        SourcePredicateExpression? predicate,
        Func<SourcePredicateExpression, bool> canAccept)
    {
        if (predicate is null)
            return (null, null);

        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical)
        {
            var left = SplitPredicate(logical.Left, canAccept);
            var right = SplitPredicate(logical.Right, canAccept);

            return (
                CombineAnd(left.Accepted, right.Accepted),
                CombineAnd(left.Residual, right.Residual));
        }

        return canAccept(predicate)
            ? (predicate, null)
            : (null, predicate);
    }

    private static SourcePredicateExpression? CombineAnd(
        SourcePredicateExpression? left,
        SourcePredicateExpression? right)
    {
        return (left, right) switch
        {
            (null, null) => null,
            (not null, null) => left,
            (null, not null) => right,
            _ => new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right)
        };
    }

    private static bool IsSupported(string tableName, SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return tableName switch
        {
            "containers" => IsSupportedContainerComparison(columnName, literal.Value, op),
            "images" => IsSupportedImageComparison(columnName, literal.Value, op),
            "networks" => IsSupportedNetworkComparison(columnName, literal.Value, op),
            "volumes" => IsSupportedVolumeComparison(columnName, literal.Value, op),
            _ => false
        };
    }

    private static bool IsSupportedContainerComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (IsStringComparison(value, op))
            return IsOneOf(columnName,
                nameof(ContainerEntity.ID),
                nameof(ContainerEntity.Image),
                nameof(ContainerEntity.ImageID),
                nameof(ContainerEntity.Command),
                nameof(ContainerEntity.State),
                nameof(ContainerEntity.Status),
                nameof(ContainerEntity.NetworkSettings),
                nameof(ContainerEntity.FlattenPorts));

        if (IsIntegerComparison(value, op))
            return IsOneOf(columnName, nameof(ContainerEntity.SizeRw), nameof(ContainerEntity.SizeRootFs));

        return IsDateTimeComparison(value, op) && IsOneOf(columnName, nameof(ContainerEntity.Created));
    }

    private static bool IsSupportedImageComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (IsStringComparison(value, op))
            return IsOneOf(columnName, nameof(ImageEntity.ID), nameof(ImageEntity.ParentID));

        if (IsIntegerComparison(value, op))
            return IsOneOf(
                columnName,
                nameof(ImageEntity.Containers),
                nameof(ImageEntity.SharedSize),
                nameof(ImageEntity.Size),
                nameof(ImageEntity.VirtualSize));

        return IsDateTimeComparison(value, op) && IsOneOf(columnName, nameof(ImageEntity.Created));
    }

    private static bool IsSupportedNetworkComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (IsStringComparison(value, op))
            return IsOneOf(
                columnName,
                nameof(NetworkEntity.Name),
                nameof(NetworkEntity.ID),
                nameof(NetworkEntity.Scope),
                nameof(NetworkEntity.Driver),
                nameof(NetworkEntity.IPAM),
                nameof(NetworkEntity.ConfigFrom));

        if (IsBoolComparison(value, op))
            return IsOneOf(
                columnName,
                nameof(NetworkEntity.EnableIPv6),
                nameof(NetworkEntity.Internal),
                nameof(NetworkEntity.Attachable),
                nameof(NetworkEntity.Ingress),
                nameof(NetworkEntity.ConfigOnly));

        return IsDateTimeComparison(value, op) && IsOneOf(columnName, nameof(NetworkEntity.Created));
    }

    private static bool IsSupportedVolumeComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        return IsStringComparison(value, op) &&
               IsOneOf(
                   columnName,
                   nameof(VolumeEntity.CreatedAt),
                   nameof(VolumeEntity.Driver),
                   nameof(VolumeEntity.Mountpoint),
                   nameof(VolumeEntity.Name),
                   nameof(VolumeEntity.Scope),
                   nameof(VolumeEntity.UsageData));
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, object entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        var left = GetColumnValue(entity, columnName);
        var right = ConvertLiteral(left, literal.Value);
        var compare = Compare(left, right);

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => Equals(left, right),
            SourcePredicateComparisonOperator.NotEqual => !Equals(left, right),
            SourcePredicateComparisonOperator.GreaterThan => compare > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => compare >= 0,
            SourcePredicateComparisonOperator.LessThan => compare < 0,
            SourcePredicateComparisonOperator.LessOrEqual => compare <= 0,
            _ => false
        };
    }

    private static object? GetColumnValue(object entity, string columnName)
    {
        return entity switch
        {
            ContainerEntity container => columnName switch
            {
                nameof(ContainerEntity.ID) => container.ID,
                nameof(ContainerEntity.Image) => container.Image,
                nameof(ContainerEntity.ImageID) => container.ImageID,
                nameof(ContainerEntity.Command) => container.Command,
                nameof(ContainerEntity.Created) => container.Created,
                nameof(ContainerEntity.State) => container.State,
                nameof(ContainerEntity.Status) => container.Status,
                nameof(ContainerEntity.SizeRw) => container.SizeRw,
                nameof(ContainerEntity.SizeRootFs) => container.SizeRootFs,
                nameof(ContainerEntity.NetworkSettings) => container.NetworkSettings,
                nameof(ContainerEntity.FlattenPorts) => container.FlattenPorts,
                _ => null
            },
            ImageEntity image => columnName switch
            {
                nameof(ImageEntity.Containers) => image.Containers,
                nameof(ImageEntity.Created) => image.Created,
                nameof(ImageEntity.ID) => image.ID,
                nameof(ImageEntity.ParentID) => image.ParentID,
                nameof(ImageEntity.SharedSize) => image.SharedSize,
                nameof(ImageEntity.Size) => image.Size,
                nameof(ImageEntity.VirtualSize) => image.VirtualSize,
                _ => null
            },
            NetworkEntity network => columnName switch
            {
                nameof(NetworkEntity.Name) => network.Name,
                nameof(NetworkEntity.ID) => network.ID,
                nameof(NetworkEntity.Created) => network.Created,
                nameof(NetworkEntity.Scope) => network.Scope,
                nameof(NetworkEntity.Driver) => network.Driver,
                nameof(NetworkEntity.EnableIPv6) => network.EnableIPv6,
                nameof(NetworkEntity.IPAM) => network.IPAM,
                nameof(NetworkEntity.Internal) => network.Internal,
                nameof(NetworkEntity.Attachable) => network.Attachable,
                nameof(NetworkEntity.Ingress) => network.Ingress,
                nameof(NetworkEntity.ConfigFrom) => network.ConfigFrom,
                nameof(NetworkEntity.ConfigOnly) => network.ConfigOnly,
                _ => null
            },
            VolumeEntity volume => columnName switch
            {
                nameof(VolumeEntity.CreatedAt) => volume.CreatedAt,
                nameof(VolumeEntity.Driver) => volume.Driver,
                nameof(VolumeEntity.Mountpoint) => volume.Mountpoint,
                nameof(VolumeEntity.Name) => volume.Name,
                nameof(VolumeEntity.Scope) => volume.Scope,
                nameof(VolumeEntity.UsageData) => volume.UsageData,
                _ => null
            },
            _ => null
        };
    }

    private static object? ConvertLiteral(object? left, object? right)
    {
        if (left is DateTime && TryGetDateTime(right, out var date))
            return date;

        if (left is long && TryGetInt64(right, out var number))
            return number;

        return right;
    }

    private static bool TryGetComparisonParts(
        SourcePredicateComparison comparison,
        out string columnName,
        out SourcePredicateLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            columnName = NormalizeColumnName(leftColumn.Column.Name);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateLiteral leftLiteral)
        {
            columnName = NormalizeColumnName(rightColumn.Column.Name);
            literal = leftLiteral;
            op = Invert(comparison.Operator);
            return true;
        }

        columnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    private static SourcePredicateComparisonOperator Invert(SourcePredicateComparisonOperator op)
    {
        return op switch
        {
            SourcePredicateComparisonOperator.GreaterThan => SourcePredicateComparisonOperator.LessThan,
            SourcePredicateComparisonOperator.GreaterOrEqual => SourcePredicateComparisonOperator.LessOrEqual,
            SourcePredicateComparisonOperator.LessThan => SourcePredicateComparisonOperator.GreaterThan,
            SourcePredicateComparisonOperator.LessOrEqual => SourcePredicateComparisonOperator.GreaterOrEqual,
            _ => op
        };
    }

    private static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }

    private static bool IsStringComparison(object? value, SourcePredicateComparisonOperator op)
    {
        return value is string && IsEqualityOperator(op);
    }

    private static bool IsBoolComparison(object? value, SourcePredicateComparisonOperator op)
    {
        return value is bool && IsEqualityOperator(op);
    }

    private static bool IsIntegerComparison(object? value, SourcePredicateComparisonOperator op)
    {
        return IsComparisonOperator(op) && TryGetInt64(value, out _);
    }

    private static bool IsDateTimeComparison(object? value, SourcePredicateComparisonOperator op)
    {
        return IsComparisonOperator(op) && TryGetDateTime(value, out _);
    }

    private static bool IsEqualityOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual;
    }

    private static bool IsComparisonOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal
            or SourcePredicateComparisonOperator.NotEqual
            or SourcePredicateComparisonOperator.GreaterThan
            or SourcePredicateComparisonOperator.GreaterOrEqual
            or SourcePredicateComparisonOperator.LessThan
            or SourcePredicateComparisonOperator.LessOrEqual;
    }

    private static bool IsOneOf(string columnName, params string[] candidates)
    {
        return candidates.Any(candidate => columnName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetInt64(object? value, out long number)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long:
                number = Convert.ToInt64(value);
                return true;
            default:
                return long.TryParse(value?.ToString(), out number);
        }
    }

    private static bool TryGetDateTime(object? value, out DateTime date)
    {
        switch (value)
        {
            case DateTime dateTime:
                date = dateTime;
                return true;
            case DateTimeOffset dateTimeOffset:
                date = dateTimeOffset.DateTime;
                return true;
            case string text:
                return DateTime.TryParse(text, out date);
            default:
                date = default;
                return false;
        }
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
            return -1;

        return left is IComparable comparable ? comparable.CompareTo(right) : 0;
    }
}
