using Musoq.Parser.Nodes;

namespace Musoq.DataSources.GitHub.Helpers;

/// <summary>
///     Result of extracting filter parameters from WHERE clause for GitHub API queries.
/// </summary>
internal class GitHubFilterParameters
{
    /// <summary>
    ///     Gets or sets the state filter (open, closed, all).
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    ///     Gets or sets the author/creator filter.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Gets or sets the assignee filter.
    /// </summary>
    public string? Assignee { get; set; }

    /// <summary>
    ///     Gets or sets labels to filter by.
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    ///     Gets or sets the milestone filter.
    /// </summary>
    public string? Milestone { get; set; }

    /// <summary>
    ///     Gets or sets the head branch filter (for PRs).
    /// </summary>
    public string? Head { get; set; }

    /// <summary>
    ///     Gets or sets the base branch filter (for PRs).
    /// </summary>
    public string? Base { get; set; }

    /// <summary>
    ///     Gets or sets the sort field.
    /// </summary>
    public string? Sort { get; set; }

    /// <summary>
    ///     Gets or sets the sort direction.
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    ///     Gets or sets the since date filter.
    /// </summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>
    ///     Gets or sets the SHA filter (for commits).
    /// </summary>
    public string? Sha { get; set; }

    /// <summary>
    ///     Gets or sets the path filter (for commits).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    ///     Gets or sets the search query for full-text search.
    /// </summary>
    public string? SearchQuery { get; set; }

    /// <summary>
    ///     Gets or sets the language filter.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///     Gets or sets the visibility filter (for repos).
    /// </summary>
    public string? Visibility { get; set; }

    /// <summary>
    ///     Gets or sets whether to filter archived repos.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    ///     Gets or sets whether to filter fork repos.
    /// </summary>
    public bool? IsFork { get; set; }
}

/// <summary>
///     Helper class to extract filter parameters from WHERE clause nodes for GitHub API.
/// </summary>
internal static class WhereNodeHelper
{
    /// <summary>
    ///     Extracts GitHub filter parameters from a WHERE node.
    /// </summary>
    public static GitHubFilterParameters ExtractParameters(WhereNode? whereNode)
    {
        var parameters = new GitHubFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    private static void ExtractFromNode(Node node, GitHubFilterParameters parameters)
    {
        switch (node)
        {
            case AndNode andNode:
                ExtractFromNode(andNode.Left, parameters);
                ExtractFromNode(andNode.Right, parameters);
                break;

            case OrNode:

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
        }
    }

    private static void ExtractEqualityCondition(EqualityNode node, GitHubFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        switch (fieldName.ToLowerInvariant())
        {
            case "state":
                parameters.State = value.ToString();
                break;
            case "authorlogin":
            case "author":
                parameters.Author = value.ToString();
                break;
            case "assigneelogin":
            case "assignee":
                parameters.Assignee = value.ToString();
                break;
            case "milestonetitle":
            case "milestone":
                parameters.Milestone = value.ToString();
                break;
            case "headref":
            case "head":
                parameters.Head = value.ToString();
                break;
            case "baseref":
            case "base":
                parameters.Base = value.ToString();
                break;
            case "sha":
                parameters.Sha = value.ToString();
                break;
            case "language":
                parameters.Language = value.ToString();
                break;
            case "visibility":
                parameters.Visibility = value.ToString();
                break;
            case "isarchived":
                if (bool.TryParse(value.ToString(), out var archived))
                    parameters.IsArchived = archived;
                break;
            case "isfork":
                if (bool.TryParse(value.ToString(), out var fork))
                    parameters.IsFork = fork;
                break;
        }
    }

    private static void ExtractComparisonCondition(Node node, string op, GitHubFilterParameters parameters)
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

        switch (fieldName.ToLowerInvariant())
        {
            case "createdat":
            case "updatedat":
                if (op is ">=" or ">")
                    if (DateTimeOffset.TryParse(value.ToString(), out var since))
                        parameters.Since = since;
                break;
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
}