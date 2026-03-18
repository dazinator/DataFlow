# Implementation Handover: Graph Builder API Improvements

**Research Issue**: #143  
**Created**: 2026-03-18

---

## Research Summary

A review of the graph building API — prompted by a real-world application registration — identified two improvements:

1. **Fan-in is correct but benefits from a clearer API** (`ConnectFanIn`). The existing 3-call pattern works at runtime (the engine merges multiple incoming edges) but is not self-documenting.

2. **`UseBlock()` is redundant alongside `Connect()`**. Every block that appears in a `Connect()` call must first be registered via `UseBlock()`. Modifying `Connect()` (and `ConnectCompeting()`) to auto-register named blocks eliminates this boilerplate entirely.

---

## Recommended Approach

Two small, backward-compatible changes to `DataFlowGraphBuilder`:

### Change 1: Auto-register blocks from Connect calls

When `Connect(string, string)` or `ConnectCompeting(string, IEnumerable<string>)` receives block names, automatically add those names to the pending DI resolution list (same effect as `UseBlock()`). Blocks already in `_blocksByName` (e.g., added via `AddBlock()` or `AddEpochBuffer()`) must not be double-registered.

**Helper method** (internal):
```csharp
private void EnsurePendingBlock(string name)
{
    // Only queue for DI resolution if not already directly registered
    var key = ResolveBlockKey(name);
    if (!_blocksByName.ContainsKey(name) &&
        !_blocksByName.ContainsKey(key) &&
        !_pendingBlockNames.Contains(name) &&
        !_pendingBlockNames.Contains(key))
    {
        _pendingBlockNames.Add(name);
    }
}
```

> Note: both `name` and its resolved `key` are checked in `_pendingBlockNames` to prevent duplicate entries when a block is referenced by both its short name and its fully-qualified key.

Call `EnsurePendingBlock` for every source and target name in `Connect(string,string)` and in `ConnectCompeting(string, IEnumerable<string>)`.

### Change 2: Add ConnectFanIn

```csharp
/// <summary>
/// Connect multiple source blocks to a single target block (fan-in).
/// Each source produces items that flow into the shared target.
/// This is equivalent to calling Connect(source, target) for each source,
/// but makes the fan-in intent explicit and readable.
/// </summary>
/// <param name="sourceNames">Names of the source blocks</param>
/// <param name="targetName">Name of the target (fan-in) block</param>
/// <param name="bufferCapacity">Buffer capacity per edge (default: 100)</param>
public DataFlowGraphBuilder ConnectFanIn(
    IEnumerable<string> sourceNames,
    string targetName,
    int bufferCapacity = 100)
{
    ArgumentNullException.ThrowIfNull(sourceNames);
    if (string.IsNullOrWhiteSpace(targetName))
        throw new ArgumentException("Target block name cannot be null or whitespace", nameof(targetName));

    var sourceList = sourceNames.ToList();
    if (sourceList.Count == 0)
        throw new ArgumentException("At least one source block name must be provided", nameof(sourceNames));

    EnsurePendingBlock(targetName);

    foreach (var sourceName in sourceList)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source block names cannot be null or whitespace");

        EnsurePendingBlock(sourceName);
        _pendingConnections.Add((sourceName, targetName, bufferCapacity));
    }

    return this;
}
```

---

## Implementation Requirements

### Core Requirements

- [ ] Add `EnsurePendingBlock(string name)` private helper to `DataFlowGraphBuilder`
- [ ] Call `EnsurePendingBlock` for source and target in `Connect(string, string)`
- [ ] Call `EnsurePendingBlock` for all names in `ConnectCompeting(string, IEnumerable<string>)`
- [ ] Add `ConnectFanIn(IEnumerable<string>, string, int)` public method
- [ ] Unit test: graph with only `Connect()` calls (no `UseBlock()`) resolves all blocks
- [ ] Unit test: `UseBlock()` + `Connect()` still works (no regression)
- [ ] Unit test: `ConnectFanIn()` produces correct merged input stream

### Non-Functional Requirements

- [ ] **Backward compatible**: All existing code using `UseBlock()` continues to work
- [ ] **No silent duplicates**: `EnsurePendingBlock` must not double-add names already in `_blocksByName`

---

## Edge Cases to Handle

- Block name already in `_blocksByName` (added via `AddBlock()` or `AddEpochBuffer()`): skip DI resolution, it's already registered
- Same block name passed to both `UseBlock()` and `Connect()`: deduplicate in `_pendingBlockNames`
- `ConnectFanIn()` with empty source list: throw `ArgumentException`

---

## Design Reference

See research README: `/research/graph-building-api-sanity/README.md`

---

## Trade-offs & Constraints

**Accepted Trade-offs**:
- `UseBlock()` becomes optional for connected blocks; its documentation should be updated to reflect this
- Auto-registration makes the graph's block set implicit (discovered from topology); this is acceptable as connections already define the topology

**Constraints**:
- Changes are POC-only (not production `src/`)
- Must not break any existing tests

---

## Testing Guidance

**Test Scenarios**:

1. **All-Connect graph** (no UseBlock): define a linear A→B→C graph using only `Connect()` calls and verify all blocks are resolved and wired
2. **Mixed graph** (UseBlock + Connect): existing style continues to work, no duplicate resolution
3. **ConnectFanIn**: 3 sources → 1 target, verify all 3 items merge in the target's input stream
4. **EnsurePendingBlock with AddBlock**: a block added via `AddBlock()` referenced in a subsequent `Connect()` call must NOT be re-resolved from DI

---

## Multi-Phase Assessment

**Recommendation**: Single-Phase — both changes are small and cohesive.

---

## Open Questions

None — approach is well-understood and straightforward.
