namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public interface IRowsReader<out TRow> : IAsyncEnumerator<TRow>
{
}