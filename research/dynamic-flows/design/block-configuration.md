# Design: Block Configuration

**Version**: 1.1  
**Date**: 2026-03-30  
**Status**: Proposed  
**Updated**: Incorporated feedback on config blob indirection and secrets handling

---

## Problem

Currently, DataFlow blocks receive their configuration at construction time (via constructor
injection or factory lambdas). There is no intrinsic mechanism for:
1. Exposing what configuration a block accepts
2. Editing that configuration from a UI
3. Persisting configuration as part of a flow definition version

This design document explores options for making blocks *configurable* in a way that:
- Is generic and works for all blocks without per-block UI code
- Integrates with the flow definition versioning model
- Provides a reasonable editing experience

---

## Options Explored

### Option A: Block-Owned Blazor Component

Each block ships a `<MyBlockSettingsEditor>` Blazor component.

**Pro**:
- Full control over UI per block
- Can use any Blazor component capabilities

**Con**:
- Requires each block to implement UI (blocks become UI-coupled)
- Inconsistent UX across blocks
- Designer host must discover and render arbitrary block components

**Verdict**: ❌ Too much per-block work. Fragments the UX.

---

### Option B: `IConfiguration` / `IOptions<T>` Binding

Blocks declare `TOptions` and receive it via DI. Config comes from `appsettings.json`.

```csharp
public class MyBlock : IBlock<int, string>
{
    public MyBlock(IOptions<MyBlockOptions> options) { ... }
}
```

**Pro**:
- Familiar .NET pattern
- No new APIs needed

**Con**:
- Static at startup — not editable at runtime
- No per-instance config (all instances of `MyBlock` share the same options)
- No snapshot in flow definition

**Verdict**: ❌ Doesn't support runtime editing or per-definition versioning.

---

### Option C: JSON Schema + JSON Forms ✅ (Recommended for v2)

Each block type exposes a **JSON Schema** describing its configuration.
The UI renders a generic form from the schema (using JSON Forms or similar).
The user fills in values; the result is stored as a `JsonElement` in `blockConfig`.
At execution time, the block receives its config JSON and deserializes it.

**How it works**:

```
IBlockTypeRegistry
  └─ RegisterBlock(key, metadata)
       └─ metadata.ConfigSchema (new: JSON Schema for config type)

Block config flow:
  1. Designer: fetch IBlockTypeRegistry.GetMetadata(key).ConfigSchema
  2. Designer: render JSON Forms editor from schema
  3. User fills values → raw JSON produced
  4. Definition saved with blockConfig["instanceId"] = rawJson
  5. Execution: DynamicFlowRunner passes rawJson to block
  6. Block: deserializes rawJson → typed TOptions
```

**Block contract** (proposed new interface):

```csharp
public interface IConfigurableBlock<TOptions> : IBlock
{
    /// <summary>Apply runtime configuration before execution.</summary>
    void ApplyConfig(TOptions options);
}
```

Or more generically (avoiding generic constraint on registry):

```csharp
public interface IConfigurableBlock : IBlock
{
    Type OptionsType { get; }
    void ApplyConfigJson(string optionsJson);
}
```

**JSON Schema generation**: .NET 9 ships `JsonSchemaExporter`. For .NET 8 (current target),
`NJsonSchema` or `System.Text.Json.JsonSchemaExporter` (preview) can generate schema from
a `Type`. Alternatively, blocks can provide the schema as a static resource.

**JSON Forms**: A JavaScript library (jsonforms.io) or a Blazor port. Since the designer will
be Blazor WASM, the simplest approach is to invoke the JSON Forms JS library via JS interop.

**Con**:
- JS interop needed for JSON Forms (or a Blazor reimplementation)
- NJsonSchema adds a NuGet dependency (unless we use .NET 9 built-in)
- Blocks need to implement `IConfigurableBlock`

**Verdict**: ✅ Best long-term approach. Uniform UX, no per-block UI code.

---

### Option D: Blazor `EditForm` + Reflection

Reflect on `TOptions` class, generate `InputText`/`InputNumber` components dynamically.

**Pro**: Pure Blazor, no JS interop, no schema needed

**Con**:
- Complex nested types are hard to render generically
- Limited to flat/simple options shapes
- Custom validation is difficult

**Verdict**: ⚠️ Viable for simple flat options, insufficient for complex configs.

---

## Recommended Approach

**Phase 1 (MVP)**: No config editing. Flow definition only captures topology.
Use factory lambdas to hardcode block configuration for now.

**Phase 2**: Introduce `IConfigurableBlock<TOptions>` interface.
Blocks that want to be configurable from the UI implement this.
Config schema exposed as `JsonSchema` string via registry metadata.

**Phase 3**: Full JSON Forms designer integration.
Generic config editing for all `IConfigurableBlock` blocks.

---

## Configuration Storage: Blob Indirection (not inline)

### Problem with inline config

Storing the config payload directly in the flow definition JSON has a critical flaw: **it
co-locates secrets (passwords, API keys, connection strings) with the topology definition**.
Even in an encrypted-at-rest database, this couples secret lifetime to definition lifetime
and makes secret rotation hard.

### Proposed: Config Blob Repository + Reference

Instead of embedding raw config in the definition, the definition holds a **reference** to
a separately-stored, versioned config blob:

```json
{
  "flowId": "my-flow",
  "version": 2,
  "blocks": [...],
  "connections": [...],
  "blockConfigRefs": {
    "step1-producer": "cfg-blob-abc123",
    "step2-batch":    "cfg-blob-def456"
  }
}
```

The actual config values live in a `IBlockConfigBlobRepository`:

```csharp
public interface IBlockConfigBlobRepository
{
    /// <summary>
    /// Save a config payload and return a stable blob ID.
    /// The implementation may encrypt the payload before persisting.
    /// </summary>
    Task<string> SaveAsync(string blockKey, string configJson, CancellationToken ct = default);

    /// <summary>
    /// Load a config blob by ID. The implementation transparently decrypts if needed.
    /// Returns null if the blob does not exist.
    /// </summary>
    Task<string?> LoadAsync(string blobId, CancellationToken ct = default);

    /// <summary>Delete a config blob (e.g. when a definition version is purged).</summary>
    Task DeleteAsync(string blobId, CancellationToken ct = default);
}
```

**Benefits of indirection**:
- Config values (including secrets) never appear in the flow definition JSON
- Config blobs can be independently versioned, rotated, or purged
- The blob repository is the single enforcement point for encryption / data protection
- The same blob ID can be referenced by multiple definition versions if config hasn't changed

### EF Core Schema

Two tables instead of one:

```csharp
// Flow topology + blob references (no raw config values)
public class FlowDefinitionRecord
{
    [Key] public long Id { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;  // FlowDefinition without blockConfig inline
    public DateTime CreatedAt { get; set; }
    public bool IsCurrent { get; set; }
}

// Independently versioned, (optionally encrypted) config blobs
public class BlockConfigBlobRecord
{
    [Key] public string BlobId { get; set; } = string.Empty;  // stable reference
    public string BlockKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;  // JSON or encrypted bytes (base64)
    public bool IsEncrypted { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## Secrets Handling

### The Problem

A block that connects to an external system (database, queue, API) will have configuration
like:

```json
{
  "connectionString": "Server=prod-db;...",
  "apiKey": "sk-live-abc123",
  "password": "hunter2"
}
```

Storing these transparently in a database column — even one encrypted at rest — violates the
principle of secret isolation. Secrets require:
- Key management (rotation without service restart)
- Audit trail
- Access control separate from the rest of the config

### Approach: JSON Schema `x-secret` Annotation + Transparent Encryption

#### Step 1: Mark secrets in the JSON Schema

Block authors annotate secret fields in their JSON Schema using a custom vocabulary keyword:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "host": {
      "type": "string",
      "description": "Database host"
    },
    "password": {
      "type": "string",
      "description": "Database password",
      "x-secret": true
    },
    "apiKey": {
      "type": "string",
      "description": "External API key",
      "x-secret": true
    }
  }
}
```

The designer UI uses `x-secret: true` to:
- Render a `<input type="password">` (masked) instead of plain text
- Visually flag the field as sensitive

#### Step 2: Repository transparently encrypts `x-secret` fields

When the designer calls `IBlockConfigBlobRepository.SaveAsync()`, the implementation:

1. Deserialises the config JSON into a `JsonDocument`
2. Walks the JSON Schema for the block and identifies all `x-secret: true` leaf paths
3. For each secret path, reads the plain-text value, encrypts it using **ASP.NET Core Data
   Protection** (`IDataProtectionProvider`), and replaces the value with the ciphertext
4. Sets `IsEncrypted = true` on the stored blob
5. Returns the stable blob ID — this is all the flow definition ever sees

```
Save flow:
  designer submits → { "host": "prod-db", "password": "hunter2", "apiKey": "sk-live-abc123" }
  repository encrypts → { "host": "prod-db", "password": "<encrypted>", "apiKey": "<encrypted>" }
  stored blob ID "cfg-blob-abc123" returned
  flow definition stores: "blockConfigRefs": { "step1-producer": "cfg-blob-abc123" }
```

#### Step 3: Repository transparently decrypts at execution time

When `DynamicFlowRunner` needs the config for a block, it calls
`IBlockConfigBlobRepository.LoadAsync("cfg-blob-abc123")`:

1. Loads the stored blob
2. If `IsEncrypted = true`, decrypts all `x-secret` leaf paths using Data Protection
3. Returns the fully-decrypted JSON string to the runner
4. Runner passes it to `IConfigurableBlock.ApplyConfigJson()`

```
Load for execution:
  stored blob retrieved → { "host": "prod-db", "password": "<encrypted>", "apiKey": "<encrypted>" }
  repository decrypts → { "host": "prod-db", "password": "hunter2", "apiKey": "sk-live-abc123" }
  plain-text JSON returned only to the execution path (never to the client/designer)
```

#### Why ASP.NET Core Data Protection?

- Built into the .NET ecosystem — no new NuGet dependency for most hosts
- Key management handled by the host (filesystem, Azure Key Vault, etc.) — easily
  configured by the application
- Key rotation: old ciphertexts can still be decrypted after key rollover
- Per-purpose isolation: the blob repository registers a purpose string so its keys are
  isolated from other Data Protection uses in the app

```csharp
// Registration (proposed)
services.AddDataFlowDefinitions(options =>
{
    options.UseSqlite("Data Source=definitions.db");
    options.UseDataProtectionForSecrets();  // enables x-secret encryption
});

// Internally:
internal class EfCoreBlockConfigBlobRepository : IBlockConfigBlobRepository
{
    private readonly IDataProtector _protector;

    public EfCoreBlockConfigBlobRepository(IDataProtectionProvider dpProvider, ...)
    {
        _protector = dpProvider.CreateProtector("DataFlow.BlockConfig.Secrets");
    }
    // ...
}
```

### Secret Rotation

When a secret (e.g. a password) changes:
1. User opens the block's config editor for the current definition version
2. Updates the secret field
3. Saves — this creates a new config blob and a new definition version
4. The old blob remains (for historical run replay) but the new version references the new blob
5. The old blob can be scheduled for deletion after a retention window

### What About External Secret Stores (Key Vault, etc.)?

For production workloads, the `IBlockConfigBlobRepository` can be implemented against
an external secret store:

```csharp
// Example: store non-secret config in DB, secrets in Azure Key Vault
public class KeyVaultBlockConfigBlobRepository : IBlockConfigBlobRepository
{
    // Stores non-secret fields in DB, writes x-secret fields as Key Vault secrets
    // Returns a composite blob ID encoding both references
}
```

The interface abstraction makes this pluggable without changing the designer UI or the
execution engine.

---

## Configuration Versioning

Config is versioned **independently of the flow topology** via the blob indirection model:

```
flow definition v1  →  blockConfigRefs: { "producer": "cfg-blob-abc" }  →  blob abc (config at v1 save time)
flow definition v2  →  blockConfigRefs: { "producer": "cfg-blob-def" }  →  blob def (config at v2 save time)
```

When the runner executes `version: 1`, it loads blob `cfg-blob-abc` (decrypting secrets on
the fly), regardless of any subsequently saved config blobs.

**Execution traceability**: `triggerParamsJson` stored with `FlowStartedEvent` includes
`{ "flowId": "my-flow", "version": 1 }`, tracing each invocation to the exact definition
version (and therefore the exact config blob, including the exact secret values used).

If config **hasn't changed** between definition versions, the same blob ID can be reused —
the `IBlockConfigBlobRepository` can detect unchanged payloads and return the existing blob
ID instead of creating a duplicate.

---

## Summary: Storage Layers

| Layer | Contains | Encrypted? |
|-------|----------|-----------|
| `FlowDefinitionRecord` | Topology (blocks, connections) + `blockConfigRefs` map | At-rest (DB level) |
| `BlockConfigBlobRecord` | Per-block config JSON with `x-secret` fields encrypted | Field-level (Data Protection) |
| Key store (Data Protection) | Encryption keys for secret fields | Managed by host (filesystem / Key Vault) |
