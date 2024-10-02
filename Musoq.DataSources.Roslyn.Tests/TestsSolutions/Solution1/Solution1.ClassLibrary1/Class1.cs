namespace Solution1.ClassLibrary1;

public class Class1 : Interface1
{
    public int Property1 => 1;
    
    public Task Method1Async()
    {
        throw new NotImplementedException();
    }

    public void Method2()
    {
        throw new NotImplementedException();
    }

    public Class1 Method3(int a)
    {
        throw new NotImplementedException();
    }

    public Enum1 Method4()
    {
        throw new NotImplementedException();
    }
}

public interface Interface1
{
    public Task Method1Async();

    public void Method2();
    
    public Class1 Method3();
    
    public Enum1 Method4();
}

public enum Enum1
{
    Value1,
    Value2
}