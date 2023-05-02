using Musoq.Parser;

namespace Musoq.DataSources.Databases.Visitors;

public class TraverseVisitor : RawTraverseVisitor<IExpressionVisitor>
{
    protected TraverseVisitor(IExpressionVisitor visitor) 
        : base(visitor)
    {
    }
}