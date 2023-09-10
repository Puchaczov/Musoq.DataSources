namespace Musoq.DataSources.CompiledCode;

public interface ICompiledCode<in TInput>
{
    /// <summary>
    /// Header, one or more lines
    /// </summary>
    /// <param name="input">The data input. It can be null.</param>
    /// <returns>True if line is header, false otherwise.</returns>
    public bool IsHeaderLine(TInput input);
    
    /// <summary>
    /// Data, one or more lines
    /// </summary>
    /// <param name="input">The data input. It can be null.</param>
    /// <returns>True if line is data, false otherwise.</returns>
    public bool IsDataLine(TInput input);
    
    /// <summary>
    /// footer, one or more lines, might be summary
    /// </summary>
    /// <param name="input">The data input. It can be null.</param>
    /// <returns>True if line is footer, false otherwise.</returns>
    public bool IsFooterLine(TInput input);
    
    /// <summary>
    /// Parses data line into json object.
    /// </summary>
    /// <param name="input">The data input. It can be null.</param>
    /// <returns>Json object.</returns>
    public string TurnIntoJson(TInput input);
    
    /// <summary>
    /// Splits input into lines containing rows.
    /// </summary>
    /// <param name="input">The data input. It can be null.</param>
    /// <returns>Lines containing rows.</returns>
    public string[] SplitIntoLines(TInput input);
}