using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class MethodEntityAnalysisTests
{
    [TestMethod]
    public async Task IsUsed_WhenReferenceAnalysisIsUnavailableWithSolution_ReturnsNull()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "MethodEntityAnalysis",
                "MethodEntityAnalysis",
                LanguageNames.CSharp,
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                ]))
            .AddDocument(
                DocumentInfo.Create(
                    documentId,
                    "Methods.cs",
                    loader: TextLoader.From(TextAndVersion.Create(
                        SourceText.From("public class Methods { public void Target() { } }"),
                        VersionStamp.Create()))));

        var document = solution.GetDocument(documentId);
        Assert.IsNotNull(document);

        var syntaxTree = await document!.GetSyntaxTreeAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        Assert.IsNotNull(syntaxTree);
        Assert.IsNotNull(semanticModel);

        var declaration = syntaxTree!.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        var symbol = semanticModel!.GetDeclaredSymbol(declaration) as Microsoft.CodeAnalysis.IMethodSymbol;
        Assert.IsNotNull(symbol);

        var entity = new MethodEntity(
            symbol!,
            declaration,
            semanticModel,
            solution,
            _ => Task.FromResult<IEnumerable<Microsoft.CodeAnalysis.FindSymbols.ReferencedSymbol>?>(null));

        Assert.IsNull(entity.ReferenceCount);
        Assert.IsNull(entity.IsUsed);
    }

    [TestMethod]
    public async Task InvocationAnalysis_WhenSemanticModelIsUnavailable_ReturnsNullCount()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "MethodEntityAnalysis",
                "MethodEntityAnalysis",
                LanguageNames.CSharp,
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                ]))
            .AddDocument(
                DocumentInfo.Create(
                    documentId,
                    "Methods.cs",
                    loader: TextLoader.From(TextAndVersion.Create(
                        SourceText.From("public class Methods { public void Target() { Missing(); } }"),
                        VersionStamp.Create()))));

        var document = solution.GetDocument(documentId);
        Assert.IsNotNull(document);

        var syntaxTree = await document!.GetSyntaxTreeAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        Assert.IsNotNull(syntaxTree);
        Assert.IsNotNull(semanticModel);

        var declaration = syntaxTree!.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        var symbol = semanticModel!.GetDeclaredSymbol(declaration) as Microsoft.CodeAnalysis.IMethodSymbol;
        Assert.IsNotNull(symbol);

        var entity = new MethodEntity(symbol!, declaration);

        Assert.IsNull(entity.UnresolvedInvocationCount);
        Assert.AreEqual(0, entity.CalleeCount);
        Assert.AreEqual(0, entity.Callees.Count());
        Assert.IsFalse(entity.IsRecursive);
    }
}
