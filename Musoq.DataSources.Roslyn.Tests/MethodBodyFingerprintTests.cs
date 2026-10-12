using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.DataSources.Roslyn.Components;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class MethodBodyFingerprintTests
{
    [TestMethod]
    public void Fingerprint_IgnoresTriviaAndHasVersionedSha256Format()
    {
        var first = ParseMethod("class C { string M() { return \"same\"; } }");
        var second = ParseMethod("class C { string M() { /* comment */ return \"same\"; } }");

        var firstFingerprint = MethodBodyFingerprint.Create(first);
        var secondFingerprint = MethodBodyFingerprint.Create(second);

        Assert.IsNotNull(firstFingerprint);
        Assert.AreEqual(firstFingerprint, secondFingerprint);
        Assert.IsTrue(firstFingerprint!.StartsWith("v1:", StringComparison.Ordinal));
        Assert.AreEqual(67, firstFingerprint.Length);
    }

    [TestMethod]
    public void Fingerprint_PreservesLiteralAndOperatorDifferences()
    {
        var literalWithSpace = ParseMethod("class C { string M() { return \"a b\"; } }");
        var literalWithoutSpace = ParseMethod("class C { string M() { return \"ab\"; } }");
        var addition = ParseMethod("class C { int M(int a, int b) { return a + b; } }");
        var subtraction = ParseMethod("class C { int M(int a, int b) { return a - b; } }");

        Assert.AreNotEqual(
            MethodBodyFingerprint.Create(literalWithSpace),
            MethodBodyFingerprint.Create(literalWithoutSpace));
        Assert.AreNotEqual(
            MethodBodyFingerprint.Create(addition),
            MethodBodyFingerprint.Create(subtraction));
    }

    [TestMethod]
    public void Fingerprint_ReturnsNullForMissingMalformedAndDirectiveBodies()
    {
        var missingBody = CSharpSyntaxTree.ParseText("interface C { void M(); }")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        var malformedBody = ParseMethod("class C { void M() { return } }");
        var directiveBody = ParseMethod("""
                                       class C
                                       {
                                           void M()
                                           {
                                       #if DEBUG
                                               return;
                                       #endif
                                           }
                                       }
                                       """);

        Assert.IsNull(MethodBodyFingerprint.Create(missingBody));
        Assert.IsNull(MethodBodyFingerprint.Create(malformedBody));
        Assert.IsNull(MethodBodyFingerprint.Create(directiveBody));
    }

    private static MethodDeclarationSyntax ParseMethod(string source)
    {
        return CSharpSyntaxTree.ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
    }
}
