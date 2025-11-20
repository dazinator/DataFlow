# Approach 4: Dedicated Registration Methods Per Block Type (RECOMMENDED)

## Overview

Provide a dedicated registration extension method for each block type (AddProducerBlock, AddTransformBlock, AddActorBlock, etc.). Each method handles its specific constructor signature and dependencies, passing IBlockContext as a constructor parameter.

## Key Insight

The current `AddActorBlock` is already a typed helper that knows about ActorBlock's specific needs. We can extend this pattern to:
1. Pass IBlockContext via constructor
2. Resolve other dependencies automatically
3. Keep the API terse (one name, one line)
4. Eliminate SetContext entirely

## Proposed Implementation

### 1. Update BlockBase to Require Context in Constructor

```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private readonly IBlockContext _context;

    protected BlockBase(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public string Name => _context.BlockName;
    
    // No SetContext method
    // No nullable context
    // No two-phase initialization
}
```

### 2. Update ActorBlock Constructor

```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }
}
```

### 3. Update AddActorBlock to Pass Context via Constructor

```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        // Create context from key
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        
        // Resolve dependencies and construct block
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new ActorBlock<TIn, TOut, TActor>(context, scopeFactory);
    });

    return this;
}
```

### 4. Add Similar Methods for Other Block Types

**AddProducerBlock**:
```csharp
public DataFlowBuilder AddProducerBlock<T>(
    string name,
    Func<IExecutionContext, IAsyncEnumerable<T>> producer)
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        return new ProducerBlock<T>(context, producer);
    });

    return this;
}
```

**AddTransformBlock**:
```csharp
public DataFlowBuilder AddTransformBlock<TIn, TOut>(
    string name,
    Func<TIn, TOut> transform)
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        return new TransformBlock<TIn, TOut>(context, transform);
    });

    return this;
}
```

**AddProcessorBlock**:
```csharp
public DataFlowBuilder AddProcessorBlock<T>(
    string name,
    Func<T, IExecutionContext, Task> processor)
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        return new ProcessorBlock<T>(context, processor);
    });

    return this;
}
```

### 5. Update Block Constructors

**ProducerBlock**:
```csharp
public class ProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    public ProducerBlock(
        IBlockContext context,
        Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(context)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }
}
```

## API Usage Examples

### Current API (would still work for generic AddBlock)
```csharp
services.AddDataFlows("global", df =>
{
    // Generic registration (for custom blocks)
    df.AddBlock("custom", sp => new MyCustomBlock("custom"));
    
    // Typed helper (ActorBlock only)
    df.AddActorBlock<int, string, TestActor>("transformer");
});
```

### Proposed API
```csharp
services.AddDataFlows("global", df =>
{
    // Typed helpers for all common block types
    df.AddProducerBlock("source", ctx => ProduceInts(ctx));
    df.AddActorBlock<int, string, TestActor>("transformer");
    df.AddTransformBlock<string, int>("parser", s => int.Parse(s));
    df.AddProcessorBlock("sink", (item, ctx) => ProcessAsync(item, ctx));
    
    // Generic registration for custom blocks (requires context in constructor)
    df.AddBlock("custom", sp => 
    {
        var context = new BlockContext("custom");
        return new MyCustomBlock(context);
    });
});
```

## Migration Path

### For Library Authors

1. **Phase 1: Add new constructors, keep old ones**
   ```csharp
   // New constructor
   public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
       : base(context) { ... }
   
   // Old constructor (marked obsolete)
   [Obsolete("Use constructor with IBlockContext parameter")]
   public ActorBlock(IServiceScopeFactory scopeFactory)
       : base() { ... }
   ```

2. **Phase 2: Update AddActorBlock to use new constructor**
   - Tests will catch any issues
   - Old manual registrations still work

3. **Phase 3: Remove obsolete constructors in next major version**
   - Clear migration guide provided
   - Compile-time errors guide users to fix

### For Library Users

#### Before (current):
```csharp
public class MyActor : IStreamActor<int, string>
{
    private readonly ILogger _logger;
    
    public MyActor(ILogger logger)
    {
        _logger = logger;
    }
}

// Registration
df.AddActorBlock<int, string, MyActor>("processor");
```

#### After (with context):
```csharp
// Actor doesn't change - it doesn't need context
public class MyActor : IStreamActor<int, string>
{
    private readonly ILogger _logger;
    
    public MyActor(ILogger logger)
    {
        _logger = logger;
    }
}

// Registration stays the same
df.AddActorBlock<int, string, MyActor>("processor");
```

**Key Point**: For ActorBlock, the TActor doesn't need context. Only the ActorBlock itself needs it. So users don't need to change their actor implementations!

## Analysis

### Pros

1. **✅ Clean Constructor Injection**
   - Context passed in constructor
   - No SetContext method needed
   - Immutable state

2. **✅ Terse API Maintained**
   - One line per block
   - Name specified once
   - Type inference works

3. **✅ No Reflection**
   - Direct constructor calls
   - Compile-time safe
   - Better performance

4. **✅ Typed Helpers Scale Well**
   - One method per block type
   - Each handles its own needs
   - Easy to add new block types

5. **✅ Minimal User Impact**
   - Actors don't need to change
   - Registration code stays same
   - Only library internals change

6. **✅ Consistent Pattern**
   - All blocks constructed the same way
   - Same pattern everywhere
   - Easy to understand

### Cons

1. **⚠️ Breaking Change for Custom Blocks**
   - Custom blocks need to add context parameter
   - BlockBase no longer has parameterless constructor
   - Migration required

2. **⚠️ More Registration Methods**
   - Need AddProducerBlock, AddTransformBlock, etc.
   - More API surface area
   - More documentation needed

3. **⚠️ Cannot Use sp.GetRequiredService<ActorBlock<...>>()**
   - Must construct manually in factory
   - Loses automatic DI resolution
   - But we're already doing this, so no change

### Trade-offs

| Aspect | Current | Proposed |
|--------|---------|----------|
| Constructor parameters | 1 (scopeFactory) | 2 (context, scopeFactory) |
| Context availability | After construction | During construction |
| SetContext method | Yes (internal) | No |
| Null context state | Yes | No |
| Registration code | 8 lines | 7 lines |
| Reflection | None | None |
| API terseness | Terse | Terse |

## Recommendation

**This is the recommended approach** ⭐⭐⭐⭐⭐ (5/5)

### Why This is Best

1. **Aligns with DI principles**: All dependencies via constructor
2. **Simple and direct**: No reflection, no factory pattern, no hybrid complexity
3. **Typed helpers already exist**: Just extend the existing pattern
4. **Minimal user impact**: Most users won't need to change code
5. **Clear migration path**: Can support both constructors temporarily

### Implementation Steps

1. Add new constructors with IBlockContext to all block types
2. Mark old constructors obsolete with helpful message
3. Update typed helper methods to use new constructors
4. Update tests
5. Provide migration guide
6. Remove obsolete constructors in next major version

## Open Questions

1. **Should we provide typed helpers for ALL block types or just the common ones?**
   - Recommendation: Start with ActorBlock, ProducerBlock, TransformBlock
   - Add others as needed

2. **Should IBlockContext be first or last parameter?**
   - Recommendation: First parameter (convention: required dependencies before optional)

3. **Do we need to support metadata in BlockContext?**
   - Current: Metadata property exists but unused
   - Recommendation: Keep it for future extensibility
