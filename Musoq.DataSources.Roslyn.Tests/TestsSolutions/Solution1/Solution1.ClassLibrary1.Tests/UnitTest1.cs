using System.Diagnostics.CodeAnalysis;

namespace Solution1.ClassLibrary1.Tests;

[ExcludeFromCodeCoverage]
public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }
}