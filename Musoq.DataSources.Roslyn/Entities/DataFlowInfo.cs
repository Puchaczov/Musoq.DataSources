using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents data flow analysis results for a method or block of code.
/// </summary>
public class DataFlowInfo
{
    private readonly DataFlowAnalysis _analysis;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFlowInfo" /> class.
    /// </summary>
    /// <param name="analysis">The Roslyn data flow analysis result.</param>
    public DataFlowInfo(DataFlowAnalysis analysis)
    {
        _analysis = analysis;
    }

    /// <summary>
    ///     Gets the variables that are read inside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> ReadInside =>
        _analysis.ReadInside.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that are written inside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> WrittenInside =>
        _analysis.WrittenInside.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that are read outside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> ReadOutside =>
        _analysis.ReadOutside.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that are written outside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> WrittenOutside =>
        _analysis.WrittenOutside.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables declared inside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> VariablesDeclared =>
        _analysis.VariablesDeclared.Select(s => s.Name);

    /// <summary>
    ///     Gets the local variables or parameters captured by lambdas or local functions.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Captured =>
        _analysis.Captured.Select(s => s.Name);

    /// <summary>
    ///     Gets the local variables or parameters captured by lambdas inside the analyzed region
    ///     that are also read inside the region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> CapturedInside =>
        _analysis.CapturedInside.Select(s => s.Name);

    /// <summary>
    ///     Gets the local variables or parameters captured by lambdas outside the analyzed region
    ///     that are also read outside the region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> CapturedOutside =>
        _analysis.CapturedOutside.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that are used by unsafe address-of operations.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> UnsafeAddressTaken =>
        _analysis.UnsafeAddressTaken.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that flow in at the start of the analyzed region (definitely assigned).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> DataFlowsIn =>
        _analysis.DataFlowsIn.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that flow out at the end of the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> DataFlowsOut =>
        _analysis.DataFlowsOut.Select(s => s.Name);

    /// <summary>
    ///     Gets the variables that are always assigned inside the analyzed region.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> AlwaysAssigned =>
        _analysis.AlwaysAssigned.Select(s => s.Name);

    /// <summary>
    ///     Gets the count of captured variables.
    /// </summary>
    public int CapturedCount => _analysis.Captured.Length;

    /// <summary>
    ///     Gets the count of variables read inside the region.
    /// </summary>
    public int ReadInsideCount => _analysis.ReadInside.Length;

    /// <summary>
    ///     Gets the count of variables written inside the region.
    /// </summary>
    public int WrittenInsideCount => _analysis.WrittenInside.Length;

    /// <summary>
    ///     Returns a string representation.
    /// </summary>
    public override string ToString() =>
        $"DataFlow: {ReadInsideCount} reads, {WrittenInsideCount} writes, {CapturedCount} captured";
}
