namespace Solution1.ClassLibrary1;

/// <summary>
/// Test class for new MethodEntity and PropertyEntity features
/// </summary>
public class TestFeatures
{
    private readonly int _readonlyField = 10;
    private static string _staticField = "static";
    public const int ConstField = 42;
    private volatile bool _volatileField;
    
    public string AutoProperty { get; set; }
    public string AutoPropertyWithInit { get; init; }
    public string AutoPropertyReadOnly { get; }
    
    private string _backingField;
    public string PropertyWithCustomGetter
    {
        get { return _backingField; }
        set { _backingField = value; }
    }
    
    public string ExpressionBodiedProperty => "test";
    
    public string GetterOnly { get; }
    
    public string InitOnly { init; }
    
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
    
    public partial void PartialMethodNoBody();
    
    public void EmptyMethod()
    {
    }
    
    public void MethodWithOnlyComments()
    {
        // This is just a comment
        // Another comment
    }
    
    public void SingleStatementMethod()
    {
        var x = 1;
    }
    
    public void MultipleStatementsMethod()
    {
        var x = 1;
        var y = 2;
        var z = x + y;
    }
    
    public int ExpressionBodiedMethod() => 42;
    
    public void MethodWithNestedBlocks()
    {
        if (true)
        {
            var x = 1;
        }
        var y = 2;
    }
    
    public async Task<int> AsyncMethodWithAwaits()
    {
        await Task.Delay(100);
        await Task.Delay(200);
        return 42;
    }
    
    public void MethodWithLambda()
    {
        var numbers = new[] { 1, 2, 3 };
        var doubled = numbers.Select(x => x * 2);
        var tripled = numbers.Select(n => n * 3);
    }
    
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
    private int _unusedField;
    
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
/// Class that uses a method from UnusedCodeTestClass
/// </summary>
public class UsedMethodCaller
{
    public int CallGetUsedField()
    {
        var instance = new UnusedCodeTestClass();
        return instance.GetUsedField();
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

/// <summary>
/// Class for testing call graph features (Callees, IsRecursive)
/// </summary>
public class CallGraphTestClass
{
    /// <summary>
    /// Method that calls other methods
    /// </summary>
    public void CallerMethod()
    {
        HelperMethod1();
        HelperMethod2();
        Console.WriteLine("Calling helpers");
    }
    
    /// <summary>
    /// First helper method
    /// </summary>
    public void HelperMethod1()
    {
        Console.WriteLine("Helper 1");
    }
    
    /// <summary>
    /// Second helper method
    /// </summary>
    public void HelperMethod2()
    {
        Console.WriteLine("Helper 2");
    }
    
    /// <summary>
    /// Recursive method that calls itself
    /// </summary>
    public int RecursiveMethod(int n)
    {
        if (n <= 1)
            return 1;
        return n * RecursiveMethod(n - 1);
    }
    
    /// <summary>
    /// Non-recursive method
    /// </summary>
    public int NonRecursiveMethod(int n)
    {
        return n * 2;
    }
    
    /// <summary>
    /// Method with local functions for testing LocalFunctions property
    /// </summary>
    public int MethodWithLocalFunctions(int x, int y)
    {
        int LocalAdd(int a, int b)
        {
            return a + b;
        }
        
        async Task<string> LocalAsyncFunction()
        {
            await Task.Delay(10);
            return "result";
        }
        
        static int LocalStaticFunction(int value)
        {
            return value * 2;
        }
        
        return LocalAdd(x, y) + LocalStaticFunction(5);
    }
}

/// <summary>
/// Base class for override testing
/// </summary>
public class BaseClassForOverride
{
    /// <summary>
    /// Virtual method to be overridden
    /// </summary>
    public virtual void VirtualMethod()
    {
        Console.WriteLine("Base implementation");
    }
    
    /// <summary>
    /// Another virtual method
    /// </summary>
    public virtual string GetValue() => "Base";
}

/// <summary>
/// Derived class that overrides methods
/// </summary>
public class DerivedClassWithOverride : BaseClassForOverride
{
    /// <summary>
    /// Overridden virtual method
    /// </summary>
    public override void VirtualMethod()
    {
        Console.WriteLine("Derived implementation");
    }
    
    /// <summary>
    /// Overridden GetValue
    /// </summary>
    public override string GetValue() => "Derived";
}

/// <summary>
/// Interface for implementation testing
/// </summary>
public interface ITestInterface
{
    /// <summary>
    /// Interface method
    /// </summary>
    void InterfaceMethodToImplement();
    
    /// <summary>
    /// Another interface method
    /// </summary>
    string GetInterfaceValue();
}

/// <summary>
/// Class that implements ITestInterface
/// </summary>
public class InterfaceImplementorClass : ITestInterface
{
    /// <summary>
    /// Implementation of interface method
    /// </summary>
    public void InterfaceMethodToImplement()
    {
        Console.WriteLine("Implemented");
    }
    
    /// <summary>
    /// Implementation of GetInterfaceValue
    /// </summary>
    public string GetInterfaceValue() => "Interface Value";
}

/// <summary>
/// Class for testing async/Task return types
/// </summary>
public class AsyncTestClass
{
    /// <summary>
    /// Async method returning Task
    /// </summary>
    public async Task AsyncVoidMethod()
    {
        await Task.Delay(100);
    }
    
    /// <summary>
    /// Async method returning Task of int
    /// </summary>
    public async Task<int> AsyncIntMethod()
    {
        await Task.Delay(100);
        return 42;
    }
    
    /// <summary>
    /// Sync method returning Task (not async)
    /// </summary>
    public Task SyncTaskMethod()
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Regular sync method
    /// </summary>
    public int SyncMethod()
    {
        return 42;
    }
    
    /// <summary>
    /// Method with nullable return type
    /// </summary>
    public string? NullableReturnMethod()
    {
        return null;
    }
    
    /// <summary>
    /// Method with non-nullable return type
    /// </summary>
    public string NonNullableReturnMethod()
    {
        return "value";
    }
}

/// <summary>
/// Public API class for testing IsPublicApi
/// </summary>
public class PublicApiClass
{
    /// <summary>
    /// Public method - part of public API
    /// </summary>
    public void PublicMethod() { }
    
    /// <summary>
    /// Private method - not part of public API
    /// </summary>
    private void PrivateMethod() { }
    
    /// <summary>
    /// Internal method - not part of public API
    /// </summary>
    internal void InternalMethod() { }
    
    /// <summary>
    /// Protected method - part of public API
    /// </summary>
    protected void ProtectedMethod() { }
}

/// <summary>
/// Internal class for testing IsPublicApi
/// </summary>
internal class InternalApiClass
{
    /// <summary>
    /// Public method in internal class - not part of public API
    /// </summary>
    public void PublicMethodInInternalClass() { }
}
