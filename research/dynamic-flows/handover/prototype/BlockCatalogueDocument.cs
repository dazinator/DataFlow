namespace DataFlow.DynamicFlows.Prototype;

using System.Text.Json.Serialization;

// ---------------------------------------------------------------------------
// BlockCatalogueDocument
// A serializable snapshot of all block types available in the registry.
// Generated once at startup; static for the lifetime of the application.
// Can be served as a static JSON file to a separately-hosted designer UI,
// or embedded in a Blazor WASM publish output for fully offline use.
// ---------------------------------------------------------------------------

/// <summary>
/// A serializable snapshot of all block types available in the registry.
/// Generated once at startup; static for the lifetime of the application.
/// Can be serialized to JSON and served statically or cached.
/// </summary>
public sealed record BlockCatalogueDocument
{
    /// <summary>When this catalogue snapshot was generated (UTC).</summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Version/identifier of the backend application that generated this catalogue.
    /// Useful when comparing catalogues across deployments.
    /// </summary>
    [JsonPropertyName("backendVersion")]
    public string? BackendVersion { get; init; }

    /// <summary>All registered block types, ordered by key.</summary>
    [JsonPropertyName("blocks")]
    public IReadOnlyList<BlockTypeEntry> Blocks { get; init; } = [];
}

/// <summary>Metadata for a single registered block type.</summary>
public sealed record BlockTypeEntry
{
    /// <summary>Unique registration key (e.g. "invoices:validator").</summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable display name for the designer palette.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Optional description shown in the palette tooltip or detail panel.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Optional category for palette grouping (e.g. "Sources", "Transforms").</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>
    /// The fully-qualified CLR type name of the data this block accepts as input.
    /// Null for source blocks (no input).
    /// </summary>
    [JsonPropertyName("inputTypeName")]
    public string? InputTypeName { get; init; }

    /// <summary>
    /// The fully-qualified CLR type name of the data this block produces as output.
    /// Null for terminal (sink) blocks.
    /// </summary>
    [JsonPropertyName("outputTypeName")]
    public string? OutputTypeName { get; init; }

    /// <summary>
    /// Short type labels for display in the designer canvas (e.g. "int", "Invoice[]").
    /// Derived from InputTypeName/OutputTypeName for readability.
    /// </summary>
    [JsonPropertyName("inputTypeLabel")]
    public string? InputTypeLabel { get; init; }

    [JsonPropertyName("outputTypeLabel")]
    public string? OutputTypeLabel { get; init; }

    /// <summary>
    /// Whether this block implements <c>IConfigurableBlock</c> and can accept
    /// runtime configuration from the designer (Phase 3).
    /// </summary>
    [JsonPropertyName("isConfigurable")]
    public bool IsConfigurable { get; init; }

    /// <summary>
    /// JSON Schema string describing the block's configuration options type.
    /// Null if the block is not configurable, or if the catalogue was exported
    /// without <c>includeConfigSchemas: true</c>.
    /// </summary>
    [JsonPropertyName("configSchema")]
    public string? ConfigSchema { get; init; }
}

// ---------------------------------------------------------------------------
// IBlockCatalogueExporter
// Generates a BlockCatalogueDocument from the current IBlockTypeRegistry state.
// ---------------------------------------------------------------------------

/// <summary>
/// Generates a <see cref="BlockCatalogueDocument"/> from the current
/// <c>IBlockTypeRegistry</c> state.
/// </summary>
public interface IBlockCatalogueExporter
{
    /// <summary>
    /// Export the current registry as a catalogue document.
    /// The result is safe to cache for the lifetime of the application since
    /// block types are registered at startup and do not change at runtime.
    /// </summary>
    /// <param name="includeConfigSchemas">
    /// When true, each configurable block's JSON Schema is included in the
    /// catalogue. Omit (default: false) to keep the catalogue lightweight for
    /// palette-only use cases.
    /// </param>
    BlockCatalogueDocument Export(bool includeConfigSchemas = false);
}

