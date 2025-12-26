namespace Solution1.ClassLibrary1;

/// <summary>
/// Test class for new MethodEntity and PropertyEntity features
/// </summary>
public class TestFeatures
{
    // Field tests
    private readonly int _readonlyField = 10;
    private static string _staticField = "static";
    public const int ConstField = 42;
    private volatile bool _volatileField;
    
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
    
    // Constructors
    public TestFeatures()
    {
    }
    
    public TestFeatures(string value)
    {
        _backingField = value;
    }
    
    public TestFeatures(string value, int number) : this(value)
    {
        var x = number;
    }
    
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
    
    // Async method with awaits
    public async Task<int> AsyncMethodWithAwaits()
    {
        await Task.Delay(100);
        await Task.Delay(200);
        return 42;
    }
    
    // Method with lambda
    public void MethodWithLambda()
    {
        var numbers = new[] { 1, 2, 3 };
        var doubled = numbers.Select(x => x * 2);
        var tripled = numbers.Select(n => n * 3);
    }
    
    // Method with deep nesting
    public void DeeplyNestedMethod(bool a, bool b, bool c)
    {
        if (a)
        {
            if (b)
            {
                if (c)
                {
                    var x = 1;
                }
            }
        }
    }
    
    // Event field-like
    public event EventHandler? SimpleEvent;
}

/// <summary>
/// Test struct for struct entity testing
/// </summary>
public struct TestStruct
{
    public int Value { get; set; }
    public string Name { get; init; }
    
    private readonly int _privateField;
    
    public TestStruct(int value, string name)
    {
        Value = value;
        Name = name;
        _privateField = value;
    }
    
    public int GetValue() => Value;
}

/// <summary>
/// Test readonly struct
/// </summary>
public readonly struct ReadOnlyTestStruct
{
    public int Value { get; }
    
    public ReadOnlyTestStruct(int value)
    {
        Value = value;
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

/// <summary>
/// Class implementing interface for FindImplementations test
/// </summary>
public class InterfaceImplementor : IInterfaceWithMethods
{
    public void InterfaceMethod()
    {
        throw new NotImplementedException();
    }
    
    public string InterfaceProperty { get; set; }
}

/// <summary>
/// Class for testing unused code detection
/// </summary>
public class UnusedCodeTestClass
{
    // Unused field for testing
    private int _unusedField;
    
    // Used field
    private int _usedField;
    
    public UnusedCodeTestClass()
    {
        _usedField = 42;
    }
    
    /// <summary>
    /// Method with unused parameter
    /// </summary>
    public void MethodWithUnusedParameter(int usedParam, string unusedParam)
    {
        var result = usedParam * 2;
        Console.WriteLine(result);
    }
    
    /// <summary>
    /// Method with unused local variable
    /// </summary>
    public void MethodWithUnusedVariable()
    {
        var unusedVar = 10;
        var usedVar = 20;
        Console.WriteLine(usedVar);
    }
    
    /// <summary>
    /// Method with all parameters used
    /// </summary>
    public int MethodWithAllParamsUsed(int a, int b)
    {
        return a + b;
    }
    
    /// <summary>
    /// Method with multiple unused parameters
    /// </summary>
    public void MethodWithMultipleUnusedParams(int a, int b, int c)
    {
        // None of the parameters are used
    }
    
    /// <summary>
    /// Private method that is never called (unused method)
    /// </summary>
    private void UnusedPrivateMethod()
    {
        Console.WriteLine("Never called");
    }
    
    /// <summary>
    /// Gets the used field value
    /// </summary>
    public int GetUsedField()
    {
        return _usedField;
    }
}

/// <summary>
/// Unused interface for testing IsUsed property
/// </summary>
public interface IUnusedInterface
{
    void DoSomething();
}

/// <summary>
/// Used interface for testing IsUsed property
/// </summary>
public interface IUsedInterface
{
    void DoSomethingUseful();
}

/// <summary>
/// Class that uses IUsedInterface
/// </summary>
public class UsedInterfaceImplementor : IUsedInterface
{
    public void DoSomethingUseful()
    {
        Console.WriteLine("Used");
    }
}

/// <summary>
/// Unused enum for testing IsUsed property
/// </summary>
public enum UnusedEnum
{
    Value1,
    Value2
}

/// <summary>
/// Used enum for testing IsUsed property
/// </summary>
public enum UsedEnum
{
    Used1,
    Used2
}

/// <summary>
/// Class that uses UsedEnum
/// </summary>
public class EnumUser
{
    public UsedEnum GetEnum() => UsedEnum.Used1;
}

/// <summary>
/// Unused struct for testing IsUsed property
/// </summary>
public struct UnusedStruct
{
    public int Value { get; set; }
}

/// <summary>
/// Used struct for testing IsUsed property
/// </summary>
public struct UsedStruct
{
    public int Value { get; set; }
}

/// <summary>
/// Class that uses UsedStruct
/// </summary>
public class StructUser
{
    public UsedStruct GetStruct() => new UsedStruct { Value = 42 };
}

