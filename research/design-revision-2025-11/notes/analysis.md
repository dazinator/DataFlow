# Problem Analysis

**Date**: 2025-11-19  
**Research**: Design Revision for DI Service Registration

---

## Problem 1: Parallel Builder Structures

### Current Situation

The previous design (#473) introduced:
- `DataFlowGraphBuilderEx` - New builder with DI support
- `DataFlowGraphBuilder` - Original builder (unchanged)

Both builders exist side-by-side for graph construction, creating confusion about which to use.

### Specific Issues

1. **Two APIs for same purpose**: Both build DataFlow graphs
2. **Feature disparity**: Only `Ex` variant supports DI-registered blocks
3. **Migration confusion**: Unclear when to use which builder
4. **Code duplication**: Both have similar Connect/AddBlock methods

### Analysis

**Why was `Ex` created?**
- Avoided modifying existing `DataFlowGraphBuilder`
- Backward compatibility concern
- Added new capabilities (UseBlock with DI)

**Problems with this approach:**
- Creates permanent parallel structure
- Developers must choose between two builders
- No clear migration path
- API confusion

### Possible Solutions

#### Option A: Enhance Existing Builder (Recommended)
Add DI support directly to `DataFlowGraphBuilder`:
```csharp
public class DataFlowGraphBuilder
{
    private readonly IServiceProvider? _serviceProvider;
    
    // Constructor enhancement (backward compatible)
    public DataFlowGraphBuilder(
        string name, 
        IServiceProvider? serviceProvider = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        _name = name;
        _serviceProvider = serviceProvider;
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    }
    
    // New method
    public DataFlowGraphBuilder UseBlock(string name)
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider required for UseBlock");
        
        var block = _serviceProvider.GetRequiredKeyedService<IBlock>(name);
        return AddBlock(block);
    }
    
    // Existing AddBlock method unchanged
    public DataFlowGraphBuilder AddBlock(IBlock block) { ... }
}
```

**Pros**:
- ✅ Single builder class
- ✅ Backward compatible (serviceProvider is optional)
- ✅ No parallel structures
- ✅ Clear upgrade path (just pass serviceProvider)

**Cons**:
- ⚠️ Modifies existing class (low risk - additive only)

#### Option B: Replace with Ex variant
Remove `DataFlowGraphBuilder`, keep only `Ex`:
- ❌ Breaking change for existing POC code
- ❌ Forces migration
- ❌ Not necessary if Option A works

#### Option C: Facade Pattern
Keep both, make one delegate to the other:
- ❌ Still has two APIs
- ❌ Doesn't solve confusion

### Recommendation

**Use Option A**: Enhance existing `DataFlowGraphBuilder` with optional DI support.

**Rationale**:
1. No parallel structures - single canonical builder
2. Backward compatible - optional parameter
3. Clear semantics - pass serviceProvider to enable UseBlock()
4. Minimal code change - additive only

---

## Problem 2: Default Lifetime Scope

### Current Situation

Previous design registered all blocks as **singletons**:
```csharp
_services.AddKeyedSingleton<IBlock>(name, (sp, key) => factory(sp));
```

### Specific Issues

1. **Multi-instance execution**: If you want to execute the same graph multiple times in different scopes, singletons share state
2. **Scoped dependencies**: Blocks that depend on scoped services (like DbContext) can't work properly
3. **Thread safety assumptions**: Singletons must be thread-safe

### Analysis

**When is singleton safe?**
- Stateless blocks
- Thread-safe blocks
- Single graph instance throughout app lifetime

**When is singleton problematic?**
- Blocks with mutable state
- Blocks using scoped dependencies (DbContext, scoped HttpClient, etc.)
- Multiple graph instances executing concurrently

**Current POC Usage**: Need to check existing tests/samples to see what's being used.

### Possible Solutions

#### Option A: Default to Scoped (Recommended)
```csharp
public DataFlowBuilder AddBlock<TBlock>(
    string name, 
    Func<IServiceProvider, TBlock> factory)
    where TBlock : IBlock
{
    // Default to scoped
    _services.AddKeyedScoped<IBlock>(name, (sp, key) => factory(sp));
    return this;
}
```

**Pros**:
- ✅ Safe default for scoped dependencies
- ✅ Each graph execution gets its own instances
- ✅ Aligns with typical .NET DI patterns

**Cons**:
- ⚠️ Performance overhead (new instances per scope)
- ⚠️ Requires scope management

#### Option B: Default to Singleton with Scoped Option
```csharp
public DataFlowBuilder AddBlock<TBlock>(
    string name, 
    Func<IServiceProvider, TBlock> factory,
    ServiceLifetime lifetime = ServiceLifetime.Singleton)
    where TBlock : IBlock
{
    switch (lifetime)
    {
        case ServiceLifetime.Singleton:
            _services.AddKeyedSingleton<IBlock>(name, (sp, key) => factory(sp));
            break;
        case ServiceLifetime.Scoped:
            _services.AddKeyedScoped<IBlock>(name, (sp, key) => factory(sp));
            break;
        case ServiceLifetime.Transient:
            _services.AddKeyedTransient<IBlock>(name, (sp, key) => factory(sp));
            break;
    }
    return this;
}
```

**Pros**:
- ✅ Flexible
- ✅ Explicit choice

**Cons**:
- ❌ Default is still singleton (same problems)
- ⚠️ More complex API

#### Option C: Separate Methods
```csharp
public DataFlowBuilder AddSingletonBlock<TBlock>(...) { }
public DataFlowBuilder AddScopedBlock<TBlock>(...) { }
public DataFlowBuilder AddTransientBlock<TBlock>(...) { }
```

**Pros**:
- ✅ Very explicit
- ✅ Follows .NET patterns (AddSingleton, AddScoped, etc.)

**Cons**:
- ⚠️ More API surface
- ⚠️ Still need to choose default for simple AddBlock()

### Investigation Needed

1. Check existing POC usage - are blocks currently stateless?
2. Are scoped dependencies used anywhere?
3. Performance impact of scoped vs singleton?

### Preliminary Recommendation

**Option C with sensible default**: 
- Provide `AddSingletonBlock`, `AddScopedBlock`, `AddTransientBlock`
- Make `AddBlock` default to **Scoped** for safety
- Clear naming makes lifetime explicit

**Rationale**:
1. Safe default (scoped) for most scenarios
2. Explicit methods for other lifetimes
3. Follows .NET DI conventions
4. Clear semantics

---

## Problem 3: Registration Idempotence

### Current Situation

Previous design doesn't use TryAdd variants:
```csharp
_services.AddKeyedSingleton<IBlock>(name, (sp, key) => factory(sp));
```

Calling `AddDataFlows()` multiple times creates duplicate registrations.

### Specific Issues

1. **Multiple registrations**: Same block registered multiple times
2. **Resolution behavior**: Which one gets resolved? (Last one wins?)
3. **Core services**: Infrastructure services also duplicated

### Analysis

**What should happen?**

Scenario 1: Called twice with same block name
```csharp
services.AddDataFlows(df => df.AddBlock("producer", sp => new ProducerA()));
services.AddDataFlows(df => df.AddBlock("producer", sp => new ProducerB()));
```
Options:
- A) Throw exception (strict)
- B) Last one wins (override)
- C) First one wins (idempotent)
- D) Both registered, resolution ambiguous

