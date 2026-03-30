# Research: Dynamic Flows

**Status**: ✅ Complete  
**Research Date**: 2026-03-30  
**Research Issue**: [Research] Dynamic Flows  
**Prototype Location**: `/research/dynamic-flows/handover/prototype/`

---

## Research Objective

Investigate whether the DataFlow library can support **metadata-driven, dynamically-defined
pipelines** — flows whose topology is defined at runtime from a serializable definition
(JSON/YAML) rather than hardcoded in C# — and explore the full design space from execution
engine to designer UI to block configuration.

---

## Executive Summary

**✅ Research Validated** — All nine research questions answered. A minimal prototype
demonstrates end-to-end dynamic flow execution using only existing APIs, with no changes
to production code. The path to a full designer feature is clear and broken into well-scoped
phases.

**Key finding**: The existing `IBlockTypeRegistry` (which stores `InputType`/`OutputType`
for every registered block) provides exactly the metadata needed to validate, build, and
execute a dynamically-defined flow.

---

## Research Questions & Findings

### Q1: Do we have enough metadata to build a designer feature?

**Answer**: ✅ Yes — with the existing `IBlockTypeRegistry`.

Every registered block key maps to `IBlockTypeMetadata` with `InputType` and `OutputType`.
This is sufficient to:
- Enumerate available blocks for a palette
- Validate that `A.OutputType` is assignable to `B.InputType` for any connection
- Build a `DataFlowGraph` from the validated definition at runtime

The `DataFlowGraphBuilder` already accepts string-key-based connections via `UseBlock()` +
`Connect()`, so the dynamic builder is a thin translation layer, not a new execution path.

**Gaps**: No display metadata for the designer palette is currently exposed via API.
The block registry would need a new HTTP endpoint (`GET /flows/blocks`) that returns
block keys with display names and type info. This is straightforward to add.

---

### Q2: What should a flow definition look like?

**Answer**: JSON, using the schema defined in `/research/dynamic-flows/design/flow-definition-schema.md`.

**Format decision**: JSON over YAML. Built into .NET (`System.Text.Json`), zero extra dependencies,
natural API transport format, easy to machine-generate from a designer canvas.

**Example**:
```json
{
  "flowId": "invoice-processing-v1",
  "version": 1,
  "name": "Invoice Processing Pipeline",
  "blocks": [
    { "instanceId": "global:producer",   "blockKey": "global:producer" },
    { "instanceId": "global:validator",  "blockKey": "invoices:validator" },
    { "instanceId": "global:processor",  "blockKey": "invoices:processor" }
  ],
  "connections": [
    { "from": "global:producer",  "to": "global:validator",  "bufferCapacity": 100 },
    { "from": "global:validator", "to": "global:processor",  "bufferCapacity": 100 }
  ],
  "blockConfig": {
    "global:producer": { "itemCount": 100, "delayMs": 50 }
  }
}
```

The `blockConfig` section is optional in v1 (blocks use hardcoded defaults).
Configuration editing is a Phase 2 feature.

---

### Q3: How do we execute a dataflow from a definition?

**Answer**: Via `DynamicFlowBuilder` (prototype provided).

```
FlowDefinition (JSON)
  → Validate() — checks block keys exist, type compatibility
  → Build(serviceProvider, registry) — calls DataFlowGraphBuilder.UseBlock() + .Connect()
  → Returns DataFlowGraph — identical to a hardcoded graph
  → ExecuteAsync(ctx) — identical execution path
```

The prototype `DynamicFlowBuilder.cs` demonstrates this with ~100 lines of code. The
`DataFlowGraphBuilder.Build()` call performs DI resolution of all block instances.

See `/research/dynamic-flows/handover/prototype/` for the full prototype code.

---

### Q4: What happens when a flow definition is invalid?

**Answer**: The `DynamicFlowBuilder.Validate()` method returns a list of descriptive errors
before any execution begins. If `Build()` is called on an invalid definition, it throws
`InvalidOperationException` with all validation errors.

**The existing visualization already handles this**: `FlowRunsList.razor` shows `✗ Failed`
with `ErrorMessage` for any failed flow. The implementation should emit a
`FlowStartedEvent` + `FlowFailedEvent` pair even for build-time failures so that users see
the error in the UI (not just in the server logs).

**Validation checks**:
1. All block keys exist in the registry
2. All connection source/target `instanceId`s reference known blocks
3. Type compatibility: `source.OutputType` assignable to `target.InputType`
4. (Future) Cycle detection, source/sink validation

