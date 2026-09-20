using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Runtime;

internal static class RuntimeDiscoverySchema
{
    public static readonly ISchemaColumn[] CultureColumns =
    [
        new SchemaColumn(nameof(CultureEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.EnglishName), 1, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.DisplayName), 2, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.NativeName), 3, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.IsNeutralCulture), 4, typeof(bool)),
        new SchemaColumn(nameof(CultureEntity.ParentName), 5, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.LCID), 6, typeof(int)),
        new SchemaColumn(nameof(CultureEntity.CultureTypes), 7, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.DecimalSeparator), 8, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.NumberGroupSeparator), 9, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.ShortDatePattern), 10, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.LongDatePattern), 11, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.ShortTimePattern), 12, typeof(string)),
        new SchemaColumn(nameof(CultureEntity.LongTimePattern), 13, typeof(string))
    ];

    public static readonly ISchemaColumn[] CurrentCultureColumns =
    [
        new SchemaColumn(nameof(CurrentCultureEntity.CurrentCulture), 0, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.CurrentUICulture), 1, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.DecimalSeparator), 2, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.NumberGroupSeparator), 3, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.ShortDatePattern), 4, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.LongDatePattern), 5, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.ShortTimePattern), 6, typeof(string)),
        new SchemaColumn(nameof(CurrentCultureEntity.LongTimePattern), 7, typeof(string))
    ];

    public static readonly ISchemaColumn[] EncodingColumns =
    [
        new SchemaColumn(nameof(EncodingEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(EncodingEntity.WebName), 1, typeof(string)),
        new SchemaColumn(nameof(EncodingEntity.CodePage), 2, typeof(int)),
        new SchemaColumn(nameof(EncodingEntity.EncodingName), 3, typeof(string)),
        new SchemaColumn(nameof(EncodingEntity.BodyName), 4, typeof(string)),
        new SchemaColumn(nameof(EncodingEntity.HeaderName), 5, typeof(string)),
        new SchemaColumn(nameof(EncodingEntity.IsSingleByte), 6, typeof(bool))
    ];

    public static readonly ISchemaColumn[] TimeZoneColumns =
    [
        new SchemaColumn(nameof(TimeZoneEntity.Id), 0, typeof(string)),
        new SchemaColumn(nameof(TimeZoneEntity.DisplayName), 1, typeof(string)),
        new SchemaColumn(nameof(TimeZoneEntity.StandardName), 2, typeof(string)),
        new SchemaColumn(nameof(TimeZoneEntity.DaylightName), 3, typeof(string)),
        new SchemaColumn(nameof(TimeZoneEntity.BaseUtcOffset), 4, typeof(TimeSpan)),
        new SchemaColumn(nameof(TimeZoneEntity.SupportsDaylightSavingTime), 5, typeof(bool))
    ];

    public static readonly ISchemaColumn[] RuntimeColumns =
    [
        new SchemaColumn(nameof(RuntimeEntity.DotNetVersion), 0, typeof(string)),
        new SchemaColumn(nameof(RuntimeEntity.FrameworkDescription), 1, typeof(string)),
        new SchemaColumn(nameof(RuntimeEntity.OSDescription), 2, typeof(string)),
        new SchemaColumn(nameof(RuntimeEntity.OSArchitecture), 3, typeof(string)),
        new SchemaColumn(nameof(RuntimeEntity.ProcessArchitecture), 4, typeof(string)),
        new SchemaColumn(nameof(RuntimeEntity.Is64BitOperatingSystem), 5, typeof(bool)),
        new SchemaColumn(nameof(RuntimeEntity.Is64BitProcess), 6, typeof(bool)),
        new SchemaColumn(nameof(RuntimeEntity.ProcessorCount), 7, typeof(int)),
        new SchemaColumn(nameof(RuntimeEntity.CurrentDirectory), 8, typeof(string))
    ];

    public static readonly ISchemaColumn[] DriveColumns =
    [
        new SchemaColumn(nameof(DriveEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(DriveEntity.DriveType), 1, typeof(string)),
        new SchemaColumn(nameof(DriveEntity.DriveFormat), 2, typeof(string)),
        new SchemaColumn(nameof(DriveEntity.IsReady), 3, typeof(bool)),
        new SchemaColumn(nameof(DriveEntity.AvailableFreeSpace), 4, typeof(long?)),
        new SchemaColumn(nameof(DriveEntity.TotalFreeSpace), 5, typeof(long?)),
        new SchemaColumn(nameof(DriveEntity.TotalSize), 6, typeof(long?)),
        new SchemaColumn(nameof(DriveEntity.RootDirectory), 7, typeof(string))
    ];

    public static readonly ISchemaColumn[] SpecialFolderColumns =
    [
        new SchemaColumn(nameof(SpecialFolderEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(SpecialFolderEntity.Path), 1, typeof(string)),
        new SchemaColumn(nameof(SpecialFolderEntity.Exists), 2, typeof(bool))
    ];

    public static readonly ISchemaColumn[] FileAttributeColumns =
    [
        new SchemaColumn(nameof(FileAttributeEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(FileAttributeEntity.Value), 1, typeof(int))
    ];

    public static readonly ISchemaColumn[] EnvironmentVariableColumns =
    [
        new SchemaColumn(nameof(EnvironmentVariableEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(EnvironmentVariableEntity.Target), 1, typeof(string))
    ];

    public static readonly ISchemaColumn[] PathInfoColumns =
    [
        new SchemaColumn(nameof(PathInfoEntity.InputPath), 0, typeof(string)),
        new SchemaColumn(nameof(PathInfoEntity.FullPath), 1, typeof(string)),
        new SchemaColumn(nameof(PathInfoEntity.Exists), 2, typeof(bool)),
        new SchemaColumn(nameof(PathInfoEntity.IsFile), 3, typeof(bool)),
        new SchemaColumn(nameof(PathInfoEntity.IsDirectory), 4, typeof(bool)),
        new SchemaColumn(nameof(PathInfoEntity.Root), 5, typeof(string)),
        new SchemaColumn(nameof(PathInfoEntity.DirectoryName), 6, typeof(string)),
        new SchemaColumn(nameof(PathInfoEntity.FileName), 7, typeof(string)),
        new SchemaColumn(nameof(PathInfoEntity.Extension), 8, typeof(string))
    ];
}