Scenario 2: Called twice, different blocks
```csharp
services.AddDataFlows(df => df.AddBlock("producer", ...));
services.AddDataFlows(df => df.AddBlock("transformer", ...));
```
Should work fine - different names.

Scenario 3: Core infrastructure services
```csharp
services.AddDataFlows(df => { });
services.AddDataFlows(df => { });
```
Core services (if any) shouldn't be duplicated.

### Possible Solutions

#### Option A: TryAdd for Blocks (First Wins)
```csharp
public DataFlowBuilder AddBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
    where TBlock : IBlock
{
    // Only add if name not already registered
    if (!_services.Any(sd => sd.ServiceKey?.ToString() == name && sd.ServiceType == typeof(IBlock)))
    {
        _services.AddKeyedScoped<IBlock>(name, (sp, key) => factory(sp));
    }
    return this;
}
```

**Pros**:
- ✅ Idempotent
- ✅ First registration wins (explicit)

**Cons**:
- ⚠️ Silent when registration skipped
- ⚠️ Might hide configuration errors

#### Option B: Throw on Duplicate
```csharp
public DataFlowBuilder AddBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
    where TBlock : IBlock
{
    if (_services.Any(sd => sd.ServiceKey?.ToString() == name && sd.ServiceType == typeof(IBlock)))
    {
        throw new InvalidOperationException($"Block '{name}' is already registered");
    }
    _services.AddKeyedScoped<IBlock>(name, (sp, key) => factory(sp));
    return this;
}
```

