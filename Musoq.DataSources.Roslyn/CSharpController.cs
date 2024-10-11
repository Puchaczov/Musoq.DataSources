using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.MSBuild;
using Musoq.DataSources.Roslyn.Dtos;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn;

/// <summary>
/// Controller for loading Roslyn solutions.
/// </summary>
[ApiController]
[Route("[controller]")]
public class CSharpController : ControllerBase
{
    /// <summary>
    /// Loads a solution.
    /// </summary>
    /// <param name="cSharpSolutionRequestDto">Load solution request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("solution/load")]
    public async Task<IActionResult> Load([FromBody] CSharpSolutionRequestDto? cSharpSolutionRequestDto, CancellationToken cancellationToken)
    {
        if (cSharpSolutionRequestDto == null)
        {
            return BadRequest("Request is empty.");
        }

        var filePath = cSharpSolutionRequestDto.SolutionFilePath;
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest("Solution file path is empty.");
        }
        
        if (!System.IO.File.Exists(filePath))
        {
            return BadRequest("Solution file does not exist.");
        }
        
        var workspace = MSBuildWorkspace.Create();
        string? workspaceFailedMessage = null;
        
        workspace.WorkspaceFailed += (sender, args) =>
        {
            workspaceFailedMessage = args.Diagnostic.Message;
        };
        
        var solution = await workspace.OpenSolutionAsync(filePath, cancellationToken: cancellationToken);
        var solutionEntity = new SolutionEntity(solution);
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });
        
        if (!string.IsNullOrWhiteSpace(workspaceFailedMessage))
        {
            return BadRequest(workspaceFailedMessage);
        }
        
        CSharpSchema.Solutions.TryAdd(filePath, solutionEntity);
        
        return Ok();
    }
    
    /// <summary>
    /// Unloads a solution.
    /// </summary>
    /// <param name="loadSolutionRequestDto">Unload solution on request.</param>
    /// <returns>Ok if solution was unloaded.</returns>
    [HttpPost("solution/unload")]
    public IActionResult Unload([FromBody] CSharpSolutionRequestDto? loadSolutionRequestDto)
    {
        if (loadSolutionRequestDto == null)
        {
            return BadRequest("Request is empty.");
        }

        var filePath = loadSolutionRequestDto.SolutionFilePath;
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest("Solution file path is empty.");
        }
        
        CSharpSchema.Solutions.TryRemove(filePath, out _);
        
        return Ok();
    }
}