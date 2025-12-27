namespace Solution1.ClassLibrary1;

/// <summary>
/// Test class for new MethodEntity and PropertyEntity features
/// </summary>
public class TestFeatures
{
    // Auto-property tests
    public string AutoProperty { get; set; }
    public string AutoPropertyWithInit { get; init; }
    public string AutoPropertyReadOnly { get; }
    
    // Property with custom getter
    private string _backingField;
    public string PropertyWithCustomGetter
    {
        get { return _backingField; }
        set { _backingField = value; }
    }
    
    // Expression-bodied property
    public string ExpressionBodiedProperty => "test";
    
    // Property with only getter
    public string GetterOnly { get; }
    
    // Property with only init setter
    public string InitOnly { init; }
    
    // Method with no body (abstract would require abstract class, so using partial)
    public partial void PartialMethodNoBody();
    
    // Method with empty body
    public void EmptyMethod()
    {
    }
    
    // Method with only comments
    public void MethodWithOnlyComments()
    {
        // This is just a comment
        // Another comment
    }
    
    // Method with single statement
    public void SingleStatementMethod()
    {
        var x = 1;
    }
    
    // Method with multiple statements
    public void MultipleStatementsMethod()
    {
        var x = 1;
        var y = 2;
        var z = x + y;
    }
    
    // Expression-bodied method
    public int ExpressionBodiedMethod() => 42;
    
    // Method with nested blocks (for statement count testing)
    public void MethodWithNestedBlocks()
    {
        if (true)
        {
            var x = 1;
        }
        var y = 2;
    }
}

public abstract class AbstractClassWithAbstractMethod
{
    public abstract void AbstractMethod();
    
    public abstract string AbstractProperty { get; }
}

public interface IInterfaceWithMethods
{
    void InterfaceMethod();
    string InterfaceProperty { get; set; }
}
