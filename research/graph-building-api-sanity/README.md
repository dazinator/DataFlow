# Research: Graph Building API Sanity Check

**Research Issue**: #143  
**Created**: 2026-03-18  
**Status**: Complete

---

## Research Objective

Review the following real application registration code and assess:
1. Whether the fan-in pattern (3 separate `.Connect()` calls) is correct
2. Whether `UseBlock()` is necessary or redundant when used together with `.Connect()`

---

## Question 1: Is the Fan-In Correct?

### Code Under Review

```csharp
// Fan-in: all three workers → TMS status batcher.
.Connect(ErpPoster1, TmsBatch)
.Connect(ErpPoster2, TmsBatch)
.Connect(ErpPoster3, TmsBatch)
```

### Findings: ✅ This is Correct

The three separate `.Connect()` calls each connect a **different source** to the same **target**. The framework's single-connection-per-source rule (`ValidateSourceNotAlreadyConnected`) only prevents the same source from being connected twice — it places no restriction on how many sources can feed the same target.

At the `DataFlowGraph` level, edges are tracked in two dictionaries:

```csharp
private readonly Dictionary<IBlock, List<Edge>> _outgoingEdges = new();
private readonly Dictionary<IBlock, List<Edge>> _incomingEdges = new();
```

The `_incomingEdges[TmsBatch]` list will contain three entries — one per ERP poster. When the graph executes, `GetBlockInputStream` calls `MergeInputStreams`:

```csharp
private object MergeInputStreams(List<object> inputs, Type inputItemType)
{
    if (inputs.Count == 1)
        return inputs[0];
    else
        // Multiple inputs - merge them
        return ReflectionHelper.MergeTypedStreams(inputs, inputItemType);
}
```

**Conclusion**: The 3-call fan-in pattern works correctly at runtime. The framework merges all incoming streams into a single input for `TmsBatch`.

### API Improvement Opportunity

While correct, the pattern is not immediately legible. A reader seeing three consecutive `Connect(..., TmsBatch)` calls must infer that this is fan-in. A dedicated API would make the intent explicit:

```csharp
// Current (works, but intent not obvious)
.Connect(ErpPoster1, TmsBatch)
.Connect(ErpPoster2, TmsBatch)
.Connect(ErpPoster3, TmsBatch)

// Proposed (intent immediately clear)
.ConnectFanIn(new[] { ErpPoster1, ErpPoster2, ErpPoster3 }, TmsBatch)
```

Adding `ConnectFanIn(IEnumerable<string> sourceNames, string targetName)` is a purely additive change — it creates the same edges under the hood but signals to the reader (and any future graph validators) that fan-in is intentional.

---

## Question 2: UseBlock / Connect Redundancy

### Code Under Review

```csharp
g.UseBlock(Source)
 .UseBlock(Batch)
 .UseBlock(RateLimit)
 .UseBlock(Routing)
 .AddEpochBuffer<SystemJournalProcessingItems>(ErpBuffer, capacity: 100)
 .UseBlock(ErpPoster1)
 .UseBlock(ErpPoster2)
 .UseBlock(ErpPoster3)
 .UseBlock(TmsBatch)
 .UseBlock(TmsSender)
 // Linear pipeline.
 .Connect(Source, Batch)
 .Connect(Batch, RateLimit)
 // ... etc.
```

### Why UseBlock Is Currently Required

`UseBlock(name)` adds a name to `_pendingBlockNames`. During `Build()`:

```csharp
foreach (var blockName in _pendingBlockNames)
{
    var key = ResolveBlockKey(blockName);
    var block = registry.GetBlock(serviceProvider, key);
    graph.AddBlock(block);
    _blocksByName[key] = block;    // <-- makes it findable by Connect()
}
```

`Connect(string, string)` calls `FindBlockByName()` which only searches `_blocksByName`. If a block's name was never put into `_blocksByName` (i.e., `UseBlock()` was never called), `Connect()` will throw a `ArgumentException`.

### The Redundancy Problem

Every block in the example is:
1. Declared via `UseBlock("name")` — to register it in `_pendingBlockNames`
2. Referenced again in a `Connect("name", ...)` call

The two-step pattern is boilerplate. The developer is forced to list every block **twice** — once to include it in the graph, and once to wire it.

### Investigation: Can Connect Auto-Register?

Yes. If `Connect(string, string)` internally calls a helper that also tracks the source/target names for deferred DI resolution (exactly like `UseBlock()` does), the explicit `UseBlock()` calls become unnecessary for any block referenced in a connection.

**Example of how this works:**

When the builder collects pending connections:
```csharp
_pendingConnections.Add((sourceName, targetName, bufferCapacity));
```

…it could simultaneously also register both names for resolution:
```csharp
EnsurePendingBlock(sourceName);
EnsurePendingBlock(targetName);
```

where `EnsurePendingBlock` only adds to `_pendingBlockNames` if not already present and not already in `_blocksByName` (to avoid double-resolving blocks added via `AddBlock()`/`AddEpochBuffer()`).

The same treatment applies to `ConnectCompeting(string, IEnumerable<string>)`.

### What Happens to UseBlock?

`UseBlock()` retains value for the edge case of terminal or source blocks that are **not** referenced in any connection. In practice this is rare (most graphs have all blocks wired), but the method should be kept for correctness.

### Recommended API After Improvement

With auto-registration in `Connect()` and a new `ConnectFanIn()`, the sample graph registration collapses to:

```csharp
df.AddGraph(GraphName, g =>
{
    // Non-DI block still needs explicit registration
    g.AddEpochBuffer<SystemJournalProcessingItems>(ErpBuffer, capacity: 100)
     // Linear pipeline — blocks auto-resolved from DI via Connect()
     .Connect(Source, Batch)
     .Connect(Batch, RateLimit)
     .Connect(RateLimit, Routing)
     .Connect(Routing, ErpBuffer)
     // Competing consumers — also auto-resolves all named blocks
     .ConnectCompeting(ErpBuffer, new[] { ErpPoster1, ErpPoster2, ErpPoster3 })
     // Fan-in — new method, explicit intent
     .ConnectFanIn(new[] { ErpPoster1, ErpPoster2, ErpPoster3 }, TmsBatch)
     .Connect(TmsBatch, TmsSender);
});
```

All 9 `UseBlock()` calls are gone. The topology is expressed purely through connections. The `AddEpochBuffer` call remains because it instantiates a concrete block (not a DI-resolved one).

---

## Recommended Approach

Implement two improvements to `DataFlowGraphBuilder`:

### Improvement A — Auto-register blocks referenced in Connect calls

**Change**: In `Connect(string, string)`, `ConnectCompeting(string, IEnumerable<string>)`, and the new `ConnectFanIn()`, automatically add block names to the pending resolution list (equivalent to an implicit `UseBlock()`).

**Backward compatible**: Yes — existing code that calls `UseBlock()` explicitly will simply result in the name being recorded twice, which is handled by the deduplication guard.

### Improvement B — Add ConnectFanIn helper

**Change**: Add `DataFlowGraphBuilder.ConnectFanIn(IEnumerable<string> sourceNames, string targetName, int bufferCapacity = 100)`.

**Implementation**: Creates one `Connect(source, target)` edge per source, exactly as the three-call pattern does today, but under a readable, intent-expressing name.

**Backward compatible**: Yes — purely additive.

---

## Success Metrics Results

| Metric | Result |
|--------|--------|
| Fan-in correctness validated | ✅ Works correctly via multiple incoming edges |
| UseBlock redundancy confirmed | ✅ Confirmed — auto-registration is feasible |
| API proposal produced | ✅ ConnectFanIn + auto-registration |
| Backward compatibility assessed | ✅ Both changes are additive/backward-compatible |

---

## Implementation Guidance

See handover document: `/research/graph-building-api-sanity/handover/implementation-handover.md`

### Key Files to Modify

| File | Change |
|------|--------|
| `poc/DataFlow/Builder/DataFlowGraphBuilder.cs` | Auto-register in `Connect(string,string)` and `ConnectCompeting(string, IEnumerable<string>)` |
| `poc/DataFlow/Builder/DataFlowGraphBuilder.cs` | Add `ConnectFanIn(IEnumerable<string>, string, int)` method |

### Test Scenarios

1. Graph defined using only `Connect()` calls (no `UseBlock()`) resolves all blocks correctly
2. Graph defined with both `UseBlock()` and `Connect()` continues to work (no duplicate resolution)
3. `ConnectFanIn()` produces correct edges and merged input at runtime

---

## References

- Research folder: `/research/graph-building-api-sanity/`
- Implementation issue: see handover
- Source reviewed: `poc/DataFlow/Builder/DataFlowGraphBuilder.cs`
- Graph execution reviewed: `poc/DataFlow/Core/DataFlowGraph.cs`
