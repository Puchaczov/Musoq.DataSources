using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.DataSources.Example.Entities;

namespace Musoq.DataSources.Example;

/// <summary>
/// Provides custom functions for the Example plugin
/// </summary>
public class ExampleLibrary : LibraryBase
{
    /// <summary>
    /// Formats an entity with a custom prefix
    /// </summary>
    /// <param name="entity">The example entity</param>
    /// <param name="prefix">Prefix to add to the name</param>
    /// <returns>Formatted string</returns>
    [BindableMethod]
    public string FormatWithPrefix([InjectSpecificSource(typeof(ExampleEntity))] ExampleEntity entity, string prefix)
    {
        return $"{prefix}: {entity.Name}";
    }

    /// <summary>
    /// Calculates the age in days since creation
    /// </summary>
    /// <param name="entity">The example entity</param>
    /// <returns>Days since creation</returns>
    [BindableMethod]
    public int DaysSinceCreation([InjectSpecificSource(typeof(ExampleEntity))] ExampleEntity entity)
    {
        return (DateTime.Now - entity.CreatedDate).Days;
    }

    /// <summary>
    /// Determines if the entity is considered "high value"
    /// </summary>
    /// <param name="entity">The example entity</param>
    /// <param name="threshold">Value threshold (default: 500)</param>
    /// <returns>True if value exceeds threshold</returns>
    [BindableMethod]
    public bool IsHighValue([InjectSpecificSource(typeof(ExampleEntity))] ExampleEntity entity, int threshold = 500)
    {
        return entity.Value > threshold;
    }

    /// <summary>
    /// Generates a status summary for the entity
    /// </summary>
    /// <param name="entity">The example entity</param>
    /// <returns>Status summary string</returns>
    [BindableMethod]
    public string GetStatusSummary([InjectSpecificSource(typeof(ExampleEntity))] ExampleEntity entity)
    {
        var status = entity.IsActive ? "Active" : "Inactive";
        var age = (DateTime.Now - entity.CreatedDate).Days;
        var valueLevel = entity.Value switch
        {
            < 100 => "Low",
            < 500 => "Medium",
            _ => "High"
        };
        
        return $"{status} | {age}d old | {valueLevel} value";
    }

    /// <summary>
    /// Calculates a simple hash of the entity name
    /// </summary>
    /// <param name="entity">The example entity</param>
    /// <returns>Hash code as string</returns>
    [BindableMethod]
    public string GetNameHash([InjectSpecificSource(typeof(ExampleEntity))] ExampleEntity entity)
    {
        return entity.Name.GetHashCode().ToString("X");
    }
}