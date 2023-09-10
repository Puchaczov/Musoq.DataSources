namespace Musoq.DataSources.CodeGenerator;

public interface ICodeGenerator
{
    /// <summary>
    /// Generates class from given columns and input type.
    /// </summary>
    /// <param name="usedColumns">Used columns.</param>
    /// <param name="tool">Tool name.</param>
    /// <param name="input">Input.</param>
    /// <returns>Generated class.</returns>
    Task<string> GenerateClassAsync(string[] usedColumns, string tool, string input);
}