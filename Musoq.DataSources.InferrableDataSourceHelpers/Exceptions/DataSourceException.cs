namespace Musoq.DataSources.InferrableDataSourceHelpers.Exceptions;

public class DataSourceException : Exception
{
    public DataSourceException(Exception? innerException)
        : base("Error while reading data from data source.", innerException)
    {
    }
}