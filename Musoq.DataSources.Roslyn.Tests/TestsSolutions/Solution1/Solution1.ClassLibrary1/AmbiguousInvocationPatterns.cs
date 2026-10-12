namespace Solution1.ClassLibrary1;

public class AmbiguousInvocationTestClass
{
    private static void Target(int value, long other)
    {
    }

    private static void Target(long value, int other)
    {
    }

    public void CallsAmbiguousTarget()
    {
        Target(1, 1);
    }
}
