# Prototype Code: Dynamic Flow Builder

This prototype demonstrates dynamic flow execution from a JSON definition.

---

## Purpose

Validates that the existing `IBlockTypeRegistry` and `DataFlowGraphBuilder` provide
sufficient foundation to execute a flow from a runtime-parsed definition — without
modifying any production code.

---

## Key Files

| File | Purpose |
|------|---------|
| `FlowDefinition.cs` | JSON-serializable model — the "source of truth" saved by the designer and executed by the runner |
| `DynamicFlowBuilder.cs` | Core engine: parses definition, validates types, builds `DataFlowGraph` |
| `IFlowDefinitionRepository.cs` | Storage abstraction + in-memory reference implementation |
| `DynamicFlowRunner.cs` | Fire-and-forget executor (mirrors `DemoFlowRunner` pattern) |
| `ExampleDefinitions.cs` | Annotated JSON examples showing valid definitions |

---

## How It Works

```
JSON Definition
      │
      ▼
FlowDefinition (deserialized model)
      │
      ▼
DynamicFlowBuilder.Validate()
  • Checks all blockKeys exist in IBlockTypeRegistry
  • Checks type compatibility for each connection:
    source.OutputType must be assignable to target.InputType
  • Returns list of validation errors (empty = valid)
      │
      ▼ (if valid)
DynamicFlowBuilder.Build(definition, serviceProvider)
  • Creates DataFlowGraphBuilder
  • Calls UseBlock(blockKey) for each block
  • Calls Connect(fromKey, toKey, bufferCapacity) for each connection
  • Returns DataFlowGraph (identical to a hardcoded graph)
      │
      ▼
DataFlowGraph.ExecuteAsync(ctx)
  • Identical execution path to hardcoded flows
  • Events surface in existing visualization UI
  • Errors captured as FlowFailedEvent → shown in UI as "✗ Failed"
```

---

## Limitations of This Prototype

1. **No aliasing**: if you want two instances of the same block type in one flow, you need two
   separate `blockKey`s. The implementation would need a block-cloning/wrapping approach
   (see implementation issue for details).

2. **No config binding**: `blockConfig` in the definition is not yet wired to the blocks.
   Block configuration is a separate implementation phase.

3. **No EF Core repository**: the `InMemoryFlowDefinitionRepository` is for demos only.
   The EF Core implementation needs the schema migration design (see implementation issue).

---

## How to Use

```csharp
// 1. Register blocks normally (no change to existing registration)
services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new DemoProducerBlock("producer", itemCount: 50, delayMs: 200));
    df.AddActorBlock<int, int, TransformActor>("transform");
    df.AddBlock("processor", sp => new DemoProcessorBlock("processor", delayMs: 60));
});

// 2. Register the dynamic runner
services.AddSingleton<DynamicFlowRunner>();

// 3. Define a flow at runtime (from DB, file, API, etc.)
var definition = new FlowDefinition
{
    FlowId = "my-flow",
    Version = 1,
    Name = "My Dynamic Flow",
    Blocks =
    [
        new() { InstanceId = "global:producer", BlockKey = "global:producer" },
        new() { InstanceId = "global:processor", BlockKey = "global:processor" }
    ],
    Connections =
    [
        new() { From = "global:producer", To = "global:processor", BufferCapacity = 100 }
    ]
};

// 4. Validate before running
var errors = runner.Validate(definition);
if (errors.Any()) { /* show errors */ }

// 5. Run — returns invocationId, fire-and-forget
var runner = sp.GetRequiredService<DynamicFlowRunner>();
var invocationId = runner.Run(definition);

// 6. View in existing visualization UI using invocationId
```
