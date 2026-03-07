using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents control flow analysis results for a method or block of code.
/// </summary>
public class ControlFlowInfo
{
    private readonly ControlFlowAnalysis _analysis;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ControlFlowInfo" /> class.
    /// </summary>
    /// <param name="analysis">The Roslyn control flow analysis result.</param>
    public ControlFlowInfo(ControlFlowAnalysis analysis)
    {
        _analysis = analysis;
    }

    /// <summary>
    ///     Gets a value indicating whether the start point of the analyzed region is reachable.
    /// </summary>
    public bool StartPointIsReachable => _analysis.StartPointIsReachable;

    /// <summary>
    ///     Gets a value indicating whether the end point of the analyzed region is reachable.
    ///     If false, there is unreachable code after the last statement.
    /// </summary>
    public bool EndPointIsReachable => _analysis.EndPointIsReachable;

    /// <summary>
    ///     Gets the number of return statements in the analyzed region.
    /// </summary>
    public int ReturnStatementCount => _analysis.ReturnStatements.Length;

    /// <summary>
    ///     Gets the number of exit points (return, throw, break, continue, goto) from the analyzed region.
    /// </summary>
    public int ExitPointCount => _analysis.ExitPoints.Length;

    /// <summary>
    ///     Gets the number of entry points into the analyzed region.
    /// </summary>
    public int EntryPointCount => _analysis.EntryPoints.Length;

    /// <summary>
    ///     Returns a string representation.
    /// </summary>
    public override string ToString() =>
        $"ControlFlow: start={StartPointIsReachable}, end={EndPointIsReachable}, returns={ReturnStatementCount}, exits={ExitPointCount}";
}
