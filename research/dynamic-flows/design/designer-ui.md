# Design: Flow Designer UI

**Version**: 1.0  
**Date**: 2026-03-30  
**Status**: Proposed

---

## Overview

A Blazor WASM designer component that allows users to compose DataFlow pipelines by
connecting registered blocks, without writing code. The designer reads available block
types from the `IBlockTypeRegistry` (via a new API endpoint) and produces a `FlowDefinition`.

---

## Data Flow of the Designer

```
Server                                    Client (Blazor WASM)
──────────────────────────────────────    ──────────────────────────────────────
GET /flows/blocks                         Fetch available block types
  → [ { key, displayName, inputType,      Display palette of blocks
         outputType, configSchema? } ]
                                          User drags blocks onto canvas
                                          User connects blocks
                                          Compatibility shown live
GET /flows/definitions                    List saved definitions (optional)
POST /flows/definitions                   Save new definition
POST /flows/run/dynamic                   Execute definition
  → { invocationId }                      View in existing FlowVisualization
```

---

## UI Components

### 1. Block Palette

- Left panel listing available block types from registry
- Each block shows: name, type label, input/output type badges
- Blocks grouped by namespace (e.g., "global", "invoices")
- Drag handle for adding to canvas

### 2. Flow Canvas

- Center area where blocks are placed
- Blocks represented as cards with input/output ports
- Compatible output→input connections drawn as arrows
- Incompatible connections shown in red with tooltip
- Canvas state serializes to `FlowDefinition`

### 3. Connection Validation

- When user attempts to connect block A to B:
  - Client checks `A.outputType == B.inputType` (or assignable)
  - If compatible: draw green arrow
  - If incompatible: show error message "Type mismatch: A outputs `int`, B expects `string`"

### 4. Block Config Panel (Phase 2)

- Right panel appears when a block is selected
- If block has `configSchema`: renders JSON Forms editor
- User edits values; they are saved into `blockConfig[instanceId]`

### 5. Definition Actions

- **Save**: POST definition to `/flows/definitions`
- **Run**: POST to `/flows/run/dynamic` → shows invocation in FlowVisualization
- **History**: View saved versions of this definition

---

## Technical Approach

### Block Registry API Endpoint

```http
GET /flows/blocks
Response: [
  {
    "key": "global:producer",
    "displayName": "Producer",
    "typeLabel": "Source",
    "inputType": null,
    "outputType": "System.Int32",
    "configSchema": null
  },
  {
    "key": "global:transform",
    "displayName": "Transform",
    "typeLabel": "Propagator",
    "inputType": "System.Int32",
    "outputType": "System.Int32",
    "configSchema": null
  }
]
```

### Canvas State

The canvas maintains a list of `BlockNode` (with position x/y for layout) and 
`ConnectionEdge` objects. These serialize to `FlowDefinition` on save.

```typescript
// Conceptual canvas model (may be C# in Blazor)
interface CanvasState {
  blocks: { instanceId: string; blockKey: string; x: number; y: number; }[];
  connections: { from: string; to: string; bufferCapacity: number; }[];
}
```

### Drag and Drop

Blazor 8 ships `IJSRuntime` interop for DnD events. Options:
1. HTML5 Drag & Drop API (built-in, basic)
2. `MudBlazor` or `Radzen` component libraries (DnD support built-in)
3. Custom SVG canvas with mouse events

**Recommendation for PoC**: HTML5 DnD is sufficient to validate the concept.
Full designer UX can use a more capable component library.

---

## Minimal Demo Scope

For the first implementation, the designer UI scope is:

1. ✅ List available blocks (from registry API)
2. ✅ Add blocks to a flow (ordered list, no graphical canvas)
3. ✅ Connect blocks in sequence (simple linear topology only)
4. ✅ Validate type compatibility
5. ✅ Save definition as JSON
6. ✅ Execute and view in visualization

**Out of scope for v1**:
- Graphical canvas with drag positioning
- Fan-out / fan-in topologies (complex connection modes)
- Block configuration editing
- Version history UI
- Delete/reorder blocks

This minimal scope is sufficient to prove the concept and provide value.

---

## Error States

When a dynamic flow fails to **build** (validation error):
- The `DynamicFlowBuilder.Validate()` throws `InvalidOperationException`
- `DynamicFlowRunner` catches it and logs the error
- **Important**: To surface the error in the visualization, we need to emit a
  `FlowStartedEvent` + `FlowFailedEvent` pair. Currently the runner doesn't emit these
  unless `ExecuteAsync` is called. We should emit a minimal failure record so the UI shows
  "✗ Failed: Block 'xyz' not found" in the Flow Run List.

The existing `FlowRunsList.razor` already handles `FlowState.Failed` with error message
display — so the visualization is ready; we just need to ensure the error events are emitted.
