using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Files;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Os;

public partial class OsLibrary
{
    /// <summary>
    ///     Gets the aggregated average value from the given group name
    /// </summary>
    /// <param name="group" injectedByRuntime="true">The group object</param>
    /// <param name="name">The name of the group</param>
    /// <returns>Aggregated value</returns>
    [AggregationGetMethod]
    public IReadOnlyList<FileEntity>? AggregateFiles([InjectGroup] Group group, string name)
    {
        return group.GetValue<IReadOnlyList<FileEntity>>(name);
    }

    /// <summary>
    ///     Sets the value to average aggregation from the given group name
    /// </summary>
    /// <param name="group" injectedByRuntime="true">The group object</param>
    /// <param name="name">The name of the group</param>
    /// <param name="file">The value to set</param>
    /// <returns>Aggregated value</returns>
    [AggregationSetMethod]
    public void SetAggregateFiles([InjectGroup] Group group, string name, FileEntity file)
    {
        var list = group.GetOrCreateValue(name, new List<FileEntity>());

        list.Add(file);
    }

    /// <summary>
    ///     Sets the value to average aggregation from the given group name
    /// </summary>
    /// <param name="group" injectedByRuntime="true">The group object</param>
    /// <param name="name">The name of the group</param>
    /// <param name="file">The value to set</param>
    /// <returns>Aggregated value</returns>
    [AggregationSetMethod]
    public void SetAggregateFiles([InjectGroup] Group group, [InjectSpecificSource(typeof(FileEntity))] FileEntity file,
        string name)
    {
        var list = group.GetOrCreateValue(name, new List<FileEntity>());

        if (list == null)
            throw new InvalidOperationException("List is null");

        list.Add(file);
    }

    /// <summary>
    ///     Gets the aggregated average value from the given group name
    /// </summary>
    /// <param name="group" injectedByRuntime="true">The group object</param>
    /// <param name="name">The name of the group</param>
    /// <returns>Aggregated value</returns>
    [AggregationGetMethod]
    public IReadOnlyList<DirectoryInfo>? AggregateDirectories([InjectGroup] Group group, string name)
    {
        return group.GetValue<IReadOnlyList<DirectoryInfo>>(name);
    }

    /// <summary>
    ///     Sets the value to average aggregation from the given group name
    /// </summary>
    /// <param name="group" injectedByRuntime="true">The group object</param>
    /// <param name="directory">The value to set</param>
    /// <param name="name">The name of the group</param>
    /// <returns>Aggregated value</returns>
    [AggregationSetMethod]
    public void SetAggregateDirectories([InjectGroup] Group group,
        [InjectSpecificSource(typeof(DirectoryInfo))] DirectoryInfo directory, string name)
    {
        var list = group.GetOrCreateValue(name, new List<DirectoryInfo>());

        if (list == null)
            throw new InvalidOperationException("List is null");

        list.Add(directory);
    }
}