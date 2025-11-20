# Approach 1: Constructor Injection with IBlockContext

## Overview

Pass IBlockContext as a constructor parameter instead of calling SetContext after construction.

## Proposed Changes

### 1. Update BlockBase Constructor

**Current**:
```csharp
protected BlockBase()
{
    // Context will be set by registration infrastructure
}

internal void SetContext(IBlockContext context)
{
    ArgumentNullException.ThrowIfNull(context);
    if (_context is not null)
    {
        throw new InvalidOperationException("Block context has already been set");
    }
    _context = context;
}
```

**Proposed**:
```csharp
protected BlockBase(IBlockContext context)
{
    ArgumentNullException.ThrowIfNull(context);
    _context = context;
}

// No SetContext method needed
```

### 2. Update ActorBlock Constructor

**Current**:
```csharp
public ActorBlock(IServiceScopeFactory scopeFactory)
    : base()
{
    _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
}
```

**Proposed**:
```csharp
public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
    : base(context)
{
    _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
}
```

### 3. Update AddActorBlock Registration

**Current**:
```csharp
_services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
{
    var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
    var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
    var context = new BlockContext(blockName);
    block.SetContext(context);
    return block;
});
```

**Proposed - Option A (Factory in Lambda)**:
```csharp
_services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
{
    var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
    var context = new BlockContext(blockName);
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var block = new ActorBlock<TIn, TOut, TActor>(context, scopeFactory);
    return block;
});
```

**Proposed - Option B (Resolve Context from DI)**:
```csharp
// First register IBlockContext as a scoped service with the block name
_services.AddKeyedScoped<IBlockContext>(fullKey, (sp, key) =>
{
    var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
    return new BlockContext(blockName);
});

// Then register block, resolving context from keyed services
_services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
{
    var context = sp.GetRequiredKeyedService<IBlockContext>(key);
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var block = new ActorBlock<TIn, TOut, TActor>(context, scopeFactory);
    return block;
});
```

## Analysis

### Pros

1. **True Constructor Injection**
   - Context available immediately during construction
   - Immutable after construction
   - No null state

2. **Removes SetContext Method**
   - Simpler BlockBase implementation
   - One less internal method
   - No two-phase initialization

3. **More Explicit**
   - Constructor signature shows all dependencies
   - Clear that context is required
   - Better for developers new to the codebase

4. **Enables Constructor Logic**
   - Could use context.BlockName in constructor
   - Could validate context metadata
   - Could initialize based on context

### Cons

1. **Breaking Change**
   - All existing block constructors need updating
   - Cannot maintain backward compatibility easily
   - Migration required for all custom blocks

2. **More Constructor Parameters**
   - ActorBlock now has 2 parameters instead of 1
   - Other blocks would also need context parameter
   - Could be verbose for blocks with many dependencies

3. **DI Container Limitation**
   - Cannot use `sp.GetRequiredService<ActorBlock<...>>()` directly
   - Must construct manually in factory
   - Loses automatic DI resolution

4. **Option B Complexity**
   - Registering both context AND block as keyed services
   - More DI registrations per block
   - Harder to understand what's happening

## Recommendation

**Option A** is simpler but loses automatic DI resolution.

However, for blocks like ActorBlock, we're already using a factory anyway, so we're not losing much. The trade-off is worth it for the cleaner initialization pattern.

## Open Questions

1. How do we handle blocks with many dependencies?
2. Should IBlockContext be first or last parameter?
3. Do we need backward compatibility layer?
4. What about ProducerBlock and other block types?
