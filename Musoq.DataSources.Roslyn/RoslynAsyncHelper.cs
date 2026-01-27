using System;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn;

internal static class RoslynAsyncHelper
{
    public static T RunSync<T>(Task<T> task)
    {
        try
        {
            return task.ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (MissingMethodException ex)
        {
            throw RoslynVersionHelper.CreateVersionMismatchException(ex, "RoslynAsyncHelper.RunSync");
        }
    }

    public static void RunSync(Task task)
    {
        try
        {
            task.ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (MissingMethodException ex)
        {
            throw RoslynVersionHelper.CreateVersionMismatchException(ex, "RoslynAsyncHelper.RunSync");
        }
    }
}

