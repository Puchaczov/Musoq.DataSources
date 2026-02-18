using System;
using Musoq.Parser.Nodes;

namespace Musoq.DataSources.Git;

/// <summary>
/// Filter parameters extracted from WHERE clause for Git queries.
/// </summary>
internal class GitFilterParameters
{
    /// <summary>Gets or sets the author name filter (commits).</summary>
    public string? Author { get; set; }

    /// <summary>Gets or sets the author email filter (commits).</summary>
    public string? AuthorEmail { get; set; }

    /// <summary>Gets or sets the committer name filter (commits).</summary>
    public string? Committer { get; set; }

    /// <summary>Gets or sets the committer email filter (commits).</summary>
    public string? CommitterEmail { get; set; }

    /// <summary>Gets or sets the commit SHA filter.</summary>
    public string? Sha { get; set; }

    /// <summary>Gets or sets the since date filter (commits on or after).</summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>Gets or sets the until date filter (commits on or before).</summary>
    public DateTimeOffset? Until { get; set; }

    /// <summary>Gets or sets the friendly name filter (branches, tags).</summary>
    public string? FriendlyName { get; set; }

    /// <summary>Gets or sets the canonical name filter (branches, tags).</summary>
    public string? CanonicalName { get; set; }

    /// <summary>Gets or sets the IsRemote filter (branches).</summary>
    public bool? IsRemote { get; set; }

    /// <summary>Gets or sets the IsCurrentRepositoryHead filter (branches).</summary>
    public bool? IsCurrentRepositoryHead { get; set; }

    /// <summary>Gets or sets the IsTracking filter (branches).</summary>
    public bool? IsTracking { get; set; }

    /// <summary>Gets or sets the IsAnnotated filter (tags).</summary>
    public bool? IsAnnotated { get; set; }

    /// <summary>Gets or sets the remote name filter (remotes).</summary>
    public string? RemoteName { get; set; }

    /// <summary>Gets or sets the remote URL filter (remotes).</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets the status state filter (status).</summary>
    public string? State { get; set; }
}

/// <summary>
/// Helper class to extract filter parameters from WHERE clause nodes for Git queries.
/// </summary>
internal static class GitWhereNodeHelper
{
    /// <summary>
    /// Extracts Git filter parameters from a WHERE node.
    /// </summary>
    public static GitFilterParameters ExtractParameters(WhereNode? whereNode)
    {
        var parameters = new GitFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    private static void ExtractFromNode(Node node, GitFilterParameters parameters)
    {
        switch (node)
        {
            case AndNode andNode:
                ExtractFromNode(andNode.Left, parameters);
                ExtractFromNode(andNode.Right, parameters);
                break;

            case OrNode:
                // OR conditions are complex - skip for pushdown
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

    private static void ExtractEqualityCondition(EqualityNode node, GitFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        switch (fieldName.ToLowerInvariant())
        {
            case "author":
                parameters.Author = value.ToString();
                break;
            case "authoremail":
                parameters.AuthorEmail = value.ToString();
                break;
            case "committer":
                parameters.Committer = value.ToString();
                break;
            case "committeremail":
                parameters.CommitterEmail = value.ToString();
                break;
            case "sha":
                parameters.Sha = value.ToString();
                break;
            case "friendlyname":
                parameters.FriendlyName = value.ToString();
                break;
            case "canonicalname":
                parameters.CanonicalName = value.ToString();
                break;
            case "isremote":
                if (bool.TryParse(value.ToString(), out var isRemote))
                    parameters.IsRemote = isRemote;
                break;
            case "iscurrentrepositoryhead":
                if (bool.TryParse(value.ToString(), out var isHead))
                    parameters.IsCurrentRepositoryHead = isHead;
                break;
            case "istracking":
                if (bool.TryParse(value.ToString(), out var isTracking))
                    parameters.IsTracking = isTracking;
                break;
            case "isannotated":
                if (bool.TryParse(value.ToString(), out var isAnnotated))
                    parameters.IsAnnotated = isAnnotated;
                break;
            case "name":
            case "remotename":
                parameters.RemoteName = value.ToString();
                break;
            case "url":
                parameters.Url = value.ToString();
                break;
            case "state":
                parameters.State = value.ToString();
                break;
        }
    }

    private static void ExtractComparisonCondition(Node node, string op, GitFilterParameters parameters)
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
            case "committedwhen":
                if (DateTimeOffset.TryParse(value.ToString(), out var date))
                {
                    if (op is ">=" or ">")
                        parameters.Since = date;
                    else if (op is "<=" or "<")
                        parameters.Until = date;
                }
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