**Pros**:
- ✅ Explicit error
- ✅ Catches configuration mistakes

**Cons**:
- ❌ Not idempotent
- ❌ Can't safely call AddDataFlows twice

#### Option C: Allow Override
```csharp
// Always add, last one wins
_services.AddKeyedScoped<IBlock>(name, (sp, key) => factory(sp));
```

**Pros**:
- ✅ Flexible
- ✅ Simple

**Cons**:
- ❌ Ambiguous behavior
- ❌ Might hide mistakes

### .NET Framework Precedents

**AddHealthChecks**: Last registration wins (override behavior)
**AddAuthentication**: Last configuration wins  
**AddOptions**: Last configuration wins  
**AddDbContext**: Typically throws if registered twice

### Recommendation

**Hybrid Approach**:
1. **For blocks/strategies**: Throw on duplicate (Option B)
2. **For infrastructure**: TryAdd (idempotent)

```csharp
public static IServiceCollection AddDataFlows(
    this IServiceCollection services,
    Action<DataFlowBuilder> configure)
{
    // Core infrastructure - idempotent
    // (None currently, but if we add core services later)
    
    var builder = new DataFlowBuilder(services);
    configure(builder);
    
    return services;
}
```

**Rationale**:
1. Duplicate block names are likely configuration errors
2. Clear error message helps debugging
3. Core infrastructure should be idempotent (if added)
4. Aligns with DbContext pattern (structural registrations throw)

---

## Problem 4: Graph Builder Integration

### Current Situation

Graph building is separate from service registration:
```csharp
// Registration (in Startup/Program.cs)
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new ActorBlock<int, string, MyActor>(...));
});

// Graph building (somewhere else)
var builder = new DataFlowGraphBuilder("flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
var graph = builder.Build();
```

### Specific Issues

1. **Separation**: Registration and topology are disconnected
2. **No unified entry point**: Not clear this is all "DataFlow configuration"
3. **Runtime topology**: Graph topology defined at runtime, not with services

### Analysis

**Desired pattern** (from issue):
```csharp
services.AddDataFlows(df => 
{
    df.AddGraph("graph-a", BuildGraph);
});
```

**Questions**:
1. What is `BuildGraph`? A method? A delegate?
2. When does graph building happen? Registration time or runtime?
3. Where is graph stored? In DI container?
4. How to get graph instance later?

**Scenarios**:

Scenario A: Single graph, defined at startup
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);
    df.AddBlock("transformer", ...);
    df.AddGraph("main", graph => 
    {
        graph.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
    });
});

// Later, resolve graph
var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("main");
await graph.StartAsync();
```

Scenario B: Multiple graphs, different topologies
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);
    df.AddBlock("transformer", ...);
    df.AddBlock("processor", ...);
    
    df.AddGraph("pipeline-a", g => g.UseBlock("producer").UseBlock("transformer").Connect("producer", "transformer"));
    df.AddGraph("pipeline-b", g => g.UseBlock("producer").UseBlock("processor").Connect("producer", "processor"));
});
```

Scenario C: Dynamic graphs at runtime
```csharp
// Not all graphs can be predefined
// Some need to be built based on runtime configuration

var graph = new DataFlowGraphBuilder("dynamic", serviceProvider)
    .UseBlock("producer")
    .UseBlock(GetTransformerBasedOnConfig())
    .Connect(...);
```

### Possible Solutions

