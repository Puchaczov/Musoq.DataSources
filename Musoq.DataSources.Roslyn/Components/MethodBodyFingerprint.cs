using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Components;

internal static class MethodBodyFingerprint
{
    private const int FormatVersion = 1;

    public static string? Create(MethodDeclarationSyntax methodDeclaration)
    {
        var body = (SyntaxNode?)methodDeclaration.Body ?? methodDeclaration.ExpressionBody;
        if (body == null || methodDeclaration.ContainsDiagnostics || body.ContainsDiagnostics)
            return null;

        if (body.DescendantTrivia(descendIntoTrivia: true)
                .Any(trivia => trivia.GetStructure() is DirectiveTriviaSyntax))
            return null;

        var tokens = body.DescendantTokens(descendIntoTrivia: false).ToArray();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(FormatVersion);
            writer.Write(tokens.Length);

            foreach (var token in tokens)
            {
                writer.Write(token.RawKind);

                var tokenText = Encoding.UTF8.GetBytes(token.Text);
                writer.Write(tokenText.Length);
                writer.Write(tokenText);
            }
        }

        return $"v{FormatVersion}:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
    }
}
