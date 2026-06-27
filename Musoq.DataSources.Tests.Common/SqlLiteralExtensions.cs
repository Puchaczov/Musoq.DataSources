namespace Musoq.DataSources.Tests.Common;

public static class SqlLiteralExtensions
{
    public static string Escape(this string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "''");
    }
}
