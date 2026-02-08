using System.Globalization;
using System.Text;
using Musoq.Parser.Nodes;

namespace Musoq.DataSources.Jira.Helpers;

/// <summary>
/// Parameters extracted from WHERE clause for Jira API queries.
/// </summary>
internal class JiraFilterParameters
{
    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the issue type filter.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the priority filter.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Gets or sets the resolution filter.
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// Gets or sets the assignee filter.
    /// </summary>
    public string? Assignee { get; set; }

    /// <summary>
    /// Gets or sets the reporter filter.
    /// </summary>
    public string? Reporter { get; set; }

    /// <summary>
    /// Gets or sets the project key filter.
    /// </summary>
    public string? ProjectKey { get; set; }

    /// <summary>
    /// Gets or sets the issue key filter.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets labels to filter by.
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    /// Gets or sets components to filter by.
    /// </summary>
    public List<string> Components { get; set; } = [];

    /// <summary>
    /// Gets or sets the created date range start.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the created date range end.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the updated date range start.
    /// </summary>
    public DateTimeOffset? UpdatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the updated date range end.
    /// </summary>
    public DateTimeOffset? UpdatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the parent issue key (for subtasks).
    /// </summary>
    public string? ParentKey { get; set; }

    /// <summary>
    /// Gets or sets the fix version filter.
    /// </summary>
    public string? FixVersion { get; set; }

    /// <summary>
    /// Gets or sets the text search query.
    /// </summary>
    public string? TextSearch { get; set; }

    /// <summary>
    /// Gets or sets the summary search query.
    /// </summary>
    public string? SummaryContains { get; set; }
}

/// <summary>
/// Helper class to extract filter parameters from WHERE clause nodes and build JQL queries.
/// </summary>
internal static class JqlBuilder
{
    /// <summary>
    /// Field name mappings from entity properties to JQL field names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        {"status", "status"},
        {"type", "issuetype"},
        {"priority", "priority"},
        {"resolution", "resolution"},
        {"assignee", "assignee"},
        {"assigneedisplayname", "assignee"},
        {"reporter", "reporter"},
        {"reporterdisplayname", "reporter"},
        {"projectkey", "project"},
        {"key", "key"},
        {"labels", "labels"},
        {"components", "component"},
        {"createdat", "created"},
        {"updatedat", "updated"},
        {"resolvedat", "resolved"},
        {"duedate", "duedate"},
        {"parentkey", "parent"},
        {"fixversions", "fixVersion"},
        {"affectsversions", "affectedVersion"},
        {"summary", "summary"},
        {"description", "description"},
        {"environment", "environment"}
    };

    /// <summary>
    /// Extracts Jira filter parameters from a WHERE node.
    /// </summary>
    public static JiraFilterParameters ExtractParameters(WhereNode? whereNode)
    {
        var parameters = new JiraFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    /// <summary>
    /// Builds a JQL query string from filter parameters.
    /// </summary>
    /// <param name="baseJql">Optional base JQL to extend (e.g., "project = PROJ")</param>
    /// <param name="parameters">Filter parameters extracted from WHERE clause</param>
    /// <returns>Complete JQL query string</returns>
    public static string BuildJql(string? baseJql, JiraFilterParameters parameters)
    {
        var conditions = new List<string>();

        if (!string.IsNullOrEmpty(baseJql))
        {
            conditions.Add(baseJql);
        }

        if (!string.IsNullOrEmpty(parameters.Status))
        {
            conditions.Add($"status = \"{EscapeJql(parameters.Status)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.Type))
        {
            conditions.Add($"issuetype = \"{EscapeJql(parameters.Type)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.Priority))
        {
            conditions.Add($"priority = \"{EscapeJql(parameters.Priority)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.Resolution))
        {
            conditions.Add($"resolution = \"{EscapeJql(parameters.Resolution)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.Assignee))
        {
            if (parameters.Assignee.Equals("unassigned", StringComparison.OrdinalIgnoreCase) ||
                parameters.Assignee.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                conditions.Add("assignee is EMPTY");
            }
            else
            {
                conditions.Add($"assignee = \"{EscapeJql(parameters.Assignee)}\"");
            }
        }

        if (!string.IsNullOrEmpty(parameters.Reporter))
        {
            conditions.Add($"reporter = \"{EscapeJql(parameters.Reporter)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.ProjectKey))
        {
            conditions.Add($"project = {parameters.ProjectKey}");
        }

        if (!string.IsNullOrEmpty(parameters.Key))
        {
            conditions.Add($"key = {parameters.Key}");
        }

        if (!string.IsNullOrEmpty(parameters.ParentKey))
        {
            conditions.Add($"parent = {parameters.ParentKey}");
        }

        if (!string.IsNullOrEmpty(parameters.FixVersion))
        {
            conditions.Add($"fixVersion = \"{EscapeJql(parameters.FixVersion)}\"");
        }

        foreach (var label in parameters.Labels)
        {
            conditions.Add($"labels = \"{EscapeJql(label)}\"");
        }

        foreach (var component in parameters.Components)
        {
            conditions.Add($"component = \"{EscapeJql(component)}\"");
        }

        if (parameters.CreatedAfter.HasValue)
        {
            conditions.Add($"created >= \"{FormatDate(parameters.CreatedAfter.Value)}\"");
        }

        if (parameters.CreatedBefore.HasValue)
        {
            conditions.Add($"created <= \"{FormatDate(parameters.CreatedBefore.Value)}\"");
        }

        if (parameters.UpdatedAfter.HasValue)
        {
            conditions.Add($"updated >= \"{FormatDate(parameters.UpdatedAfter.Value)}\"");
        }

        if (parameters.UpdatedBefore.HasValue)
        {
            conditions.Add($"updated <= \"{FormatDate(parameters.UpdatedBefore.Value)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.SummaryContains))
        {
            conditions.Add($"summary ~ \"{EscapeJql(parameters.SummaryContains)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.TextSearch))
        {
            conditions.Add($"text ~ \"{EscapeJql(parameters.TextSearch)}\"");
        }

        return conditions.Count > 0 
            ? string.Join(" AND ", conditions) 
            : "order by created DESC";
    }

    /// <summary>
    /// Builds a JQL query string directly from a WHERE node.
    /// </summary>
    /// <param name="baseJql">Optional base JQL to extend</param>
    /// <param name="whereNode">WHERE clause node</param>
    /// <returns>Complete JQL query string</returns>
    public static string BuildJqlFromWhereNode(string? baseJql, WhereNode? whereNode)
    {
        var parameters = ExtractParameters(whereNode);
        return BuildJql(baseJql, parameters);
    }

    private static void ExtractFromNode(Node node, JiraFilterParameters parameters)
    {
        switch (node)
        {
            case AndNode andNode:
                ExtractFromNode(andNode.Left, parameters);
                ExtractFromNode(andNode.Right, parameters);
                break;

            case OrNode:
                // OR conditions are complex for JQL pushdown - skip for now
                // The runtime will filter these after fetching
                break;

            case EqualityNode equalityNode:
                ExtractEqualityCondition(equalityNode, parameters);
                break;

            case GreaterOrEqualNode greaterEqualNode:
                ExtractComparisonCondition(greaterEqualNode, ">=", parameters);
                break;

            case LessOrEqualNode lessEqualNode:
                ExtractComparisonCondition(lessEqualNode, "<=", parameters);
                break;

            case GreaterNode greaterNode:
                ExtractComparisonCondition(greaterNode, ">", parameters);
                break;

            case LessNode lessNode:
                ExtractComparisonCondition(lessNode, "<", parameters);
                break;

            case LikeNode likeNode:
                ExtractLikeCondition(likeNode, parameters);
                break;
        }
    }

    private static void ExtractEqualityCondition(EqualityNode node, JiraFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        var lowerFieldName = fieldName.ToLowerInvariant();
        
        switch (lowerFieldName)
        {
            case "status":
                parameters.Status = value.ToString();
                break;
            case "type":
                parameters.Type = value.ToString();
                break;
            case "priority":
                parameters.Priority = value.ToString();
                break;
            case "resolution":
                parameters.Resolution = value.ToString();
                break;
            case "assignee":
            case "assigneedisplayname":
                parameters.Assignee = value.ToString();
                break;
            case "reporter":
            case "reporterdisplayname":
                parameters.Reporter = value.ToString();
                break;
            case "projectkey":
                parameters.ProjectKey = value.ToString();
                break;
            case "key":
                parameters.Key = value.ToString();
                break;
            case "parentkey":
                parameters.ParentKey = value.ToString();
                break;
        }
    }

    private static void ExtractComparisonCondition(Node node, string op, JiraFilterParameters parameters)
    {
        Node left, right;

        switch (node)
        {
            case GreaterOrEqualNode ge:
                left = ge.Left;
                right = ge.Right;
                break;
            case LessOrEqualNode le:
                left = le.Left;
                right = le.Right;
                break;
            case GreaterNode g:
                left = g.Left;
                right = g.Right;
                break;
            case LessNode l:
                left = l.Left;
                right = l.Right;
                break;
            default:
                return;
        }

        var (fieldName, value) = ExtractFieldAndValue(left, right);

        if (fieldName == null || value == null)
            return;

        var lowerFieldName = fieldName.ToLowerInvariant();
        
        // Handle date comparisons
        if (lowerFieldName == "createdat" && value is DateTimeOffset createdDate)
        {
            if (op is ">=" or ">")
                parameters.CreatedAfter = createdDate;
            else if (op is "<=" or "<")
                parameters.CreatedBefore = createdDate;
        }
        else if (lowerFieldName == "updatedat" && value is DateTimeOffset updatedDate)
        {
            if (op is ">=" or ">")
                parameters.UpdatedAfter = updatedDate;
            else if (op is "<=" or "<")
                parameters.UpdatedBefore = updatedDate;
        }
    }

    private static void ExtractLikeCondition(LikeNode node, JiraFilterParameters parameters)
    {
        if (node.Left is not FieldNode fieldNode)
            return;

        var pattern = node.Right switch
        {
            StringNode stringNode => stringNode.Value,
            _ => null
        };

        if (pattern == null)
            return;

        // Remove SQL LIKE wildcards for JQL contains search
        var searchText = pattern.Trim('%', '_');
        
        var lowerFieldName = fieldNode.FieldName.ToLowerInvariant();
        
        if (lowerFieldName == "summary")
        {
            parameters.SummaryContains = searchText;
        }
        else if (lowerFieldName == "description" || lowerFieldName == "text")
        {
            parameters.TextSearch = searchText;
        }
    }

    private static (string? fieldName, object? value) ExtractFieldAndValue(Node left, Node right)
    {
        string? fieldName = null;
        object? value = null;

        if (left is FieldNode fieldNode)
        {
            fieldName = fieldNode.FieldName;
            value = ExtractValue(right);
        }
        else if (right is FieldNode fieldNode2)
        {
            fieldName = fieldNode2.FieldName;
            value = ExtractValue(left);
        }

        return (fieldName, value);
    }

    private static object? ExtractValue(Node node)
    {
        return node switch
        {
            StringNode stringNode => stringNode.Value,
            IntegerNode intNode => intNode.ObjValue,
            DecimalNode decimalNode => decimalNode.Value,
            BooleanNode boolNode => boolNode.Value,
            _ => null
        };
    }

    private static string EscapeJql(string value)
    {
        // Escape special JQL characters
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string FormatDate(DateTimeOffset date)
    {
        // JQL date format: yyyy-MM-dd or yyyy-MM-dd HH:mm
        return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
