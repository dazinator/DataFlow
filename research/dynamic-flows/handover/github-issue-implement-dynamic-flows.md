# [Implementation] Dynamic Flows — Phase 1: Execution Engine

**Research Reference**: [Research] Dynamic Flows  
**Research Documentation**: `/research/dynamic-flows/`  
**Related ADR**: `/poc/docs/adr/2026-03-30-dynamic-flow-definition-format.md`

---

## ⚠️ This is an IMPLEMENTATION work item

Follow `.team/duties/IMPLEMENTATION_DUTY.md`.

---

## Objective

Implement the **Dynamic Flow Execution Engine** — the ability to execute a DataFlow pipeline
from a JSON definition at runtime, without hardcoding the topology in C#.

This is Phase 1 of the dynamic flows feature. It covers the core execution engine and
persistence layer. Designer UI and block configuration are later phases.

---

## Approach (Validated by Research)

The research prototype (`/research/dynamic-flows/handover/prototype/`) demonstrates that:

1. The existing `IBlockTypeRegistry` provides sufficient metadata to validate and build dynamic flows
2. `DataFlowGraphBuilder` already supports string-key-based block resolution (`UseBlock()` + `Connect()`)
3. The `DynamicFlowBuilder` is a thin translation layer: JSON definition → `DataFlowGraphBuilder` calls
4. Execution is identical to hardcoded flows — the same `DataFlowGraph.ExecuteAsync()` path

**No new execution primitives are needed.** The implementation is:
- New model classes (`FlowDefinition`, `FlowBlockReference`, `FlowConnection`)
- New service classes (`DynamicFlowBuilder`, `DynamicFlowRunner`, repository implementations)
- New HTTP endpoints (`GET /flows/blocks`, `POST /flows/run/dynamic`, definition CRUD)
- New EF Core entity + migration for definition storage

---

## Success Criteria

- [ ] `DynamicFlowBuilder.Validate()` returns descriptive errors for invalid definitions
- [ ] `DynamicFlowBuilder.Build()` produces a runnable `DataFlowGraph` from a valid definition
- [ ] Type mismatch connections are rejected with a clear error message
- [ ] A dynamic flow runs end-to-end and its events appear in the existing visualization UI
- [ ] A failed dynamic flow (invalid definition or execution error) appears as `✗ Failed` in `FlowRunsList`
- [ ] `IFlowDefinitionRepository` interface + EF Core + in-memory implementations
- [ ] `GET /flows/blocks` endpoint returns all registered block types with metadata
- [ ] `POST /flows/definitions` and `GET /flows/definitions/{id}` CRUD endpoints
- [ ] `POST /flows/run/dynamic` triggers execution and returns `invocationId`
- [ ] Unit tests covering validation, build, and execution scenarios
- [ ] All existing tests continue to pass

---

## Implementation Checklist

### Core Engine (DataFlow library)
- [ ] Add `FlowDefinition`, `FlowBlockReference`, `FlowConnection` model classes
- [ ] Add `DynamicFlowBuilder` class (validate + build)
- [ ] Add `IFlowDefinitionRepository` interface
- [ ] Add `InMemoryFlowDefinitionRepository` implementation
- [ ] Add `DynamicFlowRunner` service

### Persistence (DataFlow.Blazor.Server)
- [ ] Add `FlowDefinitionRecord` EF entity
- [ ] Add `FlowDefinitionRecord` to `FlowVisualizationDbContext` (or new `FlowDefinitionDbContext`)
- [ ] Add `EfCoreFlowDefinitionRepository` implementation
- [ ] Add migration / schema creation for `FlowDefinitionRecords` table

### HTTP Endpoints (DataFlow.Blazor.Server)
- [ ] `GET /flows/blocks` — returns block registry as JSON
- [ ] `POST /flows/definitions` — saves a flow definition (new or new version)
- [ ] `GET /flows/definitions` — lists all flow definitions
- [ ] `GET /flows/definitions/{flowId}` — returns latest version
- [ ] `GET /flows/definitions/{flowId}/{version}` — returns specific version
- [ ] `POST /flows/run/dynamic` — runs a definition by ID + optional version

### Build-Error Events
- [ ] When `DynamicFlowBuilder.Build()` fails, emit `FlowStartedEvent` + `FlowFailedEvent`
      so the error surfaces in `FlowRunsList` (not just server logs)

### DI Registration
- [ ] `services.AddDataFlowDefinitions()` extension method
- [ ] Support `UseExistingDbContext<TContext>()` for apps with existing DbContext

### Tests
- [ ] `DynamicFlowBuilderTests` — validation, type compatibility, build
- [ ] `DynamicFlowRunnerTests` — end-to-end execution with mock blocks
- [ ] `FlowDefinitionRepositoryTests` — CRUD for in-memory and EF implementations
- [ ] API endpoint integration tests

---

## Design References

- **Flow Definition Schema**: `/research/dynamic-flows/design/flow-definition-schema.md`
- **Block Configuration** (Phase 2): `/research/dynamic-flows/design/block-configuration.md`
- **Designer UI** (Phase 2): `/research/dynamic-flows/design/designer-ui.md`
- **Prototype Code**: `/research/dynamic-flows/handover/prototype/`
- **Research README**: `/research/dynamic-flows/README.md`
- **ADR**: `/poc/docs/adr/2026-03-30-dynamic-flow-definition-format.md`

---

## Prototype Code Reference

The prototype in `/research/dynamic-flows/handover/prototype/` can be used as a
**direct starting point** for the implementation. The key files to adapt:

| Prototype File | Production Target |
|---------------|-------------------|
| `FlowDefinition.cs` | `DataFlow.DynamicFlows/FlowDefinition.cs` or `DataFlow.POC/DynamicFlows/FlowDefinition.cs` |
| `DynamicFlowBuilder.cs` | `DataFlow.POC/DynamicFlows/DynamicFlowBuilder.cs` |
| `IFlowDefinitionRepository.cs` | `DataFlow.POC/DynamicFlows/IFlowDefinitionRepository.cs` |
| `DynamicFlowRunner.cs` | `DataFlow.Blazor.Server/DynamicFlows/DynamicFlowRunner.cs` |

---

## Out of Scope (Later Phases)

- Designer UI (Phase 2 — see `design/designer-ui.md`)
- Block configuration editing (Phase 3 — see `design/block-configuration.md`)
- Graphical drag-and-drop canvas (Phase 4)
- Block aliasing (two instances of same type in one flow) — v2 of Phase 1
