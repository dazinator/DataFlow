namespace DataFlow.DynamicFlows.Prototype;

using System.Text.Json;
using System.Text.Json.Serialization;

// ---------------------------------------------------------------------------
// Flow Definition Model
// JSON-serializable schema for a dynamic flow topology
// ---------------------------------------------------------------------------

/// <summary>
/// A versioned, serializable definition of a DataFlow pipeline.
/// This is the "source of truth" that the designer saves and the executor reads.
/// </summary>
public sealed class FlowDefinition
{
    /// <summary>Stable identifier for this flow (survives version bumps).</summary>
    [JsonPropertyName("flowId")]
    public string FlowId { get; init; } = string.Empty;

    /// <summary>Monotonically increasing version. Increment when the definition changes.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>Human-readable name shown in the UI.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The blocks that participate in this flow.</summary>
    [JsonPropertyName("blocks")]
    public IReadOnlyList<FlowBlockReference> Blocks { get; init; } = Array.Empty<FlowBlockReference>();

    /// <summary>Directed edges connecting blocks.</summary>
    [JsonPropertyName("connections")]
    public IReadOnlyList<FlowConnection> Connections { get; init; } = Array.Empty<FlowConnection>();

    /// <summary>
    /// Per-block configuration snapshots, keyed by <see cref="FlowBlockReference.InstanceId"/>.
    /// Stored as raw JSON so each block can deserialize to its own options type.
    /// Snapshotted at the time of version creation — later config edits do NOT affect this version.
    /// </summary>
    [JsonPropertyName("blockConfig")]
    public IReadOnlyDictionary<string, JsonElement>? BlockConfig { get; init; }
}

/// <summary>
/// A reference to a registered block within a flow definition.
/// </summary>
public sealed class FlowBlockReference
{
    /// <summary>
    /// Unique identifier for this block *within this flow definition*.
    /// Used as the source/target in <see cref="FlowConnection"/>.
    /// For simple cases this can equal <see cref="BlockKey"/>.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// The registered key of the block type (e.g. "global:producer").
    /// Must match a key in <see cref="DataFlow.POC.Registry.IBlockTypeRegistry"/>.
    /// </summary>
    [JsonPropertyName("blockKey")]
    public string BlockKey { get; init; } = string.Empty;
}

/// <summary>
/// A directed edge between two blocks in a flow definition.
/// </summary>
public sealed class FlowConnection
{
    /// <summary>Source block's <see cref="FlowBlockReference.InstanceId"/>.</summary>
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    /// <summary>Target block's <see cref="FlowBlockReference.InstanceId"/>.</summary>
    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    /// <summary>
    /// Bounded channel capacity between source and target.
    /// Defaults to 100, mirroring the DataFlowGraphBuilder default.
    /// </summary>
    [JsonPropertyName("bufferCapacity")]
    public int BufferCapacity { get; init; } = 100;
}
