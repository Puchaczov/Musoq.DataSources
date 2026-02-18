using Musoq.Parser.Nodes;

namespace Musoq.DataSources.Roslyn.RowsSources;

/// <summary>
/// Filter parameters extracted from WHERE clause for Roslyn solution queries.
/// Used to selectively initialize Roslyn project documents, avoiding expensive
/// InitializeAsync calls for projects that do not match the WHERE predicate.
/// </summary>
internal class RoslynFilterParameters
{
    /// <summary>Gets or sets the project assembly name filter (projects).</summary>
    public string? AssemblyName { get; set; }

    /// <summary>Gets or sets the project name filter (projects).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the project language filter (e.g. "C#").</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the project default namespace filter.</summary>
    public string? DefaultNamespace { get; set; }
}

/// <summary>
/// Helper class to extract filter parameters from WHERE clause nodes for Roslyn queries.
/// </summary>
internal static class RoslynWhereNodeHelper
{
    /// <summary>
    /// Extracts Roslyn filter parameters from a WHERE node.
    /// </summary>
    public static RoslynFilterParameters ExtractParameters(WhereNode? whereNode)
    {
        var parameters = new RoslynFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    private static void ExtractFromNode(Node node, RoslynFilterParameters parameters)
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
        }
    }

    private static void ExtractEqualityCondition(EqualityNode node, RoslynFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        
        var bareFieldName = fieldName.Contains('.')
            ? fieldName[(fieldName.LastIndexOf('.') + 1)..]
            : fieldName;

        switch (bareFieldName.ToLowerInvariant())
        {
            case "assemblyname":
                parameters.AssemblyName = value.ToString();
                break;
            case "name":
                parameters.Name = value.ToString();
                break;
            case "language":
                parameters.Language = value.ToString();
                break;
            case "defaultnamespace":
                parameters.DefaultNamespace = value.ToString();
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