#### Option A: AddGraph() Method
```csharp
public class DataFlowBuilder
{
    public DataFlowBuilder AddGraph(
        string name,
        Action<DataFlowGraphBuilder> configure)
    {
        _services.AddKeyedSingleton<DataFlowGraph>(name, (sp, key) =>
        {
            var builder = new DataFlowGraphBuilder(name, sp);
            configure(builder);
            return builder.Build();
        });
        return this;
    }
}
```

**Pros**:
- ✅ Unified registration
- ✅ Graph stored in DI
- ✅ Easy to resolve later

**Cons**:
- ⚠️ Graph built at registration time (may not have access to runtime config)
- ⚠️ All graphs must be predefined

#### Option B: Graph Factory Registration
```csharp
public class DataFlowBuilder
{
    public DataFlowBuilder AddGraphFactory(
        string name,
        Func<IServiceProvider, DataFlowGraph> factory)
    {
        _services.AddKeyedSingleton<DataFlowGraph>(name, (sp, key) => factory(sp));
        return this;
    }
}

// Usage
services.AddDataFlows(df => 
{
    df.AddGraphFactory("main", sp =>
    {
        var builder = new DataFlowGraphBuilder("main", sp);
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
        return builder.Build();
    });
});
```

**Pros**:
- ✅ Flexible factory pattern
- ✅ Access to service provider
- ✅ Can include runtime logic

**Cons**:
- ⚠️ More verbose
- ⚠️ Not as fluent

#### Option C: Keep Separate (Current)
Don't integrate graph building into AddDataFlows.

**Pros**:
- ✅ Clear separation of concerns
- ✅ Supports dynamic runtime graphs
- ✅ Simple

**Cons**:
- ❌ Not unified
- ❌ Doesn't meet stated goal

#### Option D: Class-Based Definitions
```csharp
public interface IDataFlowDefinition
{
    void Configure(DataFlowGraphBuilder builder);
}

public class MyFlowDefinition : IDataFlowDefinition
{
    public void Configure(DataFlowGraphBuilder builder)
    {
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
    }
}

// Registration
services.AddDataFlows(df => 
{
    df.AddGraphDefinition<MyFlowDefinition>("main");
});
```

**Pros**:
- ✅ Clean class-based organization
- ✅ Testable definitions
- ✅ Unified registration

**Cons**:
- ⚠️ Requires extra class for each graph
- ⚠️ More ceremony

### Investigation Needed

1. Do we need predefined graphs in DI, or is dynamic runtime building sufficient?
2. How many graphs are typically defined per application?
3. Are graphs reused or created per-request?

### Preliminary Recommendation

**Hybrid Approach**:
1. **Option A**: AddGraph() for simple, predefined graphs
2. **Keep dynamic support**: Can still use DataFlowGraphBuilder directly for runtime graphs
3. **Optional Option D**: Class-based definitions for complex graphs

```csharp
services.AddDataFlows(df => 
{
    // Blocks and strategies
    df.AddBlock("producer", ...);
    df.AddBlock("transformer", ...);
    
    // Simple predefined graph
    df.AddGraph("main", g => 
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
    
    // Or class-based for complex graphs
    df.AddGraphDefinition<ComplexFlowDefinition>("complex");
});

// Dynamic graphs still supported
var dynamicGraph = new DataFlowGraphBuilder("dynamic", serviceProvider)
    .UseBlock("producer")
    .UseBlock(GetRuntimeBlock())
    .Build();
```

**Rationale**:
1. Provides unified registration for common case
2. Maintains flexibility for dynamic scenarios
3. Optional class-based for complex graphs
4. Meets stated goal while keeping options open

---

---

## Problem 5: Namespace Collisions in Modular Architectures

[Content already documented - see research findings]

---

## Problem 6: Block Name Duplication (Developer Experience)

### Current Situation

Blocks require the name to be specified in **two** places:
1. **Registration key**: `AddBlock("transformer", ...)`
2. **Block constructor**: `new ActorBlock<...>("transformer", ...)`

### Example of the Problem

