#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Os;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class OsPatternColumns
{
    internal static readonly PatternColumnBindings<Files.FileEntity> FileEntity = new(
        (nameof(Files.FileEntity.DirectoryName), static row => row.DirectoryName),
        (nameof(Files.FileEntity.DirectoryPath), static row => row.DirectoryPath),
        (nameof(Files.FileEntity.Name), static row => row.Name),
        (nameof(Files.FileEntity.FileName), static row => row.FileName),
        (nameof(Files.FileEntity.Extension), static row => row.Extension),
        (nameof(Files.FileEntity.FullPath), static row => row.FullPath)
    );

    internal static readonly PatternColumnBindings<Directories.DirectoryEntity> DirectoryEntity = new(
        (nameof(Directories.DirectoryEntity.FullName), static row => row.FullName),
        (nameof(Directories.DirectoryEntity.Extension), static row => row.Extension),
        (nameof(Directories.DirectoryEntity.Name), static row => row.Name),
        (nameof(Directories.DirectoryEntity.Root), static row => row.Root)
    );

    internal static readonly PatternColumnBindings<Zip.ZipEntryEntity> ZipEntryEntity = new(
        (nameof(Zip.ZipEntryEntity.Name), static row => row.Name),
        (nameof(Zip.ZipEntryEntity.FullName), static row => row.FullName)
    );

    internal static readonly PatternColumnBindings<Process.ProcessEntity> ProcessEntity = new(
        (nameof(Process.ProcessEntity.MachineName), static row => row.MachineName),
        (nameof(Process.ProcessEntity.ProcessName), static row => row.ProcessName)
    );

    internal static readonly PatternColumnBindings<Dlls.DllInfo> DllInfo = new(

    );

    internal static readonly PatternColumnBindings<Compare.Directories.CompareDirectoriesResult> CompareDirectoriesResult = new(
        (nameof(Compare.Directories.CompareDirectoriesResult.SourceFileRelative), static row => row.SourceFileRelative),
        (nameof(Compare.Directories.CompareDirectoriesResult.DestinationFileRelative), static row => row.DestinationFileRelative)
    );

    internal static readonly PatternColumnBindings<Runtime.CultureEntity> CultureEntity = new(
        (nameof(Runtime.CultureEntity.Name), static row => row.Name),
        (nameof(Runtime.CultureEntity.EnglishName), static row => row.EnglishName),
        (nameof(Runtime.CultureEntity.DisplayName), static row => row.DisplayName),
        (nameof(Runtime.CultureEntity.NativeName), static row => row.NativeName),
        (nameof(Runtime.CultureEntity.ParentName), static row => row.ParentName),
        (nameof(Runtime.CultureEntity.CultureTypes), static row => row.CultureTypes),
        (nameof(Runtime.CultureEntity.DecimalSeparator), static row => row.DecimalSeparator),
        (nameof(Runtime.CultureEntity.NumberGroupSeparator), static row => row.NumberGroupSeparator),
        (nameof(Runtime.CultureEntity.ShortDatePattern), static row => row.ShortDatePattern),
        (nameof(Runtime.CultureEntity.LongDatePattern), static row => row.LongDatePattern),
        (nameof(Runtime.CultureEntity.ShortTimePattern), static row => row.ShortTimePattern),
        (nameof(Runtime.CultureEntity.LongTimePattern), static row => row.LongTimePattern)
    );

    internal static readonly PatternColumnBindings<Runtime.CurrentCultureEntity> CurrentCultureEntity = new(
        (nameof(Runtime.CurrentCultureEntity.CurrentCulture), static row => row.CurrentCulture),
        (nameof(Runtime.CurrentCultureEntity.CurrentUICulture), static row => row.CurrentUICulture),
        (nameof(Runtime.CurrentCultureEntity.DecimalSeparator), static row => row.DecimalSeparator),
        (nameof(Runtime.CurrentCultureEntity.NumberGroupSeparator), static row => row.NumberGroupSeparator),
        (nameof(Runtime.CurrentCultureEntity.ShortDatePattern), static row => row.ShortDatePattern),
        (nameof(Runtime.CurrentCultureEntity.LongDatePattern), static row => row.LongDatePattern),
        (nameof(Runtime.CurrentCultureEntity.ShortTimePattern), static row => row.ShortTimePattern),
        (nameof(Runtime.CurrentCultureEntity.LongTimePattern), static row => row.LongTimePattern)
    );

    internal static readonly PatternColumnBindings<Runtime.EncodingEntity> EncodingEntity = new(
        (nameof(Runtime.EncodingEntity.Name), static row => row.Name),
        (nameof(Runtime.EncodingEntity.WebName), static row => row.WebName),
        (nameof(Runtime.EncodingEntity.EncodingName), static row => row.EncodingName),
        (nameof(Runtime.EncodingEntity.BodyName), static row => row.BodyName),
        (nameof(Runtime.EncodingEntity.HeaderName), static row => row.HeaderName)
    );

    internal static readonly PatternColumnBindings<Runtime.TimeZoneEntity> TimeZoneEntity = new(
        (nameof(Runtime.TimeZoneEntity.Id), static row => row.Id),
        (nameof(Runtime.TimeZoneEntity.DisplayName), static row => row.DisplayName),
        (nameof(Runtime.TimeZoneEntity.StandardName), static row => row.StandardName),
        (nameof(Runtime.TimeZoneEntity.DaylightName), static row => row.DaylightName)
    );

    internal static readonly PatternColumnBindings<Runtime.RuntimeEntity> RuntimeEntity = new(
        (nameof(Runtime.RuntimeEntity.DotNetVersion), static row => row.DotNetVersion),
        (nameof(Runtime.RuntimeEntity.FrameworkDescription), static row => row.FrameworkDescription),
        (nameof(Runtime.RuntimeEntity.OSDescription), static row => row.OSDescription),
        (nameof(Runtime.RuntimeEntity.OSArchitecture), static row => row.OSArchitecture),
        (nameof(Runtime.RuntimeEntity.ProcessArchitecture), static row => row.ProcessArchitecture),
        (nameof(Runtime.RuntimeEntity.CurrentDirectory), static row => row.CurrentDirectory)
    );

    internal static readonly PatternColumnBindings<Runtime.DriveEntity> DriveEntity = new(
        (nameof(Runtime.DriveEntity.Name), static row => row.Name),
        (nameof(Runtime.DriveEntity.DriveType), static row => row.DriveType),
        (nameof(Runtime.DriveEntity.DriveFormat), static row => row.DriveFormat),
        (nameof(Runtime.DriveEntity.RootDirectory), static row => row.RootDirectory)
    );

    internal static readonly PatternColumnBindings<Runtime.SpecialFolderEntity> SpecialFolderEntity = new(
        (nameof(Runtime.SpecialFolderEntity.Name), static row => row.Name),
        (nameof(Runtime.SpecialFolderEntity.Path), static row => row.Path)
    );

    internal static readonly PatternColumnBindings<Runtime.FileAttributeEntity> FileAttributeEntity = new(
        (nameof(Runtime.FileAttributeEntity.Name), static row => row.Name)
    );

    internal static readonly PatternColumnBindings<Runtime.EnvironmentVariableEntity> EnvironmentVariableEntity = new(
        (nameof(Runtime.EnvironmentVariableEntity.Name), static row => row.Name),
        (nameof(Runtime.EnvironmentVariableEntity.Target), static row => row.Target)
    );

    internal static readonly PatternColumnBindings<Runtime.PathInfoEntity> PathInfoEntity = new(
        (nameof(Runtime.PathInfoEntity.InputPath), static row => row.InputPath),
        (nameof(Runtime.PathInfoEntity.FullPath), static row => row.FullPath),
        (nameof(Runtime.PathInfoEntity.Root), static row => row.Root),
        (nameof(Runtime.PathInfoEntity.DirectoryName), static row => row.DirectoryName),
        (nameof(Runtime.PathInfoEntity.FileName), static row => row.FileName),
        (nameof(Runtime.PathInfoEntity.Extension), static row => row.Extension)
    );

    internal static readonly PatternColumnBindings<Metadata.MetadataEntity> MetadataEntity = new(
        (nameof(Metadata.MetadataEntity.FullName), static row => row.FullName),
        (nameof(Metadata.MetadataEntity.DirectoryName), static row => row.DirectoryName),
        (nameof(Metadata.MetadataEntity.TagName), static row => row.TagName),
        (nameof(Metadata.MetadataEntity.Description), static row => row.Description)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "file" => FileEntity,
        "files" => FileEntity,
        "directories" => DirectoryEntity,
        "zip" => ZipEntryEntity,
        "processes" => ProcessEntity,
        "dlls" => DllInfo,
        "dirscompare" => CompareDirectoriesResult,
        "cultures" => CultureEntity,
        "currentculture" => CurrentCultureEntity,
        "encodings" => EncodingEntity,
        "timezones" => TimeZoneEntity,
        "runtime" => RuntimeEntity,
        "drives" => DriveEntity,
        "specialfolders" => SpecialFolderEntity,
        "fileattributes" => FileAttributeEntity,
        "environmentvariables" => EnvironmentVariableEntity,
        "pathinfo" => PathInfoEntity,
        "metadata" => MetadataEntity,
        _ => null
    };
}
