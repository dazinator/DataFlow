# Exploration Notes: Dynamic Flows

**Date**: 2026-03-30

---

## 1. Block Type Registry - What's Available

### `IBlockTypeRegistry` (already exists)

Provides:
- `RegisterBlock(string key, IBlockTypeMetadata metadata)` - registers a block by key
- `GetMetadata(string key)` - returns `IBlockTypeMetadata` with `InputType` and `OutputType`
- `GetAllBlockMetadata()` - returns all `(key, IBlockTypeMetadata)` pairs
- `GetBlock(IServiceProvider, string key)` - resolves a live `IBlock` instance from DI
- `GetAllBlockKeys()` / `GetBlockKeys(namespacePrefix)` - enumerate registered blocks

### `IBlockTypeMetadata` (already exists)

```csharp
public interface IBlockTypeMetadata
{
    Type InputType { get; }
    Type OutputType { get; }
}
```

**Conclusion**: We have exactly what we need. Given two connected blocks A→B we can check:
`A.OutputType == B.InputType` (or `B.InputType.IsAssignableFrom(A.OutputType)`)

---

## 2. How Graphs are Currently Built

Graphs are built by `DataFlowGraphBuilder`:
- `UseBlock(string name)` - queues a block for DI resolution
- `Connect(source, target)` / `Connect(sourceName, targetName)` - creates edges
- `Build(IServiceProvider, IBlockTypeRegistry)` - resolves all blocks and wires up the graph

**Key insight**: `DataFlowGraphBuilder` already accepts string-key-based connections:
```csharp
builder.UseBlock("producer").UseBlock("transform").Connect("producer", "transform");
```
This is exactly what a dynamic builder needs to do after parsing a JSON definition.

---

## 3. IDataFlowDefinition - What's Available

```csharp
public interface IDataFlowDefinition
{
    void Configure(DataFlowGraphBuilder builder);
}
```

A dynamic definition class could implement this interface, receiving its
topology from a deserialized JSON definition at runtime.

---

## 4. Error State in UI - What's Already There

From `FlowRunsList.razor`:
```razor
@if (group.Primary.ErrorMessage is not null)
{
    <span title="@group.Primary.ErrorMessage" ...>...</span>
}
```
`FlowState.Failed → "✗ Failed"` with red styling.

The `FlowRunState` has:
```csharp
public string? ErrorMessage { get; init; }
public FlowState Status { get; init; } = FlowState.NotStarted;
```

And from `FlowStateProjector`, a `FlowFailedEvent` transitions state to `FlowState.Failed`.
**Conclusion**: The UI already handles failed flows including error message display. If a
dynamic flow fails to build or execute, the existing visualization will surface this.

---

## 5. Persistence - Existing Infrastructure

`FlowVisualizationDbContext` stores `FlowEventRecord` and `FlowSnapshotRecord`.
This is designed to be extensible via `DataFlowModelBuilderExtensions.AddDataFlowVisualizationEntities()`.

**Approach for flow definitions**:
1. Define `IFlowDefinitionRepository<TDefinition>` abstraction
2. Provide `EfCoreFlowDefinitionRepository` that adds a `FlowDefinitions` table
3. Applications can use the default or implement their own (e.g., filesystem, blob storage)

---

## 6. Type Compatibility Validation

For `ProducerBlock` (output `int`) → `TransformBlock` (input `int`) → `BatchBlock` (input `int`, output `List<int>`) → `ProcessorBlock` (input `List<int>`):

```
"global:producer"  → output: int
"global:transform" → input: int, output: int        ✅ compatible
"global:batch"     → input: int, output: List<int>  ✅ compatible  
"global:processor" → input: List<int>               ✅ compatible
```

Edge validation: `registry.GetMetadata(sourceKey).OutputType.IsAssignableFrom(targetInputType)` - wait actually we want `targetInputType.IsAssignableFrom(sourceOutputType)` or simply `sourceOutputType == targetInputType` for strict checking. For polymorphic types use `IsAssignableFrom`.

---

## 7. Definition Format Decision