// ---------------------------------------------------------------------------
// IBlockCatalogueStore
// Caches and distributes the generated catalogue.
// ---------------------------------------------------------------------------

/// <summary>
/// Caches and distributes the generated <see cref="BlockCatalogueDocument"/>.
/// The in-memory implementation generates it once on first access and caches it.
/// A file-based implementation can write it to disk for serving as a static asset
/// alongside a separately-hosted Blazor WASM designer.
/// </summary>
public interface IBlockCatalogueStore
{
    /// <summary>
    /// Get the current catalogue.
    /// Implementations should cache aggressively — block types are static after startup.
    /// </summary>
    BlockCatalogueDocument GetCatalogue();

    /// <summary>
    /// Explicitly invalidate the cached catalogue (e.g. after a test reset).
    /// Most production implementations can ignore this.
    /// </summary>
    void Invalidate() { }
}

/// <summary>
/// In-memory implementation of <see cref="IBlockCatalogueStore"/>.
/// Generates the catalogue once on first access and caches it for the
/// lifetime of the application.
/// </summary>
public sealed class InMemoryBlockCatalogueStore : IBlockCatalogueStore
{
    private readonly IBlockCatalogueExporter _exporter;
    private readonly bool _includeConfigSchemas;
    private BlockCatalogueDocument? _cached;
    private readonly object _lock = new();

    public InMemoryBlockCatalogueStore(
        IBlockCatalogueExporter exporter,
        bool includeConfigSchemas = false)
    {
        _exporter = exporter;
        _includeConfigSchemas = includeConfigSchemas;
    }

    public BlockCatalogueDocument GetCatalogue()
    {
        if (_cached is not null) return _cached;
        lock (_lock)
        {
            _cached ??= _exporter.Export(_includeConfigSchemas);
        }
        return _cached;
    }

    public void Invalidate()
    {
        lock (_lock) { _cached = null; }
    }
}

// ---------------------------------------------------------------------------
// Example: what a generated catalogue JSON looks like
// (Equivalent to GET /flows/blocks response)
// ---------------------------------------------------------------------------

// {
//   "generatedAt": "2026-03-30T12:00:00Z",
//   "backendVersion": "1.4.2",
//   "blocks": [
//     {
//       "key": "global:producer",
//       "displayName": "Integer Producer",
//       "description": "Produces a sequence of integers.",
//       "category": "Sources",
//       "inputTypeName": null,
//       "outputTypeName": "System.Int32",
//       "inputTypeLabel": null,
//       "outputTypeLabel": "int",
//       "isConfigurable": true,
//       "configSchema": null
//     },
//     {
//       "key": "global:transform",
//       "displayName": "Integer Transform",
//       "description": "Doubles each integer.",
//       "category": "Transforms",
//       "inputTypeName": "System.Int32",
//       "outputTypeName": "System.Int32",
//       "inputTypeLabel": "int",
//       "outputTypeLabel": "int",
//       "isConfigurable": false,
//       "configSchema": null
//     },
//     {
//       "key": "global:batch",
//       "displayName": "Integer Batcher",
//       "description": "Batches integers into arrays.",
//       "category": "Transforms",
//       "inputTypeName": "System.Int32",
//       "outputTypeName": "System.Int32[]",
//       "inputTypeLabel": "int",
//       "outputTypeLabel": "int[]",
//       "isConfigurable": true,
//       "configSchema": null
//     },
//     {
//       "key": "global:processor",
//       "displayName": "Integer Processor",
//       "description": "Processes batches of integers.",
//       "category": "Sinks",
//       "inputTypeName": "System.Int32[]",
//       "outputTypeName": null,
//       "inputTypeLabel": "int[]",
//       "outputTypeLabel": null,
//       "isConfigurable": true,
//       "configSchema": null
//     }
//   ]
// }
