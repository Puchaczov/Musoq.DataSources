namespace Solution1.ClassLibrary1;

public static class MethodBodyFingerprintPatterns
{
    public static string SameBodyFirst()
    {
        return "same";
    }

    public static string SameBodySecond()
    {
        // Trivia must not affect the body fingerprint.
        return "same";
    }

    public static string LiteralWithSpace()
    {
        return "a b";
    }

    public static string LiteralWithoutSpace()
    {
        return "ab";
    }

    public static int OperatorAdd(int left, int right)
    {
        return left + right;
    }

    public static int OperatorSubtract(int left, int right)
    {
        return left - right;
    }

    public static void EmptyBodyFirst()
    {
    }

    public static void EmptyBodySecond()
    {
        // Comment-only bodies have the same significant tokens.
    }
}
