using Musoq.DataSources.Databases.Visitors;
using Musoq.Parser;
using Musoq.Parser.Nodes;

namespace Musoq.DataSources.Postgres.Visitors;

internal class ToStringWhereQueryPartTraverseVisitor : TraverseVisitor
{
    public ToStringWhereQueryPartTraverseVisitor(IExpressionVisitor visitor)
        : base(visitor)
    {
    }

    public override void Visit(StarNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(FSlashNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(ModuloNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(AddNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(HyphenNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(AndNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(OrNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(EqualityNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(GreaterOrEqualNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(LessOrEqualNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(GreaterNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(LessNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }

    public override void Visit(DiffNode node)
    {
        node.Left.Accept(this);
        node.Accept(Visitor);
        node.Right.Accept(this);
    }
}