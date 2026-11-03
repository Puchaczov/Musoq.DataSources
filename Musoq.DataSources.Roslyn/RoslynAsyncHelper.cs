using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn;

internal static class RoslynAsyncHelper
{
    /// <summary>
    ///     Default timeout for reference finding operations (30 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultReferenceTimeout = TimeSpan.FromSeconds(30);

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

    /// <summary>
    ///     Runs an async task synchronously with a timeout. Returns the default value if the operation times out.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="taskFactory">A factory function that creates the task with a cancellation token.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="defaultValue">The default value to return on timeout.</param>
    /// <returns>The task result, or the default value if timed out.</returns>
    public static T RunSyncWithTimeout<T>(Func<CancellationToken, Task<T>> taskFactory, TimeSpan timeout,
        T defaultValue, RoslynReporting? reporting = null)
    {
        reporting?.CancellationToken.ThrowIfCancellationRequested();
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = reporting?.CancellationToken.CanBeCanceled == true
            ? CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, reporting.CancellationToken)
            : null;
        try
        {
            var task = taskFactory(linkedSource?.Token ?? timeoutSource.Token);
            return task.ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (UseTimeoutFallback(timeoutSource.Token, reporting))
        {
            reporting?.AnalysisTimeout();
            return defaultValue;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerException is OperationCanceledException &&
                                           UseTimeoutFallback(timeoutSource.Token, reporting))
        {
            reporting?.AnalysisTimeout();
            return defaultValue;
        }
        catch (MissingMethodException ex)
        {
            throw RoslynVersionHelper.CreateVersionMismatchException(ex, "RoslynAsyncHelper.RunSyncWithTimeout");
        }
    }

    private static bool UseTimeoutFallback(CancellationToken timeout, RoslynReporting? reporting)
        => reporting?.CancellationToken.IsCancellationRequested != true && (reporting is null || timeout.IsCancellationRequested);
}
