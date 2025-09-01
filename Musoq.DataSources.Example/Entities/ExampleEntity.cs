namespace Musoq.DataSources.Example.Entities;

/// <summary>
/// Represents a simple data record for demonstration purposes
/// </summary>
public class ExampleEntity
{
    /// <summary>
    /// Unique identifier for the record
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the record
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Numeric value associated with the record
    /// </summary>
    public int Value { get; set; }
    
    /// <summary>
    /// Whether the record is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Category classification
    /// </summary>
    public string Category { get; set; } = "General";
    
    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }
}