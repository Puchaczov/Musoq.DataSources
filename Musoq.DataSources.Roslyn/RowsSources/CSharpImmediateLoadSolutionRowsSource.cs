using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.CoconaCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal class CSharpImmediateLoadSolutionRowsSource(
    string solutionFilePath,
    IHttpClient? httpClient,
    IFileSystem? fileSystem,
    string? nugetPropertiesResolveEndpoint, 
    INuGetPropertiesResolver nuGetPropertiesResolver,
    ILogger logger, 
    CancellationToken queryCancelledToken
)
    : CSharpSolutionRowsSourceBase(queryCancelledToken)
{
    private readonly CancellationToken _queryCancelledToken = queryCancelledToken;

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        logger.LogTrace("Loading solution file using AdHocWorkspace: {solutionFilePath}", solutionFilePath);
        
        // Step 1: Load the solution using MSBuildWorkspace first
        var msBuildWorkspace = MSBuildWorkspace.Create();
        var solutionLoadLogger = new SolutionLoadLogger(logger);
        var projectLoadProgressLogger = new ProjectLoadProgressLogger(logger);
        var originalSolution = await msBuildWorkspace.OpenSolutionAsync(solutionFilePath, solutionLoadLogger, projectLoadProgressLogger, cancellationToken);
        
        // Step 2: Create AdHocWorkspace and transfer the solution
        var adHocWorkspace = new AdhocWorkspace();
        var transferredSolution = await TransferSolutionToAdHocWorkspace(adHocWorkspace, originalSolution, cancellationToken);
        
        // Step 3: Create the solution entity using the transferred solution
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(
                solutionFilePath, 
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                    OSPlatform.Windows : 
                    OSPlatform.Linux,
                logger
            ), 
            nugetPropertiesResolveEndpoint,
            new NuGetRetrievalService(
                nuGetPropertiesResolver,
                fileSystem,
                httpClient),
            fileSystem,
            packageVersionConcurrencyManager,
            SolutionOperationsCommand.BannedPropertiesValues,
            SolutionOperationsCommand.ResolveValueStrategy,
            logger
        );
        var solutionEntity = new SolutionEntity(transferredSolution, nuGetPackageMetadataRetriever, _queryCancelledToken);
        
        logger.LogTrace("Initializing solution");
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });
        
        logger.LogTrace("Solution initialized with AdHocWorkspace.");
        
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(solutionEntity, SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
        
        // Dispose the original MSBuildWorkspace as we no longer need it
        msBuildWorkspace.Dispose();
    }

    private async Task<Solution> TransferSolutionToAdHocWorkspace(AdhocWorkspace adHocWorkspace, Solution originalSolution, CancellationToken cancellationToken)
    {
        logger.LogTrace("Transferring solution to AdHocWorkspace");
        
        // Create solution info
        var solutionInfo = SolutionInfo.Create(
            originalSolution.Id,
            originalSolution.Version,
            originalSolution.FilePath,
            projects: Enumerable.Empty<ProjectInfo>()
        );
        
        // Add the solution to the workspace
        var solution = adHocWorkspace.AddSolution(solutionInfo);
        
        // Transfer all projects
        foreach (var originalProject in originalSolution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            logger.LogTrace("Transferring project: {projectName}", originalProject.Name);
            
            // Create project info
            var projectInfo = ProjectInfo.Create(
                originalProject.Id,
                originalProject.Version,
                originalProject.Name,
                originalProject.AssemblyName,
                originalProject.Language,
                originalProject.FilePath,
                originalProject.OutputFilePath,
                originalProject.CompilationOptions,
                originalProject.ParseOptions,
                documents: Enumerable.Empty<DocumentInfo>(),
                projectReferences: originalProject.ProjectReferences,
                metadataReferences: originalProject.MetadataReferences,
                analyzerReferences: originalProject.AnalyzerReferences,
                additionalDocuments: Enumerable.Empty<DocumentInfo>(),
                isSubmission: originalProject.IsSubmission,
                hostObjectType: null
            );
            
            // Add the project to the workspace and get updated solution
            var projectAdded = adHocWorkspace.AddProject(projectInfo);
            
            // Transfer all documents for this project
            foreach (var originalDocument in originalProject.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var sourceText = await originalDocument.GetTextAsync(cancellationToken);
                
                var documentInfo = DocumentInfo.Create(
                    originalDocument.Id,
                    originalDocument.Name,
                    originalDocument.Folders,
                    originalDocument.SourceCodeKind,
                    loader: null,
                    filePath: originalDocument.FilePath,
                    isGenerated: false
                );
                
                var documentAdded = adHocWorkspace.AddDocument(documentInfo);
                // Now update the document with the source text
                var updatedSolution = adHocWorkspace.CurrentSolution.WithDocumentText(documentAdded.Id, sourceText);
                
                // Apply the updated solution to the workspace
                if (!adHocWorkspace.TryApplyChanges(updatedSolution))
                {
                    logger.LogWarning("Failed to apply document text changes for document: {documentName}", originalDocument.Name);
                }
            }
            
            // Transfer additional documents (skip as noted, not fully supported)
            foreach (var additionalDocument in originalProject.AdditionalDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Note: AdHocWorkspace doesn't support additional documents in the same way
                // We'll skip them for now or could add them as regular documents if needed
                logger.LogTrace("Skipping additional document: {documentName} (not fully supported in AdHocWorkspace)", additionalDocument.Name);
            }
        }
        
        logger.LogTrace("Solution transfer to AdHocWorkspace completed");
        return adHocWorkspace.CurrentSolution;
    }
}