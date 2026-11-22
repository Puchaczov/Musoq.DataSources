#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Text;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Search.Components.Traversal;

internal sealed class SearchSharedTextPrefix(
    string path,
    SearchEncodingMode mode,
    long capturedLength,
    long completeLength,
    string? identity,
    string contentHash)
{
    public const string SettingName = "search.text_read_mode";
    public string Path { get; } = path;

    public static bool IsEnabled(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(SettingName, out var value))
            return false;
        return value.ToLowerInvariant() switch
        {
            "stable" => false,
            "shared-prefix" => true,
            _ => throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument(SettingName))
        };
    }

    public static SearchSharedTextPrefix Capture(
        string path,
        SearchEncodingMode mode,
        SearchFileBudget budget,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var file = OpenShared(path);
        var length = file.Length;
        budget.ReserveBytes(length);
        var identity = SearchFileIdentity.TryCapture(file.SafeFileHandle);
        using var prefix = new PrefixStream(file, length, leaveOpen: true);
        var end = SearchTextReader.CompleteLinePrefix(prefix, mode, token);
        prefix.Position = 0;
        var hash = SearchSourceVersion.ComputeContentHash(prefix, token);
        return new SearchSharedTextPrefix(path, mode, length, end, identity, hash);
    }

    public TextReader OpenReader(Action<DataSourceDiagnostic>? warningReporter = null) =>
        SearchTextReader.Open(new PrefixStream(OpenObservedFile(), completeLength), mode, warningReporter);

    public void EnsureCurrent(CancellationToken token)
    {
        using var prefix = new PrefixStream(OpenObservedFile(), capturedLength);
        if (!string.Equals(contentHash, SearchSourceVersion.ComputeContentHash(prefix, token), StringComparison.Ordinal))
            throw new SearchSourceChangedException(Path);
    }

    private FileStream OpenObservedFile()
    {
        FileStream file;
        try
        {
            file = OpenShared(Path);
        }
        catch (FileNotFoundException)
        {
            throw new SearchSourceChangedException(Path);
        }
        catch (DirectoryNotFoundException)
        {
            throw new SearchSourceChangedException(Path);
        }

        if (file.Length >= capturedLength
            && string.Equals(identity, SearchFileIdentity.TryCapture(file.SafeFileHandle), StringComparison.Ordinal))
            return file;

        file.Dispose();
        throw new SearchSourceChangedException(Path);
    }

    private static FileStream OpenShared(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException ex) when (SearchDiagnosticCatalog.IsSharingViolation(ex))
        {
            throw new SearchSourceAccessException(SearchDiagnosticCatalog.SharingViolation(path), ex);
        }
    }

    private sealed class PrefixStream(FileStream file, long length, bool leaveOpen = false) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => file.Position; set => file.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, Math.Max(0, length - Position));
            if (count == 0)
                return 0;
            var read = file.Read(buffer[..count]);
            if (read == 0)
                throw new SearchSourceChangedException(file.Name);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => file.Seek(
            origin == SeekOrigin.End ? length + offset : offset,
            origin == SeekOrigin.End ? SeekOrigin.Begin : origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
                file.Dispose();
            base.Dispose(disposing);
        }
    }
}