---

### Q5: How should applications persist flow definitions?

**Answer**: Via `IFlowDefinitionRepository` abstraction + default EF Core implementation.

```csharp
public interface IFlowDefinitionRepository
{
    Task SaveAsync(FlowDefinition definition, CancellationToken cancellationToken = default);
    Task<FlowDefinition?> GetLatestAsync(string flowId, ...);
    Task<FlowDefinition?> GetVersionAsync(string flowId, int version, ...);
    Task<IReadOnlyList<FlowDefinitionSummary>> ListAsync(...);
}
```

**Default implementation**: `EfCoreFlowDefinitionRepository` that adds a
`FlowDefinitionRecords` table to the existing `FlowVisualizationDbContext` (or a new
`FlowDefinitionDbContext` for apps that don't use the visualization server).

**Registration** (proposed API):
```csharp
// Use existing visualization DbContext (adds table to it)
services.AddDataFlowDefinitions(options => options.UseExistingDbContext<FlowVisualizationDbContext>());

// Or: standalone definition storage only
services.AddDataFlowDefinitions(options => options.UseSqlite("Data Source=definitions.db"));
```

An `InMemoryFlowDefinitionRepository` is provided for demos and tests.

---

### Q6: Should we validate with a demo?

**Answer**: Yes — recommended as part of the implementation phase.

The prototype demonstrates the concept but does not run in the actual demo app.
The implementation issue should include a task to add a "Dynamic Flow" demo page:
- Shows the block registry palette
- Lets users build a simple linear flow via a minimal UI
- Executes it and shows the run in the existing `FlowVisualization` component

This provides a compelling, concrete demonstration of the feature.

---

### Q7: What would a sane designer UI look like?

**Answer**: See `/research/dynamic-flows/design/designer-ui.md` for the full design.

**Minimal v1 scope** (validates the concept without complex drag-and-drop):
1. Block palette: list of available blocks from registry API
2. Ordered block list: user adds blocks in sequence
3. Auto-connections: sequential connections assumed (simple linear only)
4. Type validation: live feedback on incompatible connections
5. Save definition + run → view in existing `FlowVisualization`

**v2+ enhancements** (after v1 proves the concept):
- Graphical canvas with drag-and-drop positioning
- Fan-out / fan-in topologies
- Block configuration editing panel
- Definition version history

---

### Q8: How can blocks become configurable?

**Answer**: JSON Schema + JSON Forms approach — see `/research/dynamic-flows/design/block-configuration.md`.

**Recommended approach**:
1. Blocks that support runtime configuration implement `IConfigurableBlock<TOptions>`:
   ```csharp
   public interface IConfigurableBlock : IBlock
   {
       Type OptionsType { get; }
       void ApplyConfigJson(string optionsJson);
   }
   ```

2. The block registry stores the JSON Schema for the block's `TOptions` type (generated
   automatically from the type via `System.Text.Json.JsonSchemaExporter` in .NET 9, or
   `NJsonSchema` for .NET 8). Secret fields are annotated with `"x-secret": true`.

3. The designer UI uses **JSON Forms** (via JS interop) to render a generic config editor
   from the schema — no per-block UI code required. `x-secret` fields render as password inputs.

4. The user-edited config is **not** stored inline in the definition. Instead, it is saved to
   an `IBlockConfigBlobRepository` which transparently encrypts `x-secret` fields using
   **ASP.NET Core Data Protection** before persisting. The repository returns a stable blob ID;
   only this ID is stored in `FlowDefinition.blockConfigRefs`.

5. At execution time, `DynamicFlowRunner` loads the blob (decrypting secrets on the fly) and
   calls `ApplyConfigJson()` on the block before the graph runs.

**Phase 1 (v1)**: No config editing. Blocks use hardcoded defaults.  
**Phase 2**: `IConfigurableBlock` + JSON Schema exposure + generic form in designer + `IBlockConfigBlobRepository` with Data Protection.  
**Phase 3**: Full JSON Forms integration with complex types; external secret stores (Key Vault).

---

### Q9: How do we keep a config snapshot per definition version?

**Answer**: Via the blob indirection model — the definition stores blob IDs, not inline values.

