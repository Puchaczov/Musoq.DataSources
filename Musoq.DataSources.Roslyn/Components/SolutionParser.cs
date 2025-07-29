using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components;

/// <summary>
/// Custom solution parser that uses Microsoft.Build APIs directly to parse .sln and .csproj files
/// and creates AdHocWorkspace instances without using MSBuildWorkspace.
/// </summary>
internal class SolutionParser
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public SolutionParser(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public async Task<Solution> ParseSolutionAsync(AdhocWorkspace workspace, string solutionFilePath, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Parsing solution file using custom parser: {solutionFilePath}", solutionFilePath);

        // Parse the solution file
        var solutionFile = SolutionFile.Parse(solutionFilePath);
        
        // Create solution info
        var solutionInfo = SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            solutionFilePath,
            projects: Enumerable.Empty<ProjectInfo>()
        );

        // Add the solution to the workspace
        var solution = workspace.AddSolution(solutionInfo);

        // Process each project in the solution
        foreach (var projectInSolution in solutionFile.ProjectsInOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip solution folders and other non-buildable projects
            if (projectInSolution.ProjectType == SolutionProjectType.SolutionFolder)
                continue;

            var projectFilePath = Path.Combine(Path.GetDirectoryName(solutionFilePath)!, projectInSolution.RelativePath);
            
            if (!File.Exists(projectFilePath))
            {
                _logger.LogWarning("Project file not found: {projectFilePath}", projectFilePath);
                continue;
            }

            try
            {
                var projectInfo = await ParseProjectAsync(projectInSolution, projectFilePath, cancellationToken);
                if (projectInfo != null)
                {
                    // Add the project to the workspace
                    workspace.AddProject(projectInfo);
                    _logger.LogTrace("Added project: {projectName}", projectInfo.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse project: {projectFilePath}", projectFilePath);
            }
        }

        _logger.LogTrace("Solution parsing completed");
        return workspace.CurrentSolution;
    }

    private async Task<ProjectInfo?> ParseProjectAsync(ProjectInSolution projectInSolution, string projectFilePath, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Parsing project: {projectFilePath}", projectFilePath);

        // Load the project using Microsoft.Build
        var globalProperties = new Dictionary<string, string>
        {
            ["Configuration"] = "Debug",
            ["Platform"] = "AnyCPU"
        };

        var projectCollection = new ProjectCollection();
        var project = projectCollection.LoadProject(projectFilePath, globalProperties, null);

        // Extract basic project information
        var projectId = ProjectId.CreateNewId();
        var assemblyName = project.GetPropertyValue("AssemblyName") ?? Path.GetFileNameWithoutExtension(projectFilePath);
        var outputPath = project.GetPropertyValue("OutputPath");
        var outputFileName = project.GetPropertyValue("TargetFileName");
        var outputFilePath = !string.IsNullOrEmpty(outputPath) && !string.IsNullOrEmpty(outputFileName)
            ? Path.Combine(Path.GetDirectoryName(projectFilePath)!, outputPath, outputFileName)
            : null;

        // Determine language
        var language = Path.GetExtension(projectFilePath).ToLowerInvariant() switch
        {
            ".csproj" => LanguageNames.CSharp,
            ".vbproj" => LanguageNames.VisualBasic,
            _ => LanguageNames.CSharp
        };

        // Create compilation options
        var compilationOptions = language == LanguageNames.CSharp
            ? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            : null;

        // Create parse options
        var parseOptions = language == LanguageNames.CSharp
            ? new CSharpParseOptions(LanguageVersion.Latest)
            : null;

        // Get source files and create document infos
        var documents = new List<DocumentInfo>();
        var compileItems = project.GetItems("Compile");
        
        foreach (var item in compileItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(Path.GetDirectoryName(projectFilePath)!, item.EvaluatedInclude);
            
            if (!File.Exists(filePath))
                continue;

            try
            {
                var sourceText = SourceText.From(await File.ReadAllTextAsync(filePath, cancellationToken));
                var documentInfo = DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId),
                    Path.GetFileName(filePath),
                    folders: GetFolders(item.EvaluatedInclude),
                    sourceCodeKind: SourceCodeKind.Regular,
                    loader: TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())),
                    filePath: filePath
                );

                documents.Add(documentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create document info for source file: {filePath}", filePath);
            }
        }

        // Get metadata references (for now, we'll use a basic set)
        var metadataReferences = GetBasicMetadataReferences();

        // Create project info
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            projectInSolution.ProjectName,
            assemblyName,
            language,
            projectFilePath,
            outputFilePath,
            compilationOptions,
            parseOptions,
            documents,
            projectReferences: Enumerable.Empty<ProjectReference>(),
            metadataReferences: metadataReferences,
            analyzerReferences: Enumerable.Empty<AnalyzerReference>(),
            additionalDocuments: Enumerable.Empty<DocumentInfo>()
        );

        // Dispose the project collection
        projectCollection.Dispose();

        return projectInfo;
    }

    private static IEnumerable<string> GetFolders(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrEmpty(directory))
            return Enumerable.Empty<string>();

        return directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<MetadataReference> GetBasicMetadataReferences()
    {
        // Provide basic .NET references
        var references = new List<MetadataReference>();

        try
        {
            // Add basic .NET runtime references
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (runtimeDirectory != null)
            {
                var basicAssemblies = new[]
                {
                    "System.Runtime.dll",
                    "System.Collections.dll",
                    "System.Linq.dll",
                    "System.Core.dll",
                    "mscorlib.dll",
                    "netstandard.dll"
                };

                foreach (var assembly in basicAssemblies)
                {
                    var assemblyPath = Path.Combine(runtimeDirectory, assembly);
                    if (File.Exists(assemblyPath))
                    {
                        references.Add(MetadataReference.CreateFromFile(assemblyPath));
                    }
                }
            }
        }
        catch (Exception)
        {
            // If we can't load basic references, continue with empty list
        }

        return references;
    }
}