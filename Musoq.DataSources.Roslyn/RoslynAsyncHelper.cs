using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn;

internal static class RoslynAsyncHelper
{
    public static T RunSync<T>(Task<T> task)
    {
        return task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void RunSync(Task task)
    {
        task.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
