namespace EpochAnchoringDemo.Models;

/// <summary>
/// Sample entity representing a data record to be processed.
/// </summary>
public sealed class DataRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Processed { get; set; }
}
