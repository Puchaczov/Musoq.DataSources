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

    public Class1 Method3()
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

public class CyclomaticComplexityClass1
{
    public void CyclomaticComplexityMethod1()
    {
    }
    
    public void CyclomaticComplexityMethod2(bool value)
    {
        if (value)
        {
            return;
        }
    }
    
    public void CyclomaticComplexityMethod3(bool value)
    {
        if (value)
        {
            return;
        }
        else
        {
            return;
        }
    }
}

public interface Interface1
{
    public Task Method1Async();

    public void Method2();
    
    public Class1 Method3(int a);
    
    public Enum1 Method4();
}

public interface Interface2 : Interface1{}

public enum Enum1
{
    Value1,
    Value2
}