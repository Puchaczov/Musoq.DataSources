using System.Text;
using Musoq.Parser;
using Musoq.Parser.Nodes;
using Musoq.Parser.Nodes.From;
using Musoq.Parser.Nodes.InterpretationSchema;

namespace Musoq.DataSources.Postgres.Visitors;

internal class ToStringWhereQueryPartVisitor : IExpressionVisitor
{
    private readonly StringBuilder _builder = new();
    
    public string StringifiedWherePart => _builder.ToString();
    
    public void Visit(Node node)
    {
        throw new NotImplementedException();
    }

    public void Visit(DescNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(StarNode node)
    {
        _builder.Append(" * ");
    }

    public void Visit(FSlashNode node)
    {
        _builder.Append(" / ");
    }

    public void Visit(ModuloNode node)
    {
        _builder.Append(" % ");
    }

    public void Visit(AddNode node)
    {
        _builder.Append(" + ");
    }

    public void Visit(HyphenNode node)
    {
        _builder.Append(" - ");
    }

    public void Visit(AndNode node)
    {
        _builder.Append(" AND ");
    }

    public void Visit(OrNode node)
    {
        _builder.Append(" OR ");
    }

    public void Visit(ShortCircuitingNodeLeft node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ShortCircuitingNodeRight node)
    {
        throw new NotImplementedException();
    }

    public void Visit(EqualityNode node)
    {
        _builder.Append(" = ");
    }

    public void Visit(GreaterOrEqualNode node)
    {
        _builder.Append(" >= ");
    }

    public void Visit(LessOrEqualNode node)
    {
        _builder.Append(" <= ");
    }

    public void Visit(GreaterNode node)
    {
        _builder.Append(" > ");
    }

    public void Visit(LessNode node)
    {
        _builder.Append(" < ");
    }

    public void Visit(DiffNode node)
    {
        _builder.Append(" <> ");
    }

    public void Visit(NotNode node)
    {
        _builder.Append(" NOT ");
    }

    public void Visit(LikeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RLikeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(FieldNode node)
    {
        _builder.Append($" {node.FieldName} ");
    }

    public void Visit(FieldOrderedNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(StringNode node)
    {
        _builder.Append($" '{node.Value}' ");
    }

    public void Visit(DecimalNode node)
    {
        _builder.Append($" {node.Value} ");
    }

    public void Visit(IntegerNode node)
    {
        _builder.Append($" {node.ObjValue} ");
    }

    public void Visit(HexIntegerNode node)
    {
        _builder.Append($" {node.ObjValue} ");
    }

    public void Visit(BinaryIntegerNode node)
    {
        _builder.Append($" {node.ObjValue} ");
    }

    public void Visit(OctalIntegerNode node)
    {
        _builder.Append($" {node.ObjValue} ");
    }

    public void Visit(BooleanNode node)
    {
        _builder.Append($" {node.Value} ");
    }

    public void Visit(WordNode node)
    {
        _builder.Append($" '{node.Value}' ");
    }

    public void Visit(NullNode node)
    {
        _builder.Append(" NULL ");
    }

    public void Visit(ContainsNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessMethodNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessRawIdentifierNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(IsNullNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessRefreshAggregationScoreNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessColumnNode node)
    {
        _builder.Append($" \"{node.Name}\" ");
    }

    public void Visit(AllColumnsNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(IdentifierNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessObjectArrayNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessObjectKeyNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(PropertyValueNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(DotNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessCallChainNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ArgsListNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SelectNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(GroupSelectNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(WhereNode node)
    {
    }

    public void Visit(GroupByNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(HavingNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SkipNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TakeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(JoinInMemoryWithSourceTableFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ApplyInMemoryWithSourceTableFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SchemaFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AliasedFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(JoinSourcesTableFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ApplySourcesTableFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InMemoryTableFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(JoinFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ApplyFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ExpressionFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SchemaMethodFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(PropertyFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AccessMethodFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CreateTransformationTableNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RenameTableNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TranslatedSetTreeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(IntoNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(QueryScope node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ShouldBePresentInTheTable node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TranslatedSetOperatorNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(QueryNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InternalQueryNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RootNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SingleSetNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(UnionNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(UnionAllNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ExceptNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RefreshNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(IntersectNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(PutTrueNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(MultiStatementNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(StatementsArrayNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(StatementNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CteExpressionNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CteInnerExpressionNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(JoinNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ApplyNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(OrderByNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CreateTableNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CoupleNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(CaseNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(WhenNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ThenNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ElseNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(FieldLinkNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(BitwiseAndNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(BitwiseOrNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(BitwiseXorNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(LeftShiftNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RightShiftNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InterpretFromNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InterpretCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ParseCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InterpretAtCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TryInterpretCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TryParseCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(PartialInterpretCallNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(BinarySchemaNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TextSchemaNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(FieldDefinitionNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(TextFieldDefinitionNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ComputedFieldNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(FieldConstraintNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(PrimitiveTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ByteArrayTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(StringTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(SchemaReferenceTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(ArrayTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(BitsTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(AlignmentNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(RepeatUntilTypeNode node)
    {
        throw new NotImplementedException();
    }

    public void Visit(InlineSchemaTypeNode node)
    {
        throw new NotImplementedException();
    }
}