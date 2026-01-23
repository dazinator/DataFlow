# Design Options: Moving IServiceProvider to Build()

**Date**: 2026-01-23  
**Purpose**: Evaluate different approaches for removing IServiceProvider from constructor

---

## Context

**Goal**: Remove `IServiceProvider` from `DataFlowGraphBuilder` constructor and pass it to `Build()` method instead.

**Challenges**:
1. `UseBlock()` needs service provider during graph building (pre-build)
2. `ConfigureEpochs()` currently creates coordinator during configuration (pre-build)

---

## Option A: Defer All Resolution to Build() ⭐ **RECOMMENDED**

### Description

Store the **configuration** rather than the **resolved objects** during graph building. Resolve everything in `Build()` when service provider is available.

### Changes Required

#### 1. Store Block Names Instead of Blocks

**Current `UseBlock()`**:
```csharp
public DataFlowGraphBuilder UseBlock(string name)
{
    var block = _registry.GetBlock(_serviceProvider, key);  // Resolve immediately
    _blocks.Add(block);
    return this;
}
```

**Proposed**:
```csharp
// Store pending block resolutions
private readonly List<string> _pendingBlockNames = new();

public DataFlowGraphBuilder UseBlock(string name)
{
    // Just store the name for later resolution
    _pendingBlockNames.Add(name);
    return this;
}
```

#### 2. Store Epoch Configuration Instead of Coordinator

**Current `ConfigureEpochs()`**:
```csharp
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    var coordinator = coordinatorFactory(config.CheckpointStrategy);
    builder.SetEpochCoordinator(coordinator);  // Store coordinator
    // ...
}
```

**Proposed**:
```csharp
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    // Store configuration + factory for later execution
    builder.SetEpochConfiguration(config, coordinatorFactory);
    // ...
}
```

#### 3. Update Build() Method

**Current**:
```csharp
public DataFlowGraph Build()
{
    var graph = new DataFlowGraph(_name, _graphId, _logger);
    
    foreach (var block in _blocks)  // Blocks already resolved
    {
        graph.AddBlock(block);
    }
    
    if (_epochCoordinator != null)  // Coordinator already created
    {
        graph.SetEpochCoordinator(_epochCoordinator);
    }
    
    return graph;
}
```

**Proposed**:
```csharp
public DataFlowGraph Build(IServiceProvider serviceProvider, IBlockTypeRegistry registry)
{
    ArgumentNullException.ThrowIfNull(serviceProvider);
    ArgumentNullException.ThrowIfNull(registry);
    
    var graph = new DataFlowGraph(_name, _graphId, _logger);
    
    // Resolve blocks added via AddBlock() (already resolved)
    foreach (var block in _blocks)
    {
        graph.AddBlock(block);
    }
    
    // Resolve blocks added via UseBlock() (deferred resolution)
    foreach (var blockName in _pendingBlockNames)
    {
        var key = ResolveBlockKey(blockName);
        var block = registry.GetBlock(serviceProvider, key);
        graph.AddBlock(block);
        _blocksByName[key] = block;  // Track for Connect()
    }
    
    // Create and set epoch coordinator if configured
    if (_epochConfig != null)
    {
        var factory = _epochCoordinatorFactory ?? CreateDefaultFactory(serviceProvider);
        var coordinator = factory(_epochConfig.CheckpointStrategy);
        graph.SetEpochCoordinator(coordinator);
        
        // Set epoch nodes (already created, don't need service provider)
        if (_epochSource != null)
        {
            graph.SetEpochSource(_epochSource);
            foreach (var processor in _epochProcessors)
            {
                graph.AddEpochProcessor(processor);
            }
        }
    }
    
    // Add edges (already configured)
    foreach (var edge in _edges)
    {
        graph.AddEdge(edge);
    }
    
    return graph;
}
```

### Constructor Changes

**Current**:
```csharp
public DataFlowGraphBuilder(
    string name, 
    IServiceProvider serviceProvider,  // ← Remove this
    IBlockTypeRegistry registry,       // ← Remove this
    string? namespacePrefix = null,
    ILogger<DataFlowGraph>? logger = null)
```

**Proposed**:
```csharp
public DataFlowGraphBuilder(
    string name,
    string? namespacePrefix = null,
    ILogger<DataFlowGraph>? logger = null)
{
    _name = name ?? throw new ArgumentNullException(nameof(name));
    _namespace = namespacePrefix ?? "global";
    _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    // No service provider or registry stored
}
```

### Pros ✅

1. **Clean separation of concerns**: Configuration happens during building, resolution during `Build()`
2. **No constructor dependency on IServiceProvider**: Service provider only needed at build time
3. **Removes obsolete constructor naturally**: Only one constructor needed
4. **Simplifies nullability**: No nullable service provider field
5. **Clear API**: Dependencies required at each phase are explicit
6. **Flexible**: Can build same configuration with different service providers

### Cons ❌

1. **Breaking change**: `Build()` signature changes from no parameters to requiring parameters
2. **Slightly more complex Build() method**: More logic in `Build()`
3. **Connection ordering matters**: Blocks referenced in `Connect()` must be added first
   - **Mitigation**: Already the case - `Connect()` uses `_blocksByName` which is populated during building

### Migration Path

**Before**:
```csharp
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("my-graph", serviceProvider, registry);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
var graph = builder.Build();
```

**After**:
```csharp
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("my-graph");
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
var graph = builder.Build(serviceProvider, registry);
```

**Impact**: One-line change per graph building location.

---

## Option B: Keep Service Provider, Remove from Build() ❌ **NOT RECOMMENDED**

### Description

Keep `IServiceProvider` in constructor, but remove the obsolete constructor. Don't change `Build()`.

### Changes Required

1. Remove obsolete constructor
2. Remove nullable service provider (`_serviceProvider?` → `_serviceProvider`)
3. Remove `GetServiceProvider()` nullability check

### Pros ✅

1. **Minimal changes**: Just removing obsolete code
2. **No breaking changes**: Existing API unchanged
3. **Simple migration**: Just remove obsolete constructor usage

### Cons ❌

1. **Doesn't solve the stated problem**: Issue description wants service provider in `Build()`, not constructor
2. **Doesn't improve architecture**: Service provider still a constructor dependency
3. **Misses opportunity for improvement**: No architectural benefits

**Verdict**: This doesn't address the research objective.

---

## Option C: Hybrid Approach - Two Constructors ❌ **NOT RECOMMENDED**

### Description

Keep service provider in constructor for `UseBlock()` support, but also require it in `Build()` for epoch configuration.

### Changes Required

1. Constructor with service provider (for `UseBlock()`)
2. Constructor without service provider (for `AddBlock()` only)
3. `Build()` takes service provider for epoch coordinator creation

### Pros ✅

1. **Supports both use cases**: DI-based and direct block adding
2. **Partial improvement**: Epoch coordinator deferred to build time

### Cons ❌

1. **Confusing API**: Service provider in constructor AND `Build()`?
2. **Redundant**: Same service provider passed twice
3. **Doesn't solve the core problem**: Service provider still needed at construction
4. **More complex**: Two paths to maintain

**Verdict**: Overcomplicated and confusing.

---

## Option D: Remove UseBlock() Support ❌ **NOT RECOMMENDED**

### Description

Remove `UseBlock()` method entirely, only support `AddBlock()`. This removes the need for service provider at construction time.

### Changes Required

1. Remove `UseBlock()` method
2. Remove `IServiceProvider` and `IBlockTypeRegistry` from constructor
3. Users must manually resolve blocks before adding

### Pros ✅

1. **Simplest builder**: No service provider or registry needed
2. **Clear separation**: All DI resolution happens outside builder

### Cons ❌

1. **MAJOR breaking change**: Removes fundamental API
2. **Terrible DX**: Users must manually resolve all blocks
3. **Breaks many tests and examples**: `UseBlock()` is widely used
4. **Goes against modern .NET patterns**: DI integration is expected

**Verdict**: Unacceptable - `UseBlock()` is a core API.

---

## Comparison Matrix

| Aspect | Option A (Defer) | Option B (Keep) | Option C (Hybrid) | Option D (Remove) |
|--------|-----------------|-----------------|-------------------|-------------------|
| **Solves stated problem** | ✅ Yes | ❌ No | ⚠️ Partial | ✅ Yes |
| **API breaking changes** | ⚠️ `Build()` signature | ✅ None | ⚠️ Two changes | ❌ Major |
| **Architectural clarity** | ✅ Excellent | ❌ Same | ⚠️ Confusing | ✅ Good |
| **Migration difficulty** | ⚠️ Medium | ✅ Easy | ❌ Hard | ❌ Very hard |
| **Maintains `UseBlock()`** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Code complexity** | ⚠️ Medium | ✅ Low | ❌ High | ✅ Low |
| **Flexibility** | ✅ High | ❌ Low | ⚠️ Medium | ✅ High |
| **Aligns with research goal** | ✅ Yes | ❌ No | ⚠️ Partial | ❌ No |

---

## Recommended Approach: Option A (Defer Resolution)

### Rationale

**Option A** best addresses the research objective while maintaining a clean, understandable architecture:

1. ✅ **Directly solves the stated problem**: Moves `IServiceProvider` from constructor to `Build()`
2. ✅ **Improves architecture**: Clear separation between configuration and resolution
3. ✅ **Maintains all functionality**: `UseBlock()` still works, just deferred
4. ✅ **Reasonable breaking change**: One parameter added to `Build()` - easy to migrate
5. ✅ **Future-proof**: Can build same configuration with different service providers
6. ✅ **Removes complexity**: No nullable service provider, no obsolete constructor

### Trade-offs Accepted

1. ⚠️ **Breaking change**: `Build()` signature changes
   - **Mitigation**: Clear migration path, compile-time error, easy to fix
   
2. ⚠️ **Slightly more complex Build() method**: More logic in `Build()`
   - **Mitigation**: Well-organized, easy to understand, follows clear pattern

### Implementation Strategy

**Phase 1: Core Changes**
1. Remove `IServiceProvider` and `IBlockTypeRegistry` from constructor
2. Remove obsolete constructor
3. Change `UseBlock()` to store names instead of resolving
4. Change `ConfigureEpochs()` to store configuration instead of coordinator
5. Update `Build()` to take service provider and registry parameters
6. Update `Build()` to resolve blocks and create coordinator

**Phase 2: Test Updates**
1. Update all test code to pass service provider to `Build()`
2. Remove obsolete constructor usage
3. Validate all tests pass

**Phase 3: Documentation**
1. Update API documentation
2. Create migration guide
3. Update examples and guides

---

## Alternative Consideration: Incremental Approach

If the breaking change is a concern, we could consider a **two-phase migration**:

### Phase 1: Add New Constructor + Build() Overload (Non-Breaking)

```csharp
// New constructor (no service provider)
public DataFlowGraphBuilder(string name, ...)

// New Build() overload (with service provider)
public DataFlowGraph Build(IServiceProvider serviceProvider, IBlockTypeRegistry registry)

// Keep old constructor (mark obsolete)
[Obsolete("Use new constructor without IServiceProvider...")]
public DataFlowGraphBuilder(string name, IServiceProvider serviceProvider, ...)

// Keep old Build() (mark obsolete)
[Obsolete("Use Build(IServiceProvider, IBlockTypeRegistry) instead")]
public DataFlowGraph Build()
```

### Phase 2: Remove Obsolete APIs (Breaking)

After migration period, remove obsolete constructor and `Build()` overload.

**Trade-off**: More complexity during migration period, but gentler breaking change.

---

## Next Steps

1. ✅ Complete design options analysis
2. ⬜ Get feedback on recommended approach (Option A)
3. ⬜ Prototype Option A implementation
4. ⬜ Validate with tests
5. ⬜ Document findings
