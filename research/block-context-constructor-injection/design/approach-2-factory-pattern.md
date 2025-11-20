# Approach 2: Factory Pattern with IBlockFactory<T>

## Overview

Introduce a factory interface that creates blocks with context already injected.

## Proposed Implementation

### 1. Define IBlockFactory Interface

```csharp
public interface IBlockFactory<TBlock> where TBlock : IBlock
{
    TBlock Create(IBlockContext context);
}
```

### 2. Implement Factory for ActorBlock

```csharp
public class ActorBlockFactory<TIn, TOut, TActor> : IBlockFactory<ActorBlock<TIn, TOut, TActor>>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public ActorBlockFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public ActorBlock<TIn, TOut, TActor> Create(IBlockContext context)
    {
        return new ActorBlock<TIn, TOut, TActor>(context, _scopeFactory);
    }
}
```

### 3. Update AddActorBlock

```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
    where TActor : IStreamActor<TIn, TOut>
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    // Register the factory as scoped
    _services.TryAddScoped<ActorBlockFactory<TIn, TOut, TActor>>();

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        var factory = sp.GetRequiredService<ActorBlockFactory<TIn, TOut, TActor>>();
        return factory.Create(context);
    });

    return this;
}
```

## Analysis

### Pros

1. **Separation of Concerns**
   - Factory handles block creation logic
   - Registration just wires things together
   - Testable factory logic

2. **Flexibility**
   - Factory can have complex initialization logic
   - Can inject multiple dependencies
   - Can be customized per block type

3. **No Breaking Changes**
   - Existing blocks don't need modification
   - Can introduce gradually
   - Backward compatible

### Cons

1. **More Classes**
   - One factory class per block type
   - More code to maintain
   - Increases complexity

2. **Registration Overhead**
   - Must register both factory and block
   - More services in DI container
   - Slightly worse performance

3. **Indirection**
   - One more layer between registration and block
   - Harder to trace what's happening
   - More abstractions to learn

## Recommendation

This approach is more complex than needed for the simple case of passing a context. The factory pattern is better suited for scenarios with complex construction logic, which we don't have here.

**Rating**: ⭐⭐ (2/5) - Too complex for this use case