- Config values are stored as versioned blobs in `IBlockConfigBlobRepository`
- `FlowDefinition.blockConfigRefs` maps each block instance to the blob ID saved at definition-save time
- `FlowDefinition` is immutable once published — the blob IDs it references are frozen
- New config → call `SaveAsync()` → new blob ID → new definition version with new blob IDs
- Historical runs load the blob IDs from their definition version → identical config (and secrets) to what ran originally
- Historical runs (identified by `invocationId`) reference the definition version via
  `triggerParamsJson` (stored in `FlowStartedEvent`): `{ "flowId": "...", "version": 2 }`

This also enables **secret rotation** without losing history: rotate the secret, save a new blob,
create a new definition version. Old runs still reference the old blob (which remains decryptable
via Data Protection's key history).

---

## Recommended Approach: Summary

| Aspect | Recommendation |
|--------|---------------|
| Definition format | JSON (`FlowDefinition` model — topology + blob refs only) |
| Storage | `IFlowDefinitionRepository` + EF Core default |
| Config storage | `IBlockConfigBlobRepository` — separate table, field-level encryption for secrets |
| Secrets | ASP.NET Core Data Protection, `x-secret: true` JSON Schema annotation |
| Execution | `DynamicFlowBuilder` → `DataFlowGraphBuilder` (no new execution path) |
| Validation | Pre-flight `Validate()` + descriptive errors surfaced in UI |
| Error UI | Already handled by `FlowRunsList.razor` (needs build-error events) |
| Config | Phase 2: `IConfigurableBlock` + JSON Schema + `IBlockConfigBlobRepository`; Phase 1: hardcoded defaults |
| Config versioning | Blob IDs frozen in immutable definition version |
| Designer UI | Phase 1: minimal list-based; Phase 2: graphical canvas |

---

## Implementation Phasing

### Phase 1: Dynamic Execution Engine (Core)

**Goal**: Execute a flow from a JSON definition. No UI.

Deliverables:
- `FlowDefinition` model + JSON serialization
- `DynamicFlowBuilder` (validate + build from definition)
- `IFlowDefinitionRepository` + `InMemoryFlowDefinitionRepository` + EF Core implementation
- `DynamicFlowRunner` service
- `GET /flows/blocks` API endpoint (exposes registry to client)
- `POST /flows/run/dynamic` endpoint
- Unit tests for validation and execution

### Phase 2: Minimal Designer UI

**Goal**: Compose and run flows from the UI.

Deliverables:
- Block palette component (reads from `GET /flows/blocks`)
- Simple block list + linear connection builder
- Type compatibility validation display
- Save definition + version management
- Demo page in `DataFlow.Blazor.Demo`

### Phase 3: Block Configuration

**Goal**: Configure blocks from the UI.

Deliverables:
- `IConfigurableBlock` interface + registry schema storage
- `IBlockConfigBlobRepository` interface + EF Core + in-memory implementations
- ASP.NET Core Data Protection integration for `x-secret` field encryption
- `GET /flows/blocks/{key}/schema` endpoint
- JSON Forms integration (or Blazor reflection-based editor for simple types)
- Config blob IDs stored in `FlowDefinition.blockConfigRefs`
- Config blob loading + `ApplyConfigJson()` in `DynamicFlowRunner`

### Phase 4: Full Visual Designer

**Goal**: Drag-and-drop canvas, fan-out/fan-in, version history.

---

## Success Metrics Results

| Metric | Result |
|--------|--------|
| Approach validated through prototyping | ✅ `DynamicFlowBuilder` prototype compiles and uses existing APIs |
| Research comprehensively documented | ✅ All 9 questions answered |
| Implementation issue contains complete context | ✅ See handover |
| All supporting documentation created | ✅ Schema design, config design, UI design, ADR |
| Prototype code captured | ✅ `/research/dynamic-flows/handover/prototype/` |
| Code changes reverted (only docs remain) | ✅ No POC code was modified |
| Self-improvement evaluation completed | ✅ See issue comment |

---

## References

- **Prototype**: `/research/dynamic-flows/handover/prototype/`
- **Flow Definition Schema**: `/research/dynamic-flows/design/flow-definition-schema.md`
- **Block Configuration Design**: `/research/dynamic-flows/design/block-configuration.md`
- **Designer UI Design**: `/research/dynamic-flows/design/designer-ui.md`
- **Exploration Notes**: `/research/dynamic-flows/notes/exploration-notes.md`
- **Implementation Handover**: `/research/dynamic-flows/handover/github-issue-implement-dynamic-flows.md`
