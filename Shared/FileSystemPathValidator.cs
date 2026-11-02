#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Musoq.Schema;

namespace Musoq.DataSources.Common;

/// <summary>Validates explicitly declared filesystem arguments without changing their values.</summary>
internal sealed class FileSystemPathValidator(
    IReadOnlyDictionary<string, FileSystemPathValidator.Argument[]> methods,
    Func<string, ArgumentException, ArgumentException>? errorFactory = null)
{
    private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();

    internal readonly record struct Argument(int Index, string Name, int ArgumentCount = 0);

    internal void Validate(string method, object?[] arguments)
    {
        if (!methods.TryGetValue(method, out var paths))
            return;

        foreach (var path in paths)
        {
            if (path.Index < arguments.Length &&
                (path.ArgumentCount == 0 || path.ArgumentCount == arguments.Length) &&
                arguments[path.Index] is string value)
                ValidatePath(value, path.Name);
        }
    }

    internal void Validate<TArguments>(string method, TArguments arguments)
        where TArguments : struct, ISourceArguments
    {
        if (!methods.TryGetValue(method, out var paths))
            return;

        foreach (var path in paths)
        {
            if (path.Index >= arguments.Count ||
                (path.ArgumentCount != 0 && path.ArgumentCount != arguments.Count))
                continue;

            string? value;
            try
            {
                value = arguments.GetValue<string>(path.Index);
            }
            catch (InvalidOperationException)
            {
                // A native argument with another declared type is handled by the provider's existing binder.
                continue;
            }
            ValidatePath(value, path.Name);
        }
    }

    private void ValidatePath(string? value, string argumentName)
    {
        // Null and empty values retain the provider's existing validation behavior.
        if (value is null)
            return;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsControl(character) && Array.IndexOf(InvalidPathCharacters, character) < 0)
                continue;

            var message = string.Format(CultureInfo.InvariantCulture,
                    "Filesystem path contains invalid character U+{0:X4} at position {1} (zero-based).",
                    (int)character, index);
            var exception = new ArgumentException(message, argumentName);
            throw errorFactory?.Invoke(message, exception) ?? exception;
        }
    }
}
