# Epoch-Scoped Services Design

**Status**: Draft  
**Created**: 2025-11-13  
**Related Issue**: uniun-technology/lib-dataflow#[issue-number]  
**Related Work**: Builds on Phase 5 EF Core Anchoring Demo

---

## Table of Contents

1. [Objective](#objective)
2. [Background](#background)
3. [Problem Statement](#problem-statement)
4. [Constraints](#constraints)
5. [Proposed Architecture](#proposed-architecture)
6. [Fan-In Scenarios](#fan-in-scenarios)
7. [API Refinements](#api-refinements)
8. [Impacted Components](#impacted-components)
9. [Implementation Plan](#implementation-plan)
10. [Alternative Approaches](#alternative-approaches)
11. [Success Criteria](#success-criteria)
12. [Related Documentation](#related-documentation)

---

## Objective

Create a higher-level abstraction for epochs that allows blocks to:
1. Access an **epoch object** that represents the current epoch's lifetime
2. Resolve **services scoped to the epoch** via dependency injection
3. Share the **same service instances** across concurrent blocks when they're processing the same epoch
4. Automatically handle **epoch vector subsume operations** to extend epoch lifetimes appropriately

This abstraction will:
- Hide low-level epoch tracking concerns from block developers
- Provide a clean DI scope per epoch
- Enable future support for epoch-scoped EF Core transactions (see #125)
- Simplify block development for epoch-compatible blocks

---

## Background

### Current State

The **EpochAnchoringDemo** POC demonstrates several important patterns:

1. **Epoch Lifecycle Events** - `IEpochLifecycleObserver` and `IEpochLifecycleParticipant` interfaces allow components to react to epoch creation, completion, and global alignment

2. **Per-Epoch Resources** - The `WriteContextBlock` demonstrates creating a `DbContext` scoped to each epoch:
   ```csharp
   await using var dbContext = new DemoDbContext(_dbOptions);
   await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
   // Process all items in epoch
   await dbContext.SaveChangesAsync(ct);
   await transaction.CommitAsync(ct);
   ```

3. **Manual Resource Management** - Currently, each block must:
   - Manually create epoch-scoped resources
   - Manually track epoch boundaries
   - Manually handle cleanup
   - Manually coordinate with epoch lifecycle events

4. **EF Core Tracker Example** - The EF Core tracker block has a service that tracks DbContext instances across epoch vector subsume operations, acting as an epoch-scoped cache. This is complex, low-level code that ideally should be handled by the library infrastructure.

### Limitations of Current Approach

1. **Block-Level Concern** - Each block must understand epoch vector subsume operations to properly manage epoch-scoped resources

2. **No DI Integration** - Epoch-scoped services cannot be registered in the DI container and automatically resolved

3. **Duplication** - Each block reimplements epoch tracking and resource scoping logic

4. **Coordination Challenges** - When multiple concurrent blocks process the same epoch, they cannot easily share epoch-scoped service instances

5. **Complexity** - Understanding epoch vector operations, subsume logic, and proper cleanup is a barrier to block development

---

## Problem Statement

**As a DataFlow library user**, I want to create services that are scoped per epoch so that:
- Multiple concurrent blocks processing the same epoch can resolve the same service instance
- The service lifetime automatically tracks the epoch lifetime (including epoch vector subsume operations)
- I don't need to understand low-level epoch tracking mechanics

**Example Scenario:**

```csharp
// I want to register a service in DI as epoch-scoped
services.AddEpochScoped<MyEpochTrackedService>();

// Then in my blocks, I want to access the same instance when they're in the same epoch
public class BlockA : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, DataRecord item)
    {
        // Gets the epoch-scoped instance
        var service = epoch.GetService<MyEpochTrackedService>();
        await service.TrackAsync(item);
    }
}

public class BlockB : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, DataRecord item)
    {
        // Gets THE SAME instance as BlockA if in same epoch
        var service = epoch.GetService<MyEpochTrackedService>();
        var tracked = await service.GetTrackedAsync();
    }
}
```

**Requirements:**
1. Blocks at different positions in the flow (start, middle, end) must access the same epoch-scoped services
2. Must work correctly even when blocks process at different rates
3. Must handle epoch vector subsume operations correctly (extending epoch lifetimes)
4. No race conditions in service resolution or lifecycle management

---

## Constraints

### Functional Constraints

1. **Epoch Lifetime Semantics**
   - Epoch objects must track with epoch vector operations
   - Subsume operations must extend the epoch's lifetime appropriately
   - DI scope must remain valid until the epoch is fully retired

2. **Compatibility**
   - Must integrate with existing `EpochVector` and `IEpochLifecycleParticipant` patterns
   - Must not break existing block implementations
   - Should simplify the EF Core tracker block example

3. **Concurrency Safety**
   - Multiple blocks may access the same epoch concurrently
   - Service resolution must be thread-safe
   - Scope disposal must handle concurrent access

4. **Foundation for #125**
   - Must support future epoch-scoped EF Core transactions
   - Must allow services to participate in epoch completion events
   - Must enable proper transaction commit at epoch boundaries

### Non-Functional Constraints

1. **Performance**
   - Minimal overhead over current manual approach
   - Efficient service resolution (likely dictionary lookup)
   - Minimal memory overhead per epoch

2. **Developer Experience**
   - Simple, intuitive API for block developers
   - Clear separation: library handles complexity, blocks use simple abstractions
   - Good error messages for common mistakes

---

## Proposed Architecture

### Core Concepts

#### 1. IEpoch Interface

The `IEpoch` interface represents a single epoch instance with its own DI scope:

```csharp
/// <summary>
/// Represents an epoch instance with its own dependency injection scope.
/// Provides access to services scoped to this epoch's lifetime.
/// </summary>
public interface IEpoch : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this epoch.
    /// </summary>
    EpochVector Vector { get; }
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// Multiple blocks accessing the same epoch will get the same instance.
    /// </summary>
    T GetService<T>() where T : notnull;
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope, returning null if not registered.
    /// </summary>
    T? GetServiceOrNull<T>() where T : class;
    
    /// <summary>
    /// Gets the service provider for this epoch's scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
```

#### 2. IEpochManager Interface

The `IEpochManager` manages the lifecycle of epoch objects:

```csharp
/// <summary>
/// Manages the lifecycle of epoch objects and their DI scopes.
/// Handles epoch vector subsume operations and tracks which epochs are active.
/// </summary>
public interface IEpochManager : IAsyncDisposable
{
    /// <summary>
    /// Gets or creates an epoch object for the given epoch vector.
    /// If the epoch already exists, returns the existing instance.
    /// </summary>
    IEpoch GetOrCreateEpoch(EpochVector vector);
    
    /// <summary>
    /// Called when a block completes processing an epoch.
    /// Decrements the reference count and disposes the epoch if no longer needed.
    /// </summary>
    ValueTask NotifyEpochCompletedAsync(
        EpochVector vector, 
        IBlockContext block, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Called when epoch vectors are subsumed (e.g., vector A subsumed into vector B).
    /// Extends the lifetime of the epoch object to cover the subsumed vector space.
    /// </summary>
    void NotifyEpochSubsumed(EpochVector from, EpochVector to);
}
```

#### 3. Epoch Implementation

Internal implementation that manages the DI scope and lifecycle:

```csharp
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private int _referenceCount;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public EpochVector Vector { get; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;
    
    internal Epoch(EpochVector vector, IServiceScope scope)
    {
        Vector = vector;
        _scope = scope;
        _referenceCount = 1; // Start with 1 reference
    }
    
    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
    
    public T? GetServiceOrNull<T>() where T : class
    {
        return ServiceProvider.GetService<T>();
    }
    
    internal async ValueTask<bool> TryReleaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                await DisposeAsync();
                return true; // Epoch was disposed
            }
            return false; // Still has references
        }
        finally
        {
            _lock.Release();
        }
    }
    
    internal void AddReference()
    {
        Interlocked.Increment(ref _referenceCount);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        _lock.Dispose();
    }
}
```

#### 4. EpochManager Implementation

Manages active epochs and handles subsume operations:

```csharp
internal sealed class EpochManager : IEpochManager
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ConcurrentDictionary<EpochVector, Epoch> _activeEpochs = new();
    private readonly ConcurrentDictionary<string, int> _blockRefCounts = new();
    
    public EpochManager(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider;
    }
    
    public IEpoch GetOrCreateEpoch(EpochVector vector)
    {
        return _activeEpochs.GetOrAdd(vector, v =>
        {
            var scope = _rootServiceProvider.CreateScope();
            return new Epoch(v, scope);
        });
    }
    
    public async ValueTask NotifyEpochCompletedAsync(
        EpochVector vector, 
        IBlockContext block, 
        CancellationToken cancellationToken)
    {
        if (_activeEpochs.TryGetValue(vector, out var epoch))
        {
            var shouldDispose = await epoch.TryReleaseAsync();
            if (shouldDispose)
            {
                _activeEpochs.TryRemove(vector, out _);
            }
        }
    }
    
    public void NotifyEpochSubsumed(EpochVector from, EpochVector to)
    {
        // When epoch A is subsumed into epoch B, the epoch object for A
        // should extend its lifetime to cover B's vector space
        if (_activeEpochs.TryGetValue(from, out var epochFrom))
        {
            // Option 1: Transfer reference to new vector
            _activeEpochs.TryAdd(to, epochFrom);
            epochFrom.AddReference();
            
            // Option 2: Merge strategy - needs more design work
            // This is where we handle the complex subsume semantics
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var epoch in _activeEpochs.Values)
        {
            await epoch.DisposeAsync();
        }
        _activeEpochs.Clear();
    }
}
```

### Integration with Block Development

#### Epoch-Compatible Block Interface

Blocks that want to use epoch-scoped services implement a new interface:

```csharp
/// <summary>
/// Marker interface for blocks that are epoch-compatible and want access
/// to the epoch object and its scoped services.
/// </summary>
public interface IEpochCompatibleBlock
{
    // Blocks implementing this interface will receive IEpoch in their execution context
}
```

#### Updated Block Execution Context

The `IBlockContext` interface is extended to provide the epoch object:

```csharp
public interface IBlockContext
{
    string BlockId { get; }
    
    /// <summary>
    /// Gets the current epoch object for epoch-compatible blocks.
    /// Returns null if the block is not epoch-compatible.
    /// </summary>
    IEpoch? CurrentEpoch { get; }
    
    // ... other existing members
}
```

#### Example: Refactored WriteContextBlock

The `WriteContextBlock` can be simplified using the epoch object:

```csharp
public sealed class WriteContextBlock : IEpochCompatibleBlock
{
    // Register in DI configuration:
    // services.AddEpochScoped<DemoDbContext>();
    
    public async IAsyncEnumerable<IEpochStream<DataRecord>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<DataRecord>> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var epochStream in input.WithCancellation(cancellationToken))
        {
            yield return CreateEpochStream(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, cancellationToken));
        }
    }

    private async IAsyncEnumerable<DataRecord> ProcessEpochItems(
        IEpochStream<DataRecord> epochStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Get epoch object from context (injected by framework)
        var epoch = _context.CurrentEpoch 
            ?? throw new InvalidOperationException("Epoch not available");
        
        // Resolve epoch-scoped DbContext - same instance across all blocks in this epoch
        var dbContext = epoch.GetService<DemoDbContext>();
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var processedItems = new List<DataRecord>();

        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            var trackedRecord = await dbContext.DataRecords.FindAsync(
                new object[] { item.Id }, 
                cancellationToken);
            
            if (trackedRecord != null)
            {
                trackedRecord.Processed = true;
            }

            processedItems.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var item in processedItems)
        {
            yield return item;
        }
    }
}
```

**Key Differences from Current Approach:**
- ❌ **Before**: Manually create `new DemoDbContext(_dbOptions)` in each block
- ✅ **After**: `epoch.GetService<DemoDbContext>()` - library manages lifecycle
- ❌ **Before**: Each block has its own DbContext instance
- ✅ **After**: All blocks in the same epoch share the same DbContext instance
- ❌ **Before**: Manual disposal with `await using`
- ✅ **After**: Automatic disposal when epoch completes

### Service Registration

Extend the DI container with epoch-scoped lifetime:

```csharp
public static class EpochServiceCollectionExtensions
{
    /// <summary>
    /// Adds a service with epoch-scoped lifetime.
    /// The service will be created once per epoch and shared across all blocks
    /// processing that epoch.
    /// </summary>
    public static IServiceCollection AddEpochScoped<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        // Register as scoped - the epoch's DI scope will ensure one per epoch
        return services.AddScoped<TService, TImplementation>();
    }
    
    public static IServiceCollection AddEpochScoped<TService>(
        this IServiceCollection services)
        where TService : class
    {
        return services.AddScoped<TService>();
    }
}
```

**Note**: Since epochs create their own `IServiceScope`, we can use standard `AddScoped` registration. The epoch manager ensures one scope per epoch, giving us the desired "one instance per epoch" semantics.

### Lifecycle Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     Epoch Lifecycle Flow                         │
└─────────────────────────────────────────────────────────────────┘

1. Source creates new epoch
   ┌──────────────┐
   │ Source Actor │
   └──────┬───────┘
          │ CreateEpoch(sourceId, sequence)
          ▼
   ┌──────────────┐
   │ EpochManager │ ──► GetOrCreateEpoch(vector)
   └──────┬───────┘     │
          │             ▼
          │      ┌──────────────┐
          │      │ Create Epoch │
          │      │   DI Scope   │
          │      └──────┬───────┘
          │             │
          ▼             ▼
   [Epoch Object with ServiceProvider]

2. Multiple blocks process same epoch
   ┌────────┐    ┌────────┐    ┌────────┐
   │ Block A│    │ Block B│    │ Block C│
   └───┬────┘    └───┬────┘    └───┬────┘
       │             │             │
       │  GetService<T>()          │
       └─────────┬───┘             │
                 ▼                 │
        ┌────────────────┐         │
        │  Same Service  │◄────────┘
        │   Instance     │
        └────────────────┘
        (epoch-scoped)

3. Block completes epoch processing
   ┌────────┐
   │ Block  │
   └───┬────┘
       │ OnEpochCompletedAsync()
       ▼
   ┌──────────────┐
   │ EpochManager │ ──► epoch.TryReleaseAsync()
   └──────────────┘     │
                        ▼
                 [Decrement ref count]
                        │
                        │ ref count == 0?
                        ▼
                  ┌──────────┐
                  │ Dispose  │
                  │ DI Scope │
                  └──────────┘

4. Epoch vector subsume operation
   ┌──────────────┐
   │ Subsume A→B  │
   └──────┬───────┘
          │ NotifyEpochSubsumed(A, B)
          ▼
   ┌──────────────┐
   │ EpochManager │ ──► Transfer/Merge Epoch
   └──────────────┘     │
                        ▼
                 [Epoch A lifetime extends to cover B]
```

### Reference Counting Strategy

**Challenge**: An epoch may be processed by multiple blocks at different stages of the pipeline.

**Solution**: Reference counting per block participation

```csharp
// Conceptual reference counting
Epoch created:           refCount = 1 (initial reference)
Block A starts:          refCount++ (now 2)
Block B starts:          refCount++ (now 3)
Block C starts:          refCount++ (now 4)
Block A completes:       refCount-- (now 3)
Block B completes:       refCount-- (now 2)
Block C completes:       refCount-- (now 1)
Initial reference done:  refCount-- (now 0) → Dispose epoch
```

**Implementation Detail**: The exact reference counting strategy needs to account for:
- How many blocks will process this epoch (may not be known upfront)
- Routing - not all blocks may see every epoch
- Global epoch alignment - when do we know an epoch is "done"?

**Proposed Approach**: Use `IEpochLifecycleParticipant.OnEpochCompletedAsync` events to track when each block completes. The epoch manager maintains a set of participating blocks and only disposes the epoch when all have reported completion.

---

## Fan-In Scenarios

**Critical Requirement**: The epoch-scoped services design must correctly handle fan-in scenarios where multiple upstream epoch streams merge.

### Key Requirements

1. **Element-Wise Max Merging**: When epoch vectors merge (e.g., `{s1=5}` + `{s2=3}` → `{s1=5, s2=3}`), use element-wise maximum
2. **Subsume Notification**: Trigger `NotifyEpochSubsumed` when vectors expand
3. **Service Consistency**: Downstream blocks must access the same epoch-scoped services regardless of which vector they use
4. **Reference Counting**: Epoch must not dispose until all vector variants complete
5. **No Duplicate Epochs**: Merging must not accidentally create new epoch instances

### Affected Blocks

This applies to **any block** that performs fan-in operations:
- `BufferNode` (explicit example)
- Join operations
- Merge operations
- Any block combining multiple epoch streams

### Detailed Specification

**📖 See**: [Fan-In Scenarios and Epoch Vector Merging](fan-in-scenarios.md)

This document provides:
- Complete fan-in handling requirements
- BufferNode implementation pattern
- 6 comprehensive test scenarios
- Performance considerations
- General fan-in guidelines for any block

**Test Coverage**: Phase 3 implementation must include all 6 test scenarios from the fan-in specification.

---

## API Refinements

Based on architectural review feedback, several API refinements clarify the design:

### 1. CurrentEpoch Null Semantics

- **For `IEpochCompatibleBlock`**: `CurrentEpoch` guaranteed non-null during processing
- **For other blocks**: `CurrentEpoch` always null
- **Benefit**: Clearer contract, blocks can use null-forgiving operator safely

### 2. Service Resolution Methods

```csharp
T GetService<T>() where T : notnull;      // Required service, throws if not found
T? GetService<T>() where T : class;        // Optional service, returns null
```

### 3. Participation Tracking

**Decision**: Reactive tracking - blocks counted as participants when they request epochs
- Simpler than pre-registration
- Handles routing automatically
- Consistent with DI semantics

### 4. Error Handling

- Block failures don't invalidate epoch for other blocks
- Epoch persists to allow other blocks to complete
- Disposal is safe even if services threw exceptions

### 5. Reference Counting vs Global Alignment

**Clarification**: Two independent mechanisms:
- **Reference counting** (new): Tracks when to dispose epoch DI scope
- **Global alignment** (existing): Coordinates checkpoint boundaries
- Both triggered by same events but serve different purposes

### Detailed Specifications

**📖 See**: [API Refinements and Implementation Clarifications](api-refinements.md)

This document provides:
- Complete API contracts and guarantees
- Thread-safety boundaries
- Performance benchmark targets
- Error handling semantics
- Concurrent access patterns

---

## Impacted Components

### POC Components

#### New Components
1. **`IEpoch` interface** - Core abstraction for epoch objects
2. **`Epoch` class** - Implementation with DI scope management
3. **`IEpochManager` interface** - Epoch lifecycle management
4. **`EpochManager` class** - Implementation of lifecycle management
5. **`IEpochCompatibleBlock` marker interface** - Indicates block wants epoch access
6. **`EpochServiceCollectionExtensions`** - DI registration helpers

#### Modified Components
1. **`IBlockContext`** - Add `CurrentEpoch` property
2. **`ActorExecutionContext`** - Integrate with `IEpochManager`
3. **`DataFlowGraph`** - Register and initialize `IEpochManager`
4. **`EpochLifecycleCoordinator`** - Integrate with epoch manager lifecycle events

#### Refactored Examples
1. **`WriteContextBlock`** - Simplify using epoch-scoped DbContext
2. **EF Core Tracker Block** - Refactor to use epoch-scoped services instead of manual tracking

### Core Library Components (Future)

When promoted from POC to production:
1. **DataFlow.Core** - Add `IEpoch`, `IEpochManager` interfaces
2. **DataFlow.Blocks** - Update block base classes to support `IEpochCompatibleBlock`
3. **DataFlow.DependencyInjection** - Add epoch-scoped service registration

---

## Implementation Plan

### Phase 1: Core Epoch Object Infrastructure (Week 1)

**Goal**: Implement basic epoch object with DI scope, without subsume handling

**Tasks**:
1. Define `IEpoch` interface
2. Implement `Epoch` class with DI scope management
3. Define `IEpochManager` interface (without subsume support)
4. Implement `EpochManager` with basic lifecycle management
5. Add unit tests for epoch creation and disposal

**Deliverables**:
- `poc/DataFlow.POC/Core/IEpoch.cs`
- `poc/DataFlow.POC/Core/Epoch.cs`
- `poc/DataFlow.POC/Core/IEpochManager.cs`
- `poc/DataFlow.POC/Core/EpochManager.cs`
- `poc/DataFlow.POC.Tests/Core/EpochManagerTests.cs`

**Success Criteria**:
- Can create epoch objects with their own DI scope
- Services resolved from same epoch return same instance
- Epoch disposes scope when all references released

### Phase 2: Block Integration and Context (Week 1-2)

**Goal**: Integrate epoch objects into block execution context

**Tasks**:
1. Add `IEpochCompatibleBlock` marker interface
2. Extend `IBlockContext` with `CurrentEpoch` property
3. Update `ActorExecutionContext` to manage epoch lifecycle
4. Integrate `EpochManager` into `DataFlowGraph`
5. Add integration tests

**Deliverables**:
- `poc/DataFlow.POC/Core/IEpochCompatibleBlock.cs`
- Updated `poc/DataFlow.POC/Core/IBlockContext.cs`
- Updated `poc/DataFlow.POC/Core/ActorExecutionContext.cs`
- Updated `poc/DataFlow.POC/Core/DataFlowGraph.cs`
- `poc/DataFlow.POC.Tests/Core/EpochBlockIntegrationTests.cs`

**Success Criteria**:
- Blocks implementing `IEpochCompatibleBlock` receive `IEpoch` in context
- Multiple blocks processing same epoch get same `IEpoch` instance
- Epoch disposed when all blocks complete

### Phase 3: Epoch Vector Subsume Support (Week 2)

**Goal**: Handle epoch vector subsume operations correctly, including fan-in scenarios

**Tasks**:
1. Implement `NotifyEpochSubsumed` in `EpochManager`
2. Design and implement subsume strategy (reference transfer)
3. Update reference counting to handle subsume operations
4. Add tests for subsume scenarios (including all 6 fan-in tests)
5. Implement fan-in support for BufferNode
6. Document subsume semantics

**Deliverables**:
- Updated `poc/DataFlow.POC/Core/EpochManager.cs` with subsume support
- `docs/design/epoch-scoped-services/subsume-semantics.md` (already created)
- `docs/design/epoch-scoped-services/fan-in-scenarios.md` (already created)
- `poc/DataFlow.POC.Tests/Core/EpochSubsumeTests.cs`
- `poc/DataFlow.POC.Tests/Core/FanInEpochTests.cs` (all 6 scenarios from spec)

**Success Criteria**:
- Epoch lifetime correctly extends through subsume operations
- Services remain accessible through subsumed vectors
- No memory leaks from unreleased epochs
- **All 6 fan-in test scenarios pass** (see fan-in-scenarios.md)
- BufferNode correctly merges vectors and preserves epoch-scoped services
- Services remain accessible through subsumed vectors
- No memory leaks from unreleased epochs

### Phase 4: DI Registration and Helpers (Week 2-3)

**Goal**: Provide convenient DI registration patterns

**Tasks**:
1. Implement `EpochServiceCollectionExtensions`
2. Add `AddEpochScoped<T>` extension methods
3. Create examples and documentation
4. Add tests for DI integration

**Deliverables**:
- `poc/DataFlow.POC/DependencyInjection/EpochServiceCollectionExtensions.cs`
- `docs/design/epoch-scoped-services/di-integration.md`
- `poc/DataFlow.POC.Tests/DependencyInjection/EpochScopedServicesTests.cs`

**Success Criteria**:
- Services registered with `AddEpochScoped` behave as epoch-scoped
- Clear error messages for common mistakes
- Examples demonstrate typical usage patterns

### Phase 5: Refactor EF Core Examples (Week 3)

**Goal**: Demonstrate simplification by refactoring existing examples

**Tasks**:
1. Refactor `WriteContextBlock` to use epoch-scoped DbContext
2. Refactor EF Core tracker block to use epoch-scoped services
3. Create before/after comparison documentation
4. Add integration tests demonstrating shared DbContext across blocks

**Deliverables**:
- Updated `poc/EpochAnchoringDemo/Blocks/WriteContextBlock.cs`
- Refactored EF Core tracker block (if exists)
- `docs/design/epoch-scoped-services/ef-core-examples.md`
- `poc/EpochAnchoringDemo.Tests/EpochScopedDbContextTests.cs`

**Success Criteria**:
- `WriteContextBlock` code is simpler and more maintainable
- Multiple blocks in same epoch share same DbContext instance
- No race conditions in concurrent block execution
- Tests demonstrate correctness across various scenarios

### Phase 6: Comprehensive Testing and Documentation (Week 3-4)

**Goal**: Validate design with comprehensive testing scenarios

**Tasks**:
1. Test concurrent blocks at different pipeline stages
2. Test blocks processing at different rates
3. Test various routing scenarios
4. Test error handling and cleanup
5. Performance testing vs. manual approach
6. Complete architecture documentation

**Deliverables**:
- `poc/DataFlow.POC.Tests/Integration/EpochScopedServicesIntegrationTests.cs`
- `docs/design/epoch-scoped-services/performance-analysis.md`
- `docs/design/epoch-scoped-services/testing-strategy.md`
- Updated main README with full architecture details

**Success Criteria**:
- All test scenarios pass
- No race conditions detected
- Performance overhead < 5% vs. manual approach
- Memory usage comparable to manual approach
- Clear documentation for future implementers

---

## Alternative Approaches

### Alternative 1: Manual Epoch Tracking (Current Approach)

**Description**: Each block manually tracks epoch boundaries and manages resources.

**Example**: Current `WriteContextBlock` implementation
```csharp
await using var dbContext = new DemoDbContext(_dbOptions);
await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
// Process items
await dbContext.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

**Pros**:
- ✅ Simple to understand for individual blocks
- ✅ No framework complexity
- ✅ Full control over resource lifetime

**Cons**:
- ❌ Each block reimplements epoch tracking
- ❌ Cannot share resources across blocks in same epoch
- ❌ Manual tracking of epoch vector subsume operations
- ❌ Barrier to block development

**Verdict**: **Not Selected** - Too much duplication and complexity for block developers

### Alternative 2: Global Epoch Context (AsyncLocal)

**Description**: Use `AsyncLocal<IEpoch>` to provide ambient epoch context.

**Example**:
```csharp
public static class EpochContext
{
    private static readonly AsyncLocal<IEpoch> _current = new();
    
    public static IEpoch Current
    {
        get => _current.Value ?? throw new InvalidOperationException("No epoch context");
        set => _current.Value = value;
    }
}

// In block
var dbContext = EpochContext.Current.GetService<DbContext>();
```

**Pros**:
- ✅ No need to pass epoch through method signatures
- ✅ Ambient context pattern familiar to developers
- ✅ Simple API

**Cons**:
- ❌ Hidden dependencies make testing harder
- ❌ Doesn't work well with highly concurrent scenarios
- ❌ `AsyncLocal` has performance overhead
- ❌ Difficult to reason about in complex flows
- ❌ Still need lifecycle management infrastructure

**Verdict**: **Not Selected** - Hidden dependencies and concurrency concerns outweigh benefits

### Alternative 3: Epoch Object with DI Scope (Proposed)

**Description**: Explicit epoch object passed through context, with integrated DI scope.

**Example**:
```csharp
var epoch = _context.CurrentEpoch;
var dbContext = epoch.GetService<DbContext>();
```

**Pros**:
- ✅ Explicit dependencies - clear what blocks depend on
- ✅ Works well with concurrent execution
- ✅ Natural DI integration
- ✅ Testable - can inject mock epoch objects
- ✅ Library manages complexity, blocks use simple API

**Cons**:
- ⚠️ Requires framework infrastructure
- ⚠️ Slightly more complex than ambient context
- ⚠️ Need to handle subsume operations

**Verdict**: **Selected** - Best balance of explicitness, testability, and developer experience

### Alternative 4: Block-Level DI Scopes

**Description**: Each block gets its own DI scope, managed at block level rather than epoch level.

**Example**:
```csharp
public class MyBlock
{
    public async Task ProcessAsync(IServiceScope blockScope, DataRecord item)
    {
        var service = blockScope.ServiceProvider.GetService<MyService>();
    }
}
```

**Pros**:
- ✅ Standard DI scope semantics
- ✅ Simple lifetime management
- ✅ No epoch-specific code

**Cons**:
- ❌ Cannot share services across blocks in same epoch
- ❌ Doesn't solve the core problem
- ❌ Scope lifetime unclear - per item? per batch? per epoch?

**Verdict**: **Not Selected** - Doesn't address the epoch-scoped service sharing requirement

---

## Success Criteria

| Criterion | Target | Validation Method |
|-----------|--------|-------------------|
| **Service Sharing** | Multiple blocks processing the same epoch access the same service instance | Integration test with 3+ blocks |
| **Lifecycle Correctness** | Services disposed when epoch fully processed | Memory profiling, disposal tracking |
| **Subsume Handling** | Epoch lifetime correctly extends through subsume operations | Unit tests for subsume scenarios |
| **No Race Conditions** | Concurrent access to epoch-scoped services is safe | Stress tests with high concurrency |
| **Performance** | Overhead < 5% vs. manual approach | Benchmark tests |
| **Memory** | Memory usage comparable to manual approach (±10%) | Memory profiling |
| **Developer Experience** | Block code is simpler than manual approach | Code comparison, LoC metrics |
| **EF Core Integration** | Can register DbContext as epoch-scoped | Integration test with EF Core |
| **Foundation for #125** | Architecture supports epoch-scoped transactions | Design review, proof of concept |

### Detailed Test Scenarios

1. **Same Epoch, Different Blocks**
   - Block A and Block B process same epoch
   - Both resolve `IMyService`
   - Assert: Same instance returned

2. **Different Epochs**
   - Block A processes epoch 1
   - Block B processes epoch 2
   - Both resolve `IMyService`
   - Assert: Different instances returned

3. **Different Processing Rates**
   - Fast block completes epoch 1, 2, 3
   - Slow block still processing epoch 1
   - Fast block starts epoch 4
   - Assert: Epoch 1 not disposed until slow block completes

4. **Routing Scenarios**
   - Epoch routed to blocks A and C (not B)
   - Only A and C receive the epoch object
   - Assert: Correct reference counting, proper disposal

5. **Subsume Operations**
   - Epoch A created (source 1, seq 5)
   - Epoch B created (source 2, seq 3)
   - Epochs subsumed into merged epoch
   - Assert: Original epoch services still accessible

6. **Error Handling**
   - Block throws exception during epoch processing
   - Assert: Epoch resources properly cleaned up
   - Assert: Other blocks can continue

7. **Concurrent Access**
   - 10 blocks concurrently access same epoch
   - All resolve epoch-scoped service
   - Assert: Thread-safe access
   - Assert: Same instance for all

---

## Related Documentation

### Design Documents (This Folder)

- **[Subsume Semantics](subsume-semantics.md)** - Detailed epoch vector subsume operation handling
- **[DI Integration](di-integration.md)** - Service registration patterns and usage examples
- **[Alternatives Comparison](alternatives/comparison.md)** - Analysis of 5 alternative approaches
- **[Block Lifetime Analysis](block-lifetime-analysis.md)** - Should blocks be epoch-scoped?
- **[Fan-In Scenarios](fan-in-scenarios.md)** - BufferNode and general fan-in handling ⭐ NEW
- **[API Refinements](api-refinements.md)** - Implementation clarifications and best practices ⭐ NEW
- **[Research Summary](RESEARCH_SUMMARY.md)** - Consolidated findings and recommendations

### Analysis Documents
- (None - this is a design proposal based on existing POC patterns)

### Related POC Documentation
- [Phase 5: EF Core Anchoring Demo](../../../poc/docs/plans/PHASE5_EFCORE_ANCHORING_DEMO.md)
- [Understanding Anchors vs Checkpoints](../../../poc/EpochAnchoringDemo/UNDERSTANDING_ANCHORS_VS_CHECKPOINTS.md)

### Related Issues
- #125 - Epoch-scoped EF Core transactions (future work enabled by this design)
- #412 - Original design issue

### Architecture Decision Records
- (ADRs to be created during implementation phase)

---

## Open Questions

**Note**: Several questions have been addressed through architectural review feedback. See [API Refinements](api-refinements.md) for details.

### Resolved Questions ✅

1. **Subsume Strategy** → **Resolved**: Reference transfer (see subsume-semantics.md)
2. **Reference Counting** → **Resolved**: Reactive tracking (see api-refinements.md #3)
3. **Block Lifecycle Integration** → **Resolved**: Marker interface + lifecycle events (see api-refinements.md #1)
4. **Error Recovery** → **Resolved**: Epoch persists on block failure (see api-refinements.md #5)

### Remaining Questions ⏳

1. **Service Resolution Patterns**
   - Should epoch-scoped services be explicitly registered or automatic?
   - **Recommendation**: Explicit registration via `AddScoped` (see di-integration.md)
   - How do we handle services that depend on other epoch-scoped services?
   - **Answer**: Standard DI resolution handles this automatically
   
2. **Performance Optimization**
   - Is there a way to pre-allocate epoch objects based on flow topology?
   - Can we pool epoch objects for reuse?
   - What's the overhead of the DI scope creation?
   - **Answer**: To be determined through benchmarking in Phase 6

3. **Fan-In Edge Cases**
   - Maximum number of sources in fan-in?
   - **Recommendation**: No artificial limit, monitor performance
   - Concurrent subsume operations?
   - **Answer**: Use locking in EpochManager (see fan-in-scenarios.md)

These remaining questions should be addressed during implementation phases through prototyping and testing.

---

## Next Steps

1. **Review and Feedback** ✅ Complete
   - Architectural review feedback incorporated
   - Fan-in scenarios documented
   - API refinements specified

2. **Prototype**
   - Implement Phase 1 (Core infrastructure)
   - Validate basic approach with simple tests
   - Gather learnings

3. **Iterate**
   - Refine design based on prototype findings
   - Address open questions with concrete solutions
   - Document decisions in ADRs

4. **Full Implementation**
   - Execute phases 2-6
   - Continuous testing and validation
   - Documentation updates

5. **Handover to Implementation**
   - Create implementation work item with detailed specifications
   - Include prototype code in `/research/epoch-scoped-services/handover/prototype/`
   - Document lessons learned

---

**Document Status**: ✅ Draft Complete - Ready for Review  
**Next Action**: Team review and feedback gathering  
**Implementation Start**: TBD based on review feedback
