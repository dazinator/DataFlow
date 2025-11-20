# Before/After Comparison

## Code Changes Required

### BlockBase.cs

**BEFORE (Current)**:
```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private IBlockContext? _context;

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

    public string Name => _context?.BlockName ?? string.Empty;
    // ...
}
```

**AFTER (Proposed)**:
```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private readonly IBlockContext _context;

    protected BlockBase(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    // No SetContext method
    
    public string Name => _context.BlockName;
    // ...
}
```

**Changes**:
- ✅ Remove parameterless constructor
- ✅ Add constructor with IBlockContext parameter
- ✅ Remove SetContext method
- ✅ Make _context readonly (immutable)
- ✅ Remove null check in Name property

---

### ActorBlock.cs

**BEFORE (Current)**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ActorBlock(IServiceScopeFactory scopeFactory)
        : base()
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }
    // ...
}
```

**AFTER (Proposed)**:
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
    // ...
}
```

**Changes**:
- ✅ Add IBlockContext as first parameter
- ✅ Pass context to base constructor
- ✅ No other changes needed

---

### ServiceCollectionExtensions.cs - AddActorBlock

**BEFORE (Current)**:
```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.TryAddScoped<ActorBlock<TIn, TOut, TActor>>();

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        block.SetContext(context);
        return block;
    });

    return this;
}
```

**AFTER (Proposed)**:
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
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new ActorBlock<TIn, TOut, TActor>(context, scopeFactory);
    });

    return this;
}
```

**Changes**:
- ✅ Remove `TryAddScoped<ActorBlock<...>>()` line (not needed anymore)
- ✅ Remove `sp.GetRequiredService<ActorBlock<...>>()` call
- ✅ Remove `block.SetContext(context)` call
- ✅ Add `sp.GetRequiredService<IServiceScopeFactory>()`
- ✅ Construct block directly with `new ActorBlock<...>(context, scopeFactory)`

---

### ProducerBlock.cs (New typed helper needed)

**BEFORE (Current)**:
```csharp
public class ProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    public ProducerBlock(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(name)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }
    // ...
}
```

**AFTER (Proposed)**:
```csharp
public class ProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    public ProducerBlock(IBlockContext context, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(context)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }
    // ...
}
```

**Changes**:
- ✅ Change constructor parameter from `string name` to `IBlockContext context`
- ✅ Pass context to base constructor

**New Registration Method**:
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

---

## API Usage Changes

### For Library Users

**BEFORE**:
```csharp
services.AddDataFlows("global", df =>
{
    df.AddActorBlock<int, string, MyActor>("transformer");
});
```

**AFTER**:
```csharp
services.AddDataFlows("global", df =>
{
    df.AddActorBlock<int, string, MyActor>("transformer");
});
```

**Result**: ✅ **NO CHANGE for library users!**

The API stays exactly the same. The change is internal to how blocks are constructed.

---

### For Custom Block Authors

**BEFORE**:
```csharp
public class MyCustomBlock : BlockBase<int, string>
{
    private readonly IMyService _service;

    public MyCustomBlock(string name, IMyService service)
        : base(name)
    {
        _service = service;
    }
}

// Usage
df.AddBlock("custom", sp => new MyCustomBlock("custom", sp.GetRequiredService<IMyService>()));
```

**AFTER**:
```csharp
public class MyCustomBlock : BlockBase<int, string>
{
    private readonly IMyService _service;

    public MyCustomBlock(IBlockContext context, IMyService service)
        : base(context)
    {
        _service = service;
    }
}

// Usage
df.AddBlock("custom", sp => 
{
    var context = new BlockContext("custom");
    return new MyCustomBlock(context, sp.GetRequiredService<IMyService>());
});
```

**Result**: ⚠️ **Minor change for custom block authors**
- Constructor parameter changes from `string name` to `IBlockContext context`
- Registration code needs to create BlockContext

---

## Summary of Impact

| Group | Impact | Migration Required |
|-------|--------|-------------------|
| **Library Users** (using typed helpers) | ✅ None | No |
| **Actor Implementers** | ✅ None | No |
| **Custom Block Authors** | ⚠️ Minor | Yes - constructor signature |
| **Library Maintainers** | ⚠️ Moderate | Yes - all block types |

---

## Benefits Achieved

1. **✅ Cleaner initialization**: Context set during construction, not after
2. **✅ Immutable state**: Context is readonly, cannot be changed
3. **✅ No SetContext method**: Simpler BlockBase implementation
4. **✅ No null states**: Name property always valid
5. **✅ Better DI alignment**: All dependencies via constructor
6. **✅ Terse API maintained**: Users see no difference
7. **✅ No reflection**: Direct constructor calls (though current approach doesn't use reflection either)

---

## Migration Complexity

**Low to Medium**:
- Core blocks (5-10 classes) need constructor updates
- Registration methods (1-3 currently) need updates
- Tests need updates
- Custom blocks need migration guide
- Can provide temporary backward compatibility via obsolete constructors