```csharp
services.AddDataFlows(df => {
    df.AddBlock("transformer", sp => 
        new ActorBlock<int, string, TransformActor<int, string>>(
            "transformer",  // ⚠️ Name duplicated here
            sp.GetRequiredService<IServiceScopeFactory>()));
});
```

### Specific Issues

1. **Verbosity**: Every block registration requires a verbose factory function
2. **Error-Prone**: Easy to create mismatches between registration key and block name
3. **Maintenance**: Renaming a block requires changing two places
4. **Poor DX**: Doesn't leverage DI capabilities for constructor injection
5. **Type Repetition**: Actor type parameters must be fully specified

### Analysis

**Root Cause**: Blocks take `name` as a constructor parameter, but this name is already known at registration time.

**Why does BlockBase require name in constructor?**
```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    protected BlockBase(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
    
    public string Name { get; }
    // ...
}
```

The name is used for identification, logging, and error messages. However, requiring it in the constructor creates coupling with the registration API.

### Desired Developer Experience

From the user's comment:
```csharp
df.AddActorBlock<TransformActor<int, string>>("transformer"); 
// All dependencies injected by DI, name set automatically
```

Even better - infer types:
```csharp
df.AddActorBlock<int, string, TransformActor>("transformer");
// Type parameters clear, name matches registration key
```

### Possible Solutions

#### Option A: Use IBlockContext Initialization Pattern (Recommended) ⭐ REFINED

Make blocks DI-friendly using `IBlockContext` for post-construction initialization.

**Why IBlockContext?**
- Already exists in codebase (`/poc/DataFlow.POC/Core/IBlockContext.cs`)
- Designed for block metadata and identification
- Provides clean lifecycle: construction → initialization
- Extensible for future metadata needs

**Before**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    public ActorBlock(string name, IServiceScopeFactory scopeFactory)
        : base(name)
    {
        _scopeFactory = scopeFactory;
    }
}
```

**After**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    // DI-friendly: only dependencies in constructor
    public ActorBlock(IServiceScopeFactory scopeFactory)
        : base()
    {
        _scopeFactory = scopeFactory;
    }
}

public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private IBlockContext? _context;
    
    // Initialization method called post-construction
    internal void SetContext(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
            throw new InvalidOperationException("Block context already set");
        _context = context;
    }
    
    public string Name => _context?.BlockName ?? string.Empty;
}
```

**Registration** creates block via DI, then initializes:
```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    // Register block type for DI resolution
    _services.TryAddScoped<ActorBlock<TIn, TOut, TActor>>();
    
    var fullKey = ResolveKey(name);
    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        // Resolve from DI - all scoped dependencies properly tracked
        var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
        // Initialize with context containing name
        var blockName = key as string ?? throw new InvalidOperationException();
        block.SetContext(new BlockContext(blockName));
        return block;
    });
}
```

**Pros**:
- ✅ No duplication - name only specified once
- ✅ DI-friendly constructors - only dependencies
- ✅ Name guaranteed to match registration
- ✅ Uses standard DI resolution (no `ActivatorUtilities`)
- ✅ Scoped services properly tracked for disposal
- ✅ Lifecycle pattern: construct → initialize
- ✅ Extensible via `IBlockContext.Metadata`
- ✅ Enables strongly-typed registration helpers

**Cons**:
- ⚠️ Blocks have two-phase initialization
- ⚠️ Blocks created outside DI need manual context setting
- ⚠️ Requires updating all block types

**Key Advantages Over Direct Property Setting**:
1. **No ActivatorUtilities**: Uses standard `GetRequiredService` - proper scoped tracking
2. **Lifecycle separation**: Construction vs initialization clearly separated
3. **Extensibility**: `IBlockContext` can carry metadata, not just name
4. **Immutability**: Context set once, then read-only

#### Option B: Strongly-Typed Registration Helpers

Keep current block structure, add helper methods:

```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    return AddBlock(name, sp => new ActorBlock<TIn, TOut, TActor>(
        name,
        sp.GetRequiredService<IServiceScopeFactory>()));
}

public DataFlowBuilder AddProducerBlock<T>(string name)
{
    return AddBlock(name, sp => new ProducerBlock<T>(
        name,
        sp.GetRequiredService<IAsyncEnumerable<T>>()));
}
```

**Usage**:
```csharp
df.AddActorBlock<int, string, TransformActor>("transformer");
df.AddProducerBlock<int>("producer");
```

**Pros**:
- ✅ Much cleaner API
- ✅ Type inference possible
- ✅ No changes to block classes
- ✅ Still DI-friendly

**Cons**:
- ⚠️ Name still duplicated internally
- ⚠️ Need helper for each block type
- ⚠️ Doesn't solve root problem

#### Option C: Hybrid Approach (Recommended)

Combine Option A (remove name from constructor) with Option B (typed helpers):

**Block Constructor** (DI-friendly):
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    public ActorBlock(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
}
```

**Typed Helper**:
```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    // Register block type for DI
    _services.TryAddScoped<ActorBlock<TIn, TOut, TActor>>();
    
    var fullKey = ResolveKey(name);
    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        // Resolve from DI (not ActivatorUtilities)
        var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
        // Initialize with context
        var blockName = key as string ?? throw new InvalidOperationException();
        block.SetContext(new BlockContext(blockName));
        return block;
    });
    return this;
}
```

**Usage**:
```csharp
services.AddDataFlows(df => {
    df.AddActorBlock<int, string, TransformActor>("transformer");
    df.AddProducerBlock<int>("producer");
    df.AddBatchBlock<int>("batcher", maxBatchSize: 100);
});
```

**Pros**:
- ✅ Clean, concise API
- ✅ Type inference where possible
- ✅ Full DI support for all dependencies
- ✅ Name guaranteed to match
- ✅ No duplication
- ✅ Scoped services properly tracked (no ActivatorUtilities)
- ✅ IBlockContext provides extensibility

**Cons**:
- ⚠️ Breaking change to block constructors
- ⚠️ Need helpers for each block type
- ⚠️ Two-phase initialization (construct → SetContext)

### Impact Analysis

**Breaking Changes**:
- Block constructors change (remove `string name` parameter)
- Affects all existing block types
- POC code only (not production)

**Benefits**:
- Dramatically improved developer experience
- Safer (no name mismatches)
- More maintainable
- Leverages DI properly

### Recommendation

**Option C: Hybrid Approach**

1. **Remove `name` from block constructors** - make blocks truly DI-friendly
2. **Set `Name` property during registration** - from the registration key
3. **Add strongly-typed helpers** - for common block types
4. **Keep generic `AddBlock`** - for flexibility

**Migration Path** (POC):
1. Update `BlockBase` to make `Name` settable
2. Update all block constructors to remove `name` parameter
3. Update registration to set `Name` property
4. Add typed helpers (`AddActorBlock`, `AddProducerBlock`, etc.)
5. Update tests to use new API

**API Examples**:
```csharp
// Clean, typed registration
df.AddActorBlock<int, string, TransformActor>("transformer");
df.AddProducerBlock<int>("producer");

// Still supports custom blocks
df.AddBlock("custom", sp => {
    var block = sp.GetRequiredService<MyCustomBlock>();
    block.Name = "custom";
    return block;
});

// Graph registration unchanged
df.AddGraph("main", g => g
    .UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer"));
```

---

## Summary of Recommendations

| Problem | Recommended Solution | Key Points |
|---------|---------------------|------------|
| 1. Parallel Builders | Enhance existing DataFlowGraphBuilder | Single builder, optional DI support |
| 2. Lifetime Scope | Separate methods with scoped default | AddScopedBlock (default), AddSingletonBlock, AddTransientBlock |
| 3. Idempotence | Throw on duplicate blocks | Clear errors, idempotent infrastructure |
| 4. Integration | AddGraph() method + dynamic support | Unified registration, flexible runtime |
| 5. Namespace Collisions | Optional namespace prefix | Modular monolith support, cross-namespace references |
| 6. Block Name Duplication | Remove name from constructor + typed helpers | DI-friendly, no duplication, clean API |

---

## Next Steps

1. Create prototype implementation of recommendations
2. Test edge cases and scenarios
3. Validate performance
4. Document final API specification
5. Create updated ADR
