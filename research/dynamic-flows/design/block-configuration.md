# Design: Block Configuration

**Version**: 1.0  
**Date**: 2026-03-30  
**Status**: Proposed

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

## Configuration Versioning

Config is **snapshot** in the flow definition at save time:

```json
{
  "flowId": "my-flow",
  "version": 2,
  "blockConfig": {
    "step1-producer": { "itemCount": 50, "delayMs": 200 },
    "step2-batch": { "batchSize": 10 }
  }
}
```

When the runner executes `version: 2`, it passes `blockConfig["step1-producer"]` to the
producer block, regardless of any subsequent changes saved in `version: 3`.

**Execution traceability**: the `triggerParamsJson` stored with the `FlowStartedEvent`
includes `{ "flowId": "my-flow", "version": 2 }`, so each invocation traces back to the
exact definition version (and embedded config) that produced it.

---

## EF Core Schema Addition

To support config editing persistence, the `FlowDefinitionRecord` EF entity would store:

```csharp
public class FlowDefinitionRecord
{
    [Key] public long Id { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;  // full FlowDefinition JSON
    public DateTime CreatedAt { get; set; }
    public bool IsCurrent { get; set; }  // true = latest version
}
```

Migration: additive column on existing `FlowVisualizationDbContext` or a separate
`FlowDefinitionDbContext` — depends on whether the user already has a custom DbContext.
