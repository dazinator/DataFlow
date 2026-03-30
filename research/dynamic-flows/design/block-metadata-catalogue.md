# Design: Block Metadata Catalogue

**Version**: 1.0  
**Date**: 2026-03-30  
**Status**: Proposed

---

## Problem

The designer UI needs to know what blocks are available — their keys, display names, accepted
input/output types, and (in Phase 3) their configuration schemas. This information currently
lives inside `IBlockTypeRegistry`, which is a live runtime service.

If the designer is co-hosted with the execution backend (same process or same API), a simple
`GET /flows/blocks` endpoint solves this. However:

- **Static hosting**: A Blazor WASM designer can be deployed as static files with no server
  component — it cannot call a live registry.
- **Separate hosting**: An organisation may want to host the designer UI independently from
  the execution backend (different deployments, different teams, different release cadences).
- **Performance**: The registry is static after startup; regenerating its metadata on every
  HTTP request is wasteful. A cached catalogue is more efficient.
- **Reproducibility**: A flow definition was built against a specific set of block types. A
  snapshot of the catalogue at save time provides a stable record of what was available
  when the flow was authored.

---

## Core Idea: Separate Metadata from Runtime

Block *types* (registered at startup) are fundamentally different from block *instances*
(created at runtime to execute a flow). The metadata that the designer needs — keys, names,
type info, config schemas — is fully determined at startup and **never changes while the
application is running**.

This means the metadata can be:
1. Exported as a **static JSON document** at startup (or on first request)
2. Cached indefinitely (until the application restarts)
3. Served from a static file host, CDN, or embedded in the Blazor WASM app itself
4. Consumed by a designer that has no runtime connection to the execution backend

---

## Proposed Design

### `BlockCatalogueDocument` — the serializable snapshot

```csharp
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
    /// Null if the block is not configurable.
    /// Present only when the registry has been asked to export config schemas.
    /// </summary>
    [JsonPropertyName("configSchema")]
    public string? ConfigSchema { get; init; }
}
```

### `IBlockCatalogueExporter` — generation interface

```csharp
/// <summary>
/// Generates a <see cref="BlockCatalogueDocument"/> from the current
/// <see cref="IBlockTypeRegistry"/> state.
/// </summary>
public interface IBlockCatalogueExporter
{
    /// <summary>
    /// Export the current registry as a catalogue document.
    /// The result is safe to cache for the lifetime of the application.
    /// </summary>
    BlockCatalogueDocument Export(bool includeConfigSchemas = false);
}
```

### `IBlockCatalogueStore` — distribution and caching interface

```csharp
/// <summary>
/// Caches and distributes the generated <see cref="BlockCatalogueDocument"/>.
/// The in-memory implementation generates it once on first access and caches it.
/// A file-based implementation can write it to disk for serving as a static asset.
/// </summary>
public interface IBlockCatalogueStore
{
    /// <summary>
    /// Get the current catalogue. Implementations should cache aggressively —
    /// the registry is static after startup.
    /// </summary>
    BlockCatalogueDocument GetCatalogue();

    /// <summary>
    /// Explicitly refresh the catalogue (e.g. after a test reset).
    /// Most implementations ignore this; production code never needs to call it.
    /// </summary>
    void Invalidate() { }
}
```

---

## Deployment Scenarios

### Scenario A: Co-hosted API (simplest)

Designer and execution backend run in the same ASP.NET Core process.

```
GET /flows/blocks
← { "generatedAt": "...", "blocks": [...] }
```

- `IBlockCatalogueExporter` generates the document on first request
- `IBlockCatalogueStore` (in-memory) caches it
- Subsequent requests served from cache — no registry traversal per request

### Scenario B: Separately hosted designer + API backend

Designer is a Blazor WASM app hosted on a static file server or CDN.
Execution backend is a separate API service.

```
API service startup:
  → IBlockCatalogueExporter.Export()
  → Write to wwwroot/block-catalogue.json (or S3/CDN)

Designer WASM startup:
  → Fetch /block-catalogue.json (static, no auth, no live connection to backend needed)
  → Deserialize to BlockCatalogueDocument
  → Populate palette
```

- The catalogue JSON is a build-time or startup-time artifact
- Designer and backend can be deployed and scaled independently
- Catalogue is refreshed when the backend redeploys (catalogue version in filename or ETag)

### Scenario C: Embedded catalogue (fully offline designer)

For scenarios where the designer must work without any network access to the backend:

```
dotnet run --export-block-catalogue > wwwroot/block-catalogue.json
```

The static JSON is bundled into the Blazor WASM publish output. The designer
never needs a live backend connection to display the block palette.

### Scenario D: Multi-tenant / multi-backend

Different execution backends (e.g. different customer tenants) register different blocks.
Each backend exports its own catalogue; the designer loads the appropriate catalogue
based on the selected tenant/environment.

---

## Versioning the Catalogue

Since the catalogue is static for a given deployment, it can be versioned simply:

```json
{
  "generatedAt": "2026-03-30T12:00:00Z",
  "backendVersion": "1.4.2",
  "blocks": [...]
}
```

A flow definition can optionally embed the `backendVersion` string at save time, so it is
always possible to identify which catalogue was active when the flow was authored. This is
useful if a block is renamed or removed in a future version.

---

## Relationship to Config Schema

The `configSchema` field in `BlockTypeEntry` is the same JSON Schema exposed by
`IConfigurableBlock.OptionsType` (see `block-configuration.md`). Embedding it in the catalogue
document means:

- The designer retrieves config schemas with a single catalogue fetch — no per-block schema
  endpoint needed in the simple case
- The schema is available for offline/static hosting scenarios (Scenario B/C above)
- `includeConfigSchemas: false` (default) keeps the catalogue small for palette-only use

In Phase 3, the separate `GET /flows/blocks/{key}/schema` endpoint remains useful for:
- On-demand schema refresh without reloading the full catalogue
- Dynamic schema resolution (e.g. schemas that depend on other config values)

---

## Integration with `FlowDefinition`

The `FlowDefinition` references blocks by key. The catalogue provides the authoritative list
of valid keys:

```
Catalogue (static JSON)           FlowDefinition (user-saved JSON)
  blocks[].key ──────────────────► blocks[].blockKey  (validated against catalogue)
  blocks[].inputTypeName           connections[].from/to (type compatibility)
  blocks[].outputTypeName
```

`DynamicFlowBuilder.Validate()` can optionally accept a `BlockCatalogueDocument` instead
of (or in addition to) a live `IBlockTypeRegistry`, enabling client-side validation in a
separately-hosted designer without a server round-trip.

---

## Summary

| Concern | Solution |
|---------|---------|
| Designer needs block metadata | Export `BlockCatalogueDocument` from registry |
| Metadata is static after startup | Generate once, cache indefinitely |
| Designer hosted separately | Serve catalogue as static JSON file |
| Offline / WASM designer | Bundle catalogue into WASM publish output |
| Config schema exposure | Include in catalogue (`includeConfigSchemas: true`) or via separate endpoint |
| Multi-version tracking | Embed `backendVersion` + `generatedAt` in catalogue |
| Client-side validation | `DynamicFlowBuilder.Validate(catalogue)` (no live registry needed) |
