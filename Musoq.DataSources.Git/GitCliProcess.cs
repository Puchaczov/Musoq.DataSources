using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Git;

internal readonly record struct GitCliProcessRequest(
    string RepositoryPath,
    string Executable,
    string BackendSettingName,
    IReadOnlyList<string> OperationArguments,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);

internal static class GitCliProcessEnvironment
{
    public static IReadOnlyDictionary<string, string> Default { get; } = new Dictionary<string, string>
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_LFS_SKIP_SMUDGE"] = "1",
        ["GIT_PAGER"] = "cat",
        ["GIT_TERMINAL_PROMPT"] = "0"
    };
}

internal interface IGitCliProcess : IDisposable
{
    Stream StandardOutput { get; }

    void Complete();

    void Stop();
}

internal interface IGitCliProcessFactory
{
    IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken);
}

internal sealed class GitCliProcessFactory : IGitCliProcessFactory
{
    public static GitCliProcessFactory Default { get; } = new();

    public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken)
    {
        return GitCliProcess.Start(request, cancellationToken);
    }
}

/// <summary>
/// Starts a read-only Git child process without invoking a shell. It is intentionally a small protocol primitive:
/// operation-specific readers own their arguments and parsers, while this type owns cancellation, stderr draining,
/// and actionable failures.
/// </summary>
internal sealed class GitCliProcess : IGitCliProcess
{
    private const int MaximumCapturedErrorCharacters = 64 * 1024;
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly Timer? _metricsSampler;
    private readonly Process _process;
    private readonly Task<string> _standardError;
    private bool _completed;
    private bool _stopRecorded;
    private bool _disposed;

    private GitCliProcess(Process process, CancellationToken cancellationToken)
    {
        _process = process;
        _standardError = ReadStandardErrorAsync(process.StandardError);
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
        return StartCore(
            repositoryPath,
            options.Executable,
            GitHistoryBackendOptions.BackendSettingName,
            operationArguments,
            cancellationToken);
    }

    public static GitCliProcess Start(
        string repositoryPath,
        GitReferenceBackendOptions options,
        IEnumerable<string> operationArguments,
        CancellationToken cancellationToken)
    {
        return StartCore(
            repositoryPath,
            options.Executable,
            GitReferenceBackendOptions.BackendSettingName,
            operationArguments,
            cancellationToken);
    }

    internal static GitCliProcess Start(
        GitCliProcessRequest request,
        CancellationToken cancellationToken)
    {
        return StartCore(
            request.RepositoryPath,
            request.Executable,
            request.BackendSettingName,
            request.OperationArguments,
            cancellationToken,
            request.EnvironmentVariables);
    }

    private static GitCliProcess StartCore(
        string repositoryPath,
        string executable,
        string backendSettingName,
        IEnumerable<string> operationArguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(operationArguments);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(executable)
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
        foreach (var (name, value) in GitCliProcessEnvironment.Default)
            startInfo.Environment[name] = value;
        if (environmentVariables is not null)
        {
            foreach (var (name, value) in environmentVariables)
                startInfo.Environment[name] = value;
        }

        foreach (var argument in operationArguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            var process = Process.Start(startInfo) ??
                          throw new GitCliUnavailableException($"Unable to start Git executable '{executable}'.");
            GitCliProcessMetrics.RecordStarted();
            return new GitCliProcess(process, cancellationToken);
        }
        catch (Win32Exception exception)
        {
            throw new GitCliUnavailableException(
                $"Git executable '{executable}' could not be started. Set '{GitHistoryBackendOptions.ExecutableSettingName}' " +
                $"to an executable Git CLI path, or select the compatibility backend with {backendSettingName}=libgit2.",
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
        GitCliProcessMetrics.RecordCompleted();

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

            if (!_stopRecorded)
            {
                _stopRecorded = true;
                GitCliProcessMetrics.RecordStopped();
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

    private static async Task<string> ReadStandardErrorAsync(StreamReader reader)
    {
        var buffer = ArrayPool<char>.Shared.Rent(4096);
        var captured = new StringBuilder(Math.Min(MaximumCapturedErrorCharacters, buffer.Length));
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
                if (count == 0)
                    return captured.ToString();

                if (captured.Length < MaximumCapturedErrorCharacters)
                {
                    var remaining = MaximumCapturedErrorCharacters - captured.Length;
                    captured.Append(buffer, 0, Math.Min(count, remaining));
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}

internal sealed class GitCliUnavailableException : InvalidOperationException
{
    public GitCliUnavailableException(string message)
        : base(message)
    {
    }

    public GitCliUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Process-local telemetry used by the isolated macro runner to report conservative combined memory.</summary>
internal static class GitCliProcessMetrics
{
    private static int _measurementDepth;
    private static long _peakWorkingSet;
    private static long _startedProcesses;
    private static long _completedProcesses;
    private static long _stoppedProcesses;

    public static long PeakWorkingSet => Interlocked.Read(ref _peakWorkingSet);

    public static GitCliProcessMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _startedProcesses),
        Interlocked.Read(ref _completedProcesses),
        Interlocked.Read(ref _stoppedProcesses),
        PeakWorkingSet);

    public static IDisposable BeginMeasurement()
    {
        if (Interlocked.Increment(ref _measurementDepth) == 1)
        {
            Interlocked.Exchange(ref _peakWorkingSet, 0);
            Interlocked.Exchange(ref _startedProcesses, 0);
            Interlocked.Exchange(ref _completedProcesses, 0);
            Interlocked.Exchange(ref _stoppedProcesses, 0);
        }
        return new MeasurementScope();
    }

    internal static void RecordStarted() => Interlocked.Increment(ref _startedProcesses);

    internal static void RecordCompleted() => Interlocked.Increment(ref _completedProcesses);

    internal static void RecordStopped() => Interlocked.Increment(ref _stoppedProcesses);

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

internal readonly record struct GitCliProcessMetricsSnapshot(
    long StartedProcesses,
    long CompletedProcesses,
    long StoppedProcesses,
    long PeakWorkingSet);
