using Musoq.Parser.Nodes;

namespace Musoq.DataSources.Os;

/// <summary>
///     Filter parameters extracted from WHERE clause for Os file/directory queries.
/// </summary>
internal class OsFileFilterParameters
{
    /// <summary>Gets or sets the file extension filter (e.g. ".txt").</summary>
    public string? Extension { get; set; }

    /// <summary>Gets or sets the file name filter (e.g. "file.txt" or "*.txt").</summary>
    public string? Name { get; set; }
}

/// <summary>
///     Filter parameters extracted from WHERE clause for Os directory queries.
/// </summary>
internal class OsDirectoryFilterParameters
{
    /// <summary>Gets or sets the directory name filter.</summary>
    public string? Name { get; set; }
}

/// <summary>
///     Helper class to extract filter parameters from WHERE clause nodes for Os queries.
/// </summary>
internal static class OsWhereNodeHelper
{
    /// <summary>
    ///     Extracts Os file filter parameters from a WHERE node.
    /// </summary>
    public static OsFileFilterParameters ExtractFileParameters(WhereNode? whereNode)
    {
        var parameters = new OsFileFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractFileFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    /// <summary>
    ///     Extracts Os directory filter parameters from a WHERE node.
    /// </summary>
    public static OsDirectoryFilterParameters ExtractDirectoryParameters(WhereNode? whereNode)
    {
        var parameters = new OsDirectoryFilterParameters();

        if (whereNode?.Expression == null)
            return parameters;

        ExtractDirectoryFromNode(whereNode.Expression, parameters);

        return parameters;
    }

    private static void ExtractFileFromNode(Node node, OsFileFilterParameters parameters)
    {
        switch (node)
        {
            case AndNode andNode:
                ExtractFileFromNode(andNode.Left, parameters);
                ExtractFileFromNode(andNode.Right, parameters);
                break;

            case OrNode:

                break;

            case EqualityNode equalityNode:
                ExtractFileEqualityCondition(equalityNode, parameters);
                break;
        }
    }

    private static void ExtractDirectoryFromNode(Node node, OsDirectoryFilterParameters parameters)
    {
        switch (node)
        {
            case AndNode andNode:
                ExtractDirectoryFromNode(andNode.Left, parameters);
                ExtractDirectoryFromNode(andNode.Right, parameters);
                break;

            case OrNode:

                break;

            case EqualityNode equalityNode:
                ExtractDirectoryEqualityCondition(equalityNode, parameters);
                break;
        }
    }

    private static void ExtractFileEqualityCondition(EqualityNode node, OsFileFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        switch (fieldName.ToLowerInvariant())
        {
            case "extension":
                parameters.Extension = value.ToString();
                break;
            case "name":
            case "filename":
                parameters.Name = value.ToString();
                break;
        }
    }

    private static void ExtractDirectoryEqualityCondition(EqualityNode node, OsDirectoryFilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);

        if (fieldName == null || value == null)
            return;

        switch (fieldName.ToLowerInvariant())
        {
            case "name":
                parameters.Name = value.ToString();
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