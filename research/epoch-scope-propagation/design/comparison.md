# Approach Comparison: EpochManager vs Stream-Coupled Scopes

**Purpose**: Systematic comparison of current (EpochManager) vs alternative (stream-coupled) approaches

---

## Quick Reference

| Dimension | Current (EpochManager) | Alternative (Stream-Coupled) | Winner |
|-----------|------------------------|------------------------------|--------|
| **Conceptual Model** | Centralized registry | Propagating metadata | Tie |
| **Scope Creation** | Lazy (on first access) | Eager (at source) | Current |
| **Scope Disposal** | Reference counting | Stream disposal | Alternative |
| **Fan-In Support** | ✅ Via subsume semantics | ❌ No clean solution | **Current** |
| **Block Simplicity** | Via IBlockContext | Direct on stream | Alternative |
| **Framework Complexity** | Higher (manager coordination) | Lower (no manager) | Alternative |
| **Lifecycle Clarity** | Explicit events | Implicit with stream | Tie |
| **Concurrent Access** | ✅ Handled by manager | ✅ Handled by scope | Tie |
| **Subsume Semantics** | ✅ Explicit support | ❌ Unclear | **Current** |
| **Service Sharing** | ✅ Guaranteed | ⚠️ Breaks at fan-in | **Current** |

**Overall Winner**: **Current (EpochManager)** - Fan-in support is critical

---

## Detailed Comparison

### 1. Conceptual Model

#### Current Approach

**Model**: Epoch objects are managed by a central registry (EpochManager)

```
┌──────────────────────────────────────┐
│         IEpochManager                │
│  ┌────────────────────────────────┐  │
│  │ Dictionary<Vector, Epoch>      │  │
│  │  {s1=5} → Epoch{scope=A}       │  │
│  │  {s2=3} → Epoch{scope=B}       │  │
│  │  {s1=5,s2=3} → Epoch{scope=A}  │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
         ↑                    ↑
         │                    │
    IEpochStream          IBlockContext
    {vector={s1=5}}       .CurrentEpoch
```

**Characteristics**:
- Single source of truth for active epochs
- Vector → Epoch mapping
- Lifecycle events flow through manager

#### Alternative Approach

**Model**: Scope is embedded in stream, propagates with stream

```
IEpochStream<T>
┌────────────────────────┐
│ Vector: {s1=5}         │
│ Items: IAsyncEnum<T>   │
│ Scope: IServiceScope   │
└────────────────────────┘
         │
         │ propagates
         ▼
IEpochStream<T>
┌────────────────────────┐
│ Vector: {s1=6}         │
│ Items: IAsyncEnum<T>   │
│ Scope: SAME SCOPE      │
└────────────────────────┘
```

**Characteristics**:
- No centralized registry
- Scope travels with stream
- Propagation is explicit in block code

**Analysis**:
- Current: More indirection but clearer lifecycle
- Alternative: More direct but less coordination
- **Winner**: Tie (different trade-offs, both valid conceptually)

---

### 2. Scope Creation Timing

#### Current Approach

**Lazy creation** on first access:

```csharp
// Source creates stream without scope
var stream = new EpochStream<T>(vector, items);

// Later, when block accesses CurrentEpoch:
var epoch = _epochManager.GetOrCreateEpoch(vector);
//          └─ Creates scope here if doesn't exist
```

**Pros**:
- Only create scopes for epochs that are actually used
- Deferred cost until needed
- Can skip scope creation for non-epoch-compatible blocks

**Cons**:
- Requires lookup (dictionary access)
- Indirection via manager
- Not created until first access (delayed)

#### Alternative Approach

**Eager creation** at source:

```csharp
// Source creates stream WITH scope
var scope = _rootProvider.CreateScope();
var stream = new EpochStream<T>(vector, items, scope);
```

**Pros**:
- Scope exists from stream creation
- No lookup needed
- Simpler (no deferred creation logic)

**Cons**:
- May create scopes unnecessarily (if no blocks use them)
- Upfront cost
- Source must have access to root service provider

**Analysis**:
- Current approach is more efficient (only creates when needed)
- Alternative is simpler but potentially wasteful
- **Winner**: **Current** (lazy is better for optional feature)

---

### 3. Scope Disposal

#### Current Approach

**Reference counting** determines disposal:

```csharp
// Each block increments reference count
epoch.AddReference();

// Each block completion decrements
await epoch.TryReleaseAsync();
// └─ Disposes scope when refCount == 0
```

**Pros**:
- Explicit control over lifetime
- Handles complex block topologies
- Can keep scope alive as long as needed

**Cons**:
- Reference counting complexity
- Must track all block participation
- Potential for leaks if counting logic is wrong

#### Alternative Approach

**Stream disposal** triggers scope disposal:

```csharp
await stream.DisposeAsync();
// └─ Automatically disposes scope
```

**Pros**:
- Simple, automatic
- No reference counting needed
- Clear lifetime (scope = stream)

**Cons**:
- Who calls DisposeAsync on stream?
- With propagation, multiple streams share scope
- Back to reference counting problem if scope is shared

**Analysis**:
- Alternative seems simpler but has hidden complexity
- With scope propagation, still need some form of reference tracking
- **Winner**: **Tie** (both need reference tracking ultimately)

---

### 4. Fan-In Support

#### Current Approach

**Explicit subsume semantics**:

```csharp
// BufferNode merges vectors
var merged = vectorA.ElementWiseMax(vectorB);

// Notify manager
_epochManager.NotifyEpochSubsumed(vectorA, merged);
_epochManager.NotifyEpochSubsumed(vectorB, merged);

// Manager extends epoch lifetime
_activeEpochs.TryAdd(merged, epochFromA); // Reuse one scope
```

**Pros**:
- Clean handling of vector merging
- One scope "wins" and covers merged space
- Service sharing guaranteed after merge

**Cons**:
- Requires explicit notification
- Manager must handle subsume logic
- Need strategy for which scope to keep

#### Alternative Approach

**No clear solution**:

```csharp
// BufferNode merges streams
var mergedVector = vectorA.ElementWiseMax(vectorB);
var mergedItems = MergeItems(streamA.Items, streamB.Items);
var mergedScope = ??? // scopeA? scopeB? new? both?

yield return new EpochStream<T>(mergedVector, mergedItems, mergedScope);
```

**Options**:
1. Pick scopeA (arbitrary, loses scopeB services)
2. Pick scopeB (arbitrary, loses scopeA services)
3. Create new scope (loses both, fresh services)
4. Merge providers (complex, resolution order issues)
5. Reintroduce manager (defeats purpose)

**Analysis**:
- Current approach has explicit, working solution
- Alternative has no clean solution for fan-in
- This is a **critical requirement** per existing design
- **Winner**: **Current** (Alternative fails this requirement)

---

### 5. Block Simplicity

#### Current Approach

**Access via IBlockContext**:

```csharp
public async IAsyncEnumerable<IEpochStream<TOut>> ProcessAsync(...)
{
    await foreach (var stream in input)
    {
        // Access epoch from context
        var epoch = _context.CurrentEpoch 
            ?? throw new InvalidOperationException("Epoch not available");
        
        var service = epoch.GetService<MyService>();
        
        // Process...
    }
}
```

**Characteristics**:
- Requires IBlockContext injection
- Null-check needed (or null-forgiving operator)
- Indirection through context

#### Alternative Approach

**Access directly from stream**:

```csharp
public async IAsyncEnumerable<IEpochStream<TOut>> ProcessAsync(...)
{
    await foreach (var stream in input)
    {
        // Access services directly from stream
        var service = stream.GetService<MyService>();
        
        // Process...
    }
}
```

**Characteristics**:
- More direct
- No context needed
- Service resolution is on stream itself

**Analysis**:
- Alternative is simpler for block developers
- Current has one extra indirection level
- **Winner**: **Alternative** (more direct API)

---

### 6. Framework Complexity

#### Current Approach

**Framework responsibilities**:
1. Detect `IEpochCompatibleBlock`
2. Call `IEpochManager.GetOrCreateEpoch(vector)`
3. Populate `IBlockContext.CurrentEpoch`
4. Call `NotifyEpochCompletedAsync` when block completes
5. Call `NotifyEpochSubsumed` on fan-in
6. Coordinate reference counting

**Complexity**: **High** (multiple coordination points)

#### Alternative Approach

**Framework responsibilities**:
1. Provide root `IServiceProvider` to sources
2. Ensure streams are disposed properly
3. (Maybe) coordinate scope sharing if streams share

**Complexity**: **Lower** (less coordination, but disposal still complex)

**Analysis**:
- Alternative reduces framework coordination burden
- Current centralizes complexity (easier to reason about)
- **Winner**: **Alternative** (lower framework complexity)

---

### 7. Lifecycle Clarity

#### Current Approach

**Explicit lifecycle events**:

```
Create:   EpochManager.GetOrCreateEpoch(vector)
Use:      epoch.GetService<T>()
Complete: EpochManager.NotifyEpochCompletedAsync(vector, block)
Dispose:  epoch.TryReleaseAsync() → DisposeAsync()
```

**Characteristics**:
- Clear entry/exit points
- Explicit notifications
- Centralized state machine

#### Alternative Approach

**Implicit lifecycle tied to stream**:

```
Create:  new EpochStream(vector, items, scope)
Use:     stream.GetService<T>()
Dispose: stream.DisposeAsync()
```

**Characteristics**:
- Lifecycle = stream lifecycle
- No explicit events
- Distributed (each stream manages its scope)

**Analysis**:
- Current: More explicit but more coordination
- Alternative: Simpler but less visibility into lifecycle
- **Winner**: **Tie** (different trade-offs)

---

### 8. Concurrent Access to Same Epoch

#### Current Approach

Multiple blocks access same epoch via manager:

```
Block A → EpochManager.GetOrCreateEpoch({s1=5}) → Epoch{scope=A}
                                                        ↓
Block B → EpochManager.GetOrCreateEpoch({s1=5}) → SAME Epoch{scope=A}
```

**Thread Safety**: EpochManager dictionary + Epoch reference counting locks

**Works**: ✅ Yes, guaranteed same instance

#### Alternative Approach

Multiple blocks access same stream:

```
          ┌─ Block A
Stream ──┤
          └─ Block B

Both blocks: stream.GetService<T>()
```

**Thread Safety**: ServiceProvider is thread-safe (standard DI guarantee)

**Works**: ✅ Yes, if they access same stream instance

**But**: With propagation, blocks may have different stream instances that share scope:

```
Stream 1 {scope=A} → Block A processes → Stream 2 {scope=A}
                                              ↓
                                         Block B processes
```

Do Stream 1 and Stream 2 share scope? Only if explicitly propagated.

**Analysis**:
- Both can work
- Current guarantees via dictionary lookup
- Alternative works if propagation is correct
- **Winner**: **Tie** (both can be made thread-safe)

---

### 9. Subsume Semantics

#### Current Approach

**Explicit support via NotifyEpochSubsumed**:

```csharp
void NotifyEpochSubsumed(EpochVector from, EpochVector to)
{
    if (_activeEpochs.TryGetValue(from, out var epoch))
    {
        // Extend epoch to cover new vector
        _activeEpochs.TryAdd(to, epoch);
        epoch.AddReference();
    }
}
```

**Semantics**: Clear extension of epoch lifetime to cover subsumed vector

#### Alternative Approach

**No explicit support**:

How do we represent that epoch A is subsumed into epoch B?

```
Stream A: {vector={s1=5}, scope=scopeA}
Stream B: {vector={s2=3}, scope=scopeB}
Merged:   {vector={s1=5,s2=3}, scope=???}
```

No clear way to extend scopeA to cover merged vector.

**Analysis**:
- Current has explicit, working semantics
- Alternative has no clear representation
- **Winner**: **Current** (Alternative doesn't support this)

---

### 10. Service Sharing Across Blocks

#### Current Approach

**Guaranteed via vector → epoch mapping**:

```
Block A processes vector {s1=5} → Gets Epoch{scope=A} → service1
Block B processes vector {s1=5} → Gets SAME Epoch{scope=A} → SAME service1
```

**Works for all topologies** including fan-in.

#### Alternative Approach

**Works for linear pipelines**:

```
Stream → Block A → Stream (same scope) → Block B
        service1 ←─────────────────────→ SAME service1
```

**Breaks at fan-in**:

```
Stream A (scopeA) → service1A
Stream B (scopeB) → service1B
Merged Stream (??) → service1? (which one?)
```

**Analysis**:
- Current: Always works
- Alternative: Works for linear, breaks for fan-in
- **Winner**: **Current** (Alternative breaks critical requirement)

---

## Use Case Analysis

### Use Case 1: Linear Pipeline (No Fan-In)

**Topology**:
```
Source → Transform → Process → Sink
```

**Current Approach**:
- EpochManager creates epoch on demand
- All blocks access same epoch
- Reference counting handles disposal

**Alternative Approach**:
- Source creates stream with scope
- Transform propagates scope
- Process propagates scope
- Sink disposes stream → disposes scope

**Winner**: **Alternative** (simpler for this case)

---

### Use Case 2: Pipeline with Fan-In

**Topology**:
```
Source A ──┐
           ├─ BufferNode → Process → Sink
Source B ──┘
```

**Current Approach**:
- Two epochs: {A=n} and {B=m}
- BufferNode merges: {A=n, B=m}
- EpochManager extends one epoch to cover merged vector
- Process accesses merged epoch → gets consistent services

**Alternative Approach**:
- Two streams: {A=n, scopeA} and {B=m, scopeB}
- BufferNode merges: {A=n, B=m, scope=???}
- **Problem**: No clean scope merging solution

**Winner**: **Current** (Alternative fails)

---

### Use Case 3: Multiple Concurrent Blocks

**Topology**:
```
Source → Split ──┬─ Block A ─┐
                 └─ Block B ─┘─ Join
```

**Current Approach**:
- Both blocks access same epoch for same vector
- Reference counting keeps epoch alive until both complete
- Join accesses same epoch

**Alternative Approach**:
- Both blocks receive stream with same scope (if split propagates correctly)
- Both access same scope from their streams
- Join must handle two streams with same scope

**Winner**: **Current** (clearer coordination)

---

## Summary Matrix

| Aspect | Current | Alternative | Critical? | Winner |
|--------|---------|-------------|-----------|--------|
| Fan-In Support | ✅ | ❌ | **YES** | Current |
| Subsume Semantics | ✅ | ❌ | **YES** | Current |
| Service Sharing | ✅ | ⚠️ | **YES** | Current |
| Block Simplicity | ⚠️ | ✅ | No | Alternative |
| Framework Simplicity | ❌ | ✅ | No | Alternative |
| Eager Creation | ❌ | ✅ | No | Alternative |
| Reference Counting | Required | May be required | N/A | Tie |

---

## Recommendation

### Current Approach (EpochManager) Should Be Retained

**Reasons**:

1. **Fan-In is Critical**: The existing design explicitly requires fan-in support with correct service sharing semantics. Alternative approach has no clean solution.

2. **Subsume Semantics are Critical**: Epoch vector subsume operations are fundamental to the epoch model. Alternative doesn't support this clearly.

3. **Service Sharing Must Work Everywhere**: Guaranteeing same service instances for same epoch across all blocks is the core value proposition. Alternative breaks this at fan-in points.

4. **Framework Complexity is Acceptable**: The additional framework complexity is worth it to ensure correctness and support all topologies.

5. **Block Complexity is Minimal**: The extra indirection (`context.CurrentEpoch.GetService`) is minimal compared to manual epoch tracking that alternative would require at fan-in points.

### Alternative Approach is NOT Viable as General Solution

**Why it fails**:
- No clean fan-in scope merging
- Subsume semantics are unclear
- Service sharing breaks at fan-in
- Would require fallback to manual management for complex cases

**Where it could work**:
- Linear pipelines only (no fan-in)
- Simple topologies
- But limiting the design to simple topologies defeats the purpose

### Hybrid Not Recommended

Could we use alternative for linear pipelines and current for fan-in?
- **No**: Introduces two different models for same concept
- Confusing for developers
- Adds complexity rather than reducing it

---

## Conclusion

**The alternative approach of coupling DI scope to IEpochStream is NOT viable** because:

1. ❌ **Fails fan-in support** - no clean scope merging solution
2. ❌ **Unclear subsume semantics** - can't extend scope to cover subsumed vectors
3. ❌ **Breaks service sharing** - different scopes at merge points
4. ✅ **Works for linear pipelines** - but this is insufficient

**The current EpochManager approach should be retained** because:

1. ✅ **Supports all topologies** - including fan-in
2. ✅ **Clear subsume semantics** - explicit NotifyEpochSubsumed
3. ✅ **Guarantees service sharing** - vector → epoch mapping
4. ✅ **Handles complex cases** - reference counting, lifecycle events

**No design revision needed** - proceed with current approach in issue #415.

**However**: Document this exploration as a considered alternative in the design docs to show due diligence in evaluating options.