Options considered:
1. **JSON** - Built into .NET, no extra deps, schema validation via `System.Text.Json.JsonSchemaExporter` (.NET 9) or `NJsonSchema`
2. **YAML** - More human-readable but requires a YAML library (YamlDotNet etc.)
3. **Custom DSL** - Overkill
4. **C# code generation** - Too complex for a designer

**Decision**: JSON for v1. Simple, zero extra dependencies.

Sample:
```json
{
  "flowId": "my-flow-v1",
  "version": 1,
  "name": "My Custom Flow",
  "blocks": [
    { "instanceId": "step1", "blockKey": "global:producer" },
    { "instanceId": "step2", "blockKey": "global:transform" },
    { "instanceId": "step3", "blockKey": "global:processor" }
  ],
  "connections": [
    { "from": "step1", "to": "step2", "bufferCapacity": 100 },
    { "from": "step2", "to": "step3", "bufferCapacity": 100 }
  ]
}
```

**Config snapshot (for versioning)**:
```json
{
  "flowId": "my-flow-v1",
  "version": 2,
  "blocks": [...],
  "connections": [...],
  "blockConfig": {
    "step1": { "itemCount": 50, "delayMs": 200 }
  }
}
```

---

## 8. Block Configuration via JSON Schema

**Problem**: Blocks don't currently have a generic configuration mechanism.

**Approaches explored**:

### Approach A: Block-owned Blazor component
- Blocks ship their own `<MyBlockSettingsEditor>` component
- Most flexible but each block must implement UI
- Fragments the UX across blocks

### Approach B: `IOptions<T>` / `IConfiguration` binding
- Blocks declare `TOptions` type
- Library binds config from IConfiguration at startup
- Good for static config, bad for runtime editable config

### Approach C: JSON Forms / JSON Schema (preferred)
- Block exposes a JSON Schema for its configuration class
- Generic UI renders the form from the schema
- JSON Forms (https://jsonforms.io) or similar renders the editor
- Block gets config as raw JSON → deserializes to typed options
- **Preferred**: single unified approach, no per-block UI code

### Approach D: Blazor EditForm with reflection
- Reflect on options type, generate form fields dynamically
- More work but no JS dependency

**Recommendation**: JSON Schema approach (C) for the generic config editing.
See `/research/dynamic-flows/design/block-configuration.md` for details.

---

## 9. Configuration Versioning

Store the `blockConfig` JSON as part of the flow definition snapshot. When a flow is executed
from a definition, the `blockConfig` embedded in that version is used—even if the user has
subsequently saved a newer version with different config.

This means:
- Flow definition `v1` with config `{ "delayMs": 200 }` always runs with that config
- Flow definition `v2` with config `{ "delayMs": 100 }` runs with the new config
- The `FlowEventRecord` / `FlowSnapshotRecord` captures the `InvocationId` which links back
  to the exact definition version that was used (if we store `definitionId` + `definitionVersion`
  in the trigger params JSON)

---

## 10. DynamicFlowBuilder Prototype Key Insights

The prototype (`DynamicFlowBuilder.cs`) demonstrates:

1. **Parse** definition JSON → `FlowDefinition`
2. **Validate** type compatibility for each connection using registry metadata
3. **Build** `DataFlowGraphBuilder` from the parsed definition
4. **Execute** exactly like any other `DataFlowGraph`

The `DataFlowGraphBuilder` already handles the DI resolution via `UseBlock()` + `Connect()`.
So the dynamic builder is essentially a translator from JSON → `DataFlowGraphBuilder` calls.

**Key code path**:
```csharp
// For each block in definition:
builder.UseBlock(block.BlockKey);

// For each connection in definition:
builder.Connect(conn.From, conn.To, conn.BufferCapacity);

// Build and execute:
var graph = builder.Build(serviceProvider, registry);
await graph.ExecuteAsync(ctx);
```

This means the block instance naming is slightly different: the `instanceId` in the definition
maps to the block key for DI resolution, but the block's own `.Name` property comes from the
registered IBlock. We may need to support aliasing (e.g., using the same block type twice with
different instance IDs) - this would require cloning/wrapping. For v1, one-block-type-per-instance
is sufficient.
