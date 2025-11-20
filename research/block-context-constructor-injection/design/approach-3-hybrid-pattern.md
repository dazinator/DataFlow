# Approach 3: Hybrid - Optional Constructor + SetContext Fallback

## Overview

Make IBlockContext an optional constructor parameter, falling back to SetContext for blocks that don't have it.

## Proposed Implementation

### 1. Update BlockBase to Support Both Patterns

```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private IBlockContext? _context;

    // New: Constructor with context (preferred)
    protected BlockBase(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    // Legacy: Parameterless constructor (backward compatible)
    protected BlockBase()
    {
        // Context will be set via SetContext
    }

    // SetContext only works if context not already set via constructor
    internal void SetContext(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
        {
            throw new InvalidOperationException("Block context has already been set via constructor");
        }
        _context = context;
    }

    public string Name => _context?.BlockName ?? string.Empty;
    // ... rest of implementation
}
```

### 2. Smart Registration that Detects Constructor Pattern

```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        
        // Try constructor with context first
        var blockType = typeof(ActorBlock<TIn, TOut, TActor>);
        var contextConstructor = blockType.GetConstructor(new[] { typeof(IBlockContext), typeof(IServiceScopeFactory) });
        
        if (contextConstructor != null)
        {
            // New pattern: context via constructor
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return (IBlock)contextConstructor.Invoke(new object[] { context, scopeFactory });
        }
        else
        {
            // Legacy pattern: context via SetContext
            var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
            block.SetContext(context);
            return block;
        }
    });

    return this;
}
```

### 3. ActorBlock Can Use Either Constructor

```csharp
// New preferred constructor
public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
    : base(context)
{
    _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
}

// Legacy constructor (can be deprecated)
public ActorBlock(IServiceScopeFactory scopeFactory)
    : base()
{
    _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
}
```

## Analysis

### Pros

1. **Gradual Migration**
   - Old blocks keep working
   - New blocks can use constructor injection
   - No breaking changes

2. **Best of Both Worlds**
   - Constructor injection when available
   - Fallback for compatibility
   - Flexible

3. **Opt-In Improvement**
   - Teams can migrate at their own pace
   - Can test new pattern incrementally
   - Lower risk

### Cons

1. **Reflection Usage**
   - Uses GetConstructor and Invoke
   - Performance overhead (though cached)
   - More complex than direct call

2. **Two Code Paths**
   - Must maintain both patterns
   - More testing required
   - Potential for bugs

3. **Technical Debt**
   - Eventually want to remove old pattern
   - Increases complexity temporarily
   - Confusion about which pattern to use

4. **Runtime Discovery**
   - Constructor detection happens at runtime
   - Could fail in unexpected ways
   - Not compile-time safe

## Recommendation

This adds complexity and runtime overhead to support backward compatibility. The reflection usage here is MORE complex than the current (non-existent) reflection issue mentioned in the problem statement.

**Rating**: ⭐⭐ (2/5) - Introduces unnecessary complexity

## Better Alternative

If we want backward compatibility, better to:
1. Mark SetContext pattern as obsolete with warning
2. Provide migration guide
3. Do breaking change in next major version
4. Keep it simple - one pattern, not two
