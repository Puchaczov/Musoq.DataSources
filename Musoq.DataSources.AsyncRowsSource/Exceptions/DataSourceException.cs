namespace Musoq.DataSources.AsyncRowsSource.Exceptions;

public class DataSourceException : Exception
{
    public DataSourceException(Exception? innerException)
        : base("Error while reading data from data source.", innerException)
    {
    }
}