using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Git;

/// <summary>
/// Starts a read-only Git child process without invoking a shell. It is intentionally a small protocol primitive:
/// operation-specific readers own their arguments and parsers, while this type owns cancellation, stderr draining,
/// and actionable failures.
/// </summary>
internal sealed class GitCliProcess : IDisposable
{
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly Timer? _metricsSampler;
    private readonly Process _process;
    private readonly Task<string> _standardError;
    private bool _completed;
    private bool _disposed;

    private GitCliProcess(Process process, CancellationToken cancellationToken)
    {
        _process = process;
        _standardError = process.StandardError.ReadToEndAsync();
        _metricsSampler = GitCliProcessMetrics.StartSampling(process);
        _cancellationRegistration = cancellationToken.Register(static state => ((GitCliProcess)state!).Stop(), this);
    }

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public static GitCliProcess Start(
        string repositoryPath,
        GitHistoryBackendOptions options,
        IEnumerable<string> operationArguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(operationArguments);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(options.Executable)
        {
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // These options prevent presentation, external diff, attribute-driven filters, and locking. The history
        // readers only ask Git for object metadata; they never checkout, smudge, fetch, or mutate configuration.
        startInfo.ArgumentList.Add("--no-pager");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("color.ui=false");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("diff.external=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.process=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.smudge=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("filter.lfs.required=false");
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        foreach (var argument in operationArguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            var process = Process.Start(startInfo) ??
                          throw new InvalidOperationException($"Unable to start Git executable '{options.Executable}'.");
            return new GitCliProcess(process, cancellationToken);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Git executable '{options.Executable}' could not be started. Set '{GitHistoryBackendOptions.ExecutableSettingName}' " +
                "to an executable Git CLI path, or select the compatibility backend with GIT_HISTORY_BACKEND=libgit2.",
                exception);
        }
    }

    public void Complete()
    {
        if (_completed)
            return;

        _process.WaitForExit();
        GitCliProcessMetrics.Observe(_process);
        _metricsSampler?.Dispose();
        var standardError = _standardError.GetAwaiter().GetResult();
        _completed = true;

        if (_process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(standardError)
                ? "Git did not provide an error message."
                : standardError.Trim();
            throw new InvalidOperationException($"Git exited with code {_process.ExitCode}: {detail}");
        }
    }

    public void Stop()
    {
        try
        {
            if (!_process.HasExited)
            {
                // PeakWorkingSet64 is not always available after a killed child has been reaped, so sample
                // while it is still alive. This is telemetry only; it must never interfere with cancellation.
                GitCliProcessMetrics.Observe(_process);
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The child exited between HasExited and Kill.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationRegistration.Dispose();
        _metricsSampler?.Dispose();
        if (!_completed)
        {
            Stop();
            _process.WaitForExit();
            GitCliProcessMetrics.Observe(_process);
        }
        _process.Dispose();
    }
}

/// <summary>Process-local telemetry used by the isolated macro runner to report conservative combined memory.</summary>
internal static class GitCliProcessMetrics
{
    private static int _measurementDepth;
    private static long _peakWorkingSet;

    public static long PeakWorkingSet => Interlocked.Read(ref _peakWorkingSet);

    public static IDisposable BeginMeasurement()
    {
        if (Interlocked.Increment(ref _measurementDepth) == 1)
            Interlocked.Exchange(ref _peakWorkingSet, 0);
        return new MeasurementScope();
    }

    public static Timer? StartSampling(Process process)
    {
        if (Volatile.Read(ref _measurementDepth) == 0)
            return null;

        return new Timer(static state => Observe((Process)state!), process, TimeSpan.Zero, TimeSpan.FromMilliseconds(20));
    }

    public static void Observe(Process process)
    {
        long observed;
        try
        {
            observed = process.PeakWorkingSet64;
        }
        catch (InvalidOperationException)
        {
            // A killed process can be reaped by the OS before Windows exposes final peak information. The macro
            // runner still includes the parent peak; successful completions provide the child measurement.
            return;
        }
        long current;
        do
        {
            current = Interlocked.Read(ref _peakWorkingSet);
            if (current >= observed)
                return;
        } while (Interlocked.CompareExchange(ref _peakWorkingSet, observed, current) != current);
    }

    private sealed class MeasurementScope : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref _measurementDepth);
        }
    }
}
