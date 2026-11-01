using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal sealed partial class GitRepositoryContext
{
    internal byte[] Bytes(params string[] arguments)
    {
        using var process = Start(arguments);
        using var output = new MemoryStream();
        process.StandardOutput.CopyTo(output);
        process.Complete();
        return output.ToArray();
    }

    private bool IsBinary(string blob)
    {
        using var process = Start("cat-file", "blob", blob);
        var prefix = new byte[8000];
        var count = process.StandardOutput.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        if (count < prefix.Length) process.Complete();
        return prefix.AsSpan(0, count).Contains((byte)0);
    }

    public IEnumerable<BlameHunkEntity> Blame(string path, string revision)
    {
        var requested = OptionalText("rev-parse", "--verify", "--quiet", "--end-of-options", revision + "^{commit}")?.Trim()
            ?? throw new ArgumentException($"Invalid revision '{revision}': not found.", nameof(revision));
        var entry = Tokens("--literal-pathspecs", "ls-tree", "-z", requested, "--", path).FirstOrDefault()
            ?? throw new FileNotFoundException($"File '{path}' does not exist at revision '{revision}'.");
        var header = entry[..entry.IndexOf('\t')].Split(' ');
        if (header.Length != 3 || header[1] != "blob") yield break;
        var blob = header[2];
        if (IsBinary(blob)) yield break;
        var fileContent = new GitBlameFileContent(() => Text("cat-file", "blob", blob), Token, Diagnostics);
        using var process = Start("blame", "--line-porcelain", "--encoding=UTF-8", requested, "--", path);
        using var reader = new StreamReader(process.StandardOutput, Encoding.UTF8);
        var remaining = 0;
        while (reader.ReadLine() is { } line)
        {
            Token.ThrowIfCancellationRequested();
            var parts = line.Split(' ');
            if (parts.Length is < 3 or > 4) throw new InvalidDataException("Invalid Git blame header.");
            var original = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var final = int.Parse(parts[2], CultureInfo.InvariantCulture);
            var count = parts.Length == 4 ? int.Parse(parts[3], CultureInfo.InvariantCulture) : 1;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            bool hasContent = false;
            while (reader.ReadLine() is { } attribute)
            {
                Token.ThrowIfCancellationRequested();
                if (attribute.StartsWith('\t')) { hasContent = true; break; }
                var separator = attribute.IndexOf(' ');
                if (separator > 0) fields[attribute[..separator]] = attribute[(separator + 1)..];
            }
            if (!hasContent) throw new InvalidDataException("Git blame ended before the line content.");
            if (remaining > 0) { remaining--; continue; }
            remaining = count - 1;
            var originalPath = DecodeGitPath(fields["filename"]);
            yield return new BlameHunkEntity(this, path, requested, parts[0], final - 1, count,
                fields["author"], fields["author-mail"].Trim('<', '>'), SignatureDate(fields, "author"),
                fields["committer"], fields["committer-mail"].Trim('<', '>'), SignatureDate(fields, "committer"),
                fields["summary"], original == final ? null : original, originalPath == path ? null : originalPath, fileContent);
        }
        if (remaining != 0) throw new InvalidDataException("Git blame ended inside a hunk.");
        process.Complete();
    }

    private static DateTimeOffset SignatureDate(IReadOnlyDictionary<string, string> fields, string name)
    {
        var zone = fields[name + "-tz"];
        var offset = new TimeSpan(int.Parse(zone.AsSpan(1, 2), CultureInfo.InvariantCulture),
            int.Parse(zone.AsSpan(3, 2), CultureInfo.InvariantCulture), 0);
        if (zone[0] == '-') offset = -offset;
        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(fields[name + "-time"], CultureInfo.InvariantCulture)).ToOffset(offset);
    }

    internal static string DecodeGitPath(string value)
    {
        if (!value.StartsWith('"')) return value;
        if (!value.EndsWith('"')) throw new InvalidDataException("Unterminated Git quoted path.");
        using var bytes = new MemoryStream();
        var text = new StringBuilder();
        void FlushText() { bytes.Write(Encoding.UTF8.GetBytes(text.ToString())); text.Clear(); }
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (value[i] != '\\') { text.Append(value[i]); continue; }
            FlushText();
            var escaped = value[++i];
            if (escaped is >= '0' and <= '7')
            {
                if (i + 2 >= value.Length - 1) throw new InvalidDataException("Invalid Git octal path escape.");
                bytes.WriteByte(Convert.ToByte(value.Substring(i, 3), 8)); i += 2;
            }
            else bytes.WriteByte(escaped switch
                { 'a' => 7, 'b' => 8, 't' => 9, 'n' => 10, 'v' => 11, 'f' => 12, 'r' => 13, '\\' => 92, '"' => 34,
                    _ => throw new InvalidDataException("Unknown Git path escape.") });
        }
        FlushText();
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
