# Research: Is CreateActorInputStream Necessary in EpochActorBlock?

**Issue**: [#62](https://github.com/uniun-technology/dataflow/issues/62)  
**Date**: 2025-12-10  
**Status**: Completed

## Executive Summary

**Conclusion**: `CreateActorInputStream` **IS NECESSARY** for correctness when using the DI scope rotation feature.

**Reason**: It maintains a single shared enumerator across multiple actor instances, allowing rotation to continue from where the previous actor left off.

---

## Problem Statement

The question was whether `EpochActorBlock.CreateActorInputStream()` is necessary for safety/correctness or if it's an unnecessary call.

```csharp
private async IAsyncEnumerable<TOut> ProcessEpochItems(...)
{
    await using var inputEnumerator = epochStream.Items.GetAsyncEnumerator(cancellationToken);

    while (!cancellationToken.IsCancellationRequested)
    {
        bool rotationRequested = false;

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            // ...
            var actorInput = CreateActorInputStream(inputEnumerator, cancellationToken);  // Is this needed?
            var actorOutput = actor.RunAsync(actorInput, _context);
            // ...
        }
        // ...
    }
}
```

---

## Analysis

### Key Design Pattern: DI Scope Rotation

`EpochActorBlock` supports **DI scope rotation** - the ability for an actor to request a fresh DI scope mid-stream by calling `context.RequestRotation()`. This is critical for:

1. **Memory management** with scoped services (e.g., EF Core `DbContext`)
2. **State isolation** between processing batches
3. **Resource cleanup** without waiting for the entire stream to complete

### The Rotation Mechanism

When an actor requests rotation:
1. The current actor finishes processing and its DI scope is disposed
2. A NEW actor instance is created in a NEW DI scope
3. The new actor continues processing from where the previous actor left off

### Why CreateActorInputStream is Critical

The challenge is that multiple actor instances need to share progress through the input stream:

```csharp
// Line 62: Create enumerator ONCE outside the loop
await using var inputEnumerator = epochStream.Items.GetAsyncEnumerator(cancellationToken);

while (!cancellationToken.IsCancellationRequested)
{
    // Line 68-81: Create NEW actor instance in NEW scope (potentially multiple times)
    await using (var scope = _scopeFactory.CreateAsyncScope())
    {
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // CRITICAL: Wrap the SHARED enumerator
        var actorInput = CreateActorInputStream(inputEnumerator, cancellationToken);
        var actorOutput = actor.RunAsync(actorInput, _context);
        // ...
    }
}
```

**What CreateActorInputStream does**:
```csharp
private static async IAsyncEnumerable<TIn> CreateActorInputStream(
    IAsyncEnumerator<TIn> enumerator,  // ← Shared stateful enumerator
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    while (await enumerator.MoveNextAsync())
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return enumerator.Current;
    }
}
```

It wraps the **shared enumerator** in a fresh `IAsyncEnumerable` that each actor can enumerate.

### Why You Can't Pass `epochStream.Items` Directly

If we tried to pass `epochStream.Items` directly to each actor:

```csharp
// BROKEN: Each actor gets the same IAsyncEnumerable reference
var actorOutput = actor.RunAsync(epochStream.Items, _context);
```

**Problem**: Each actor would call `GetAsyncEnumerator()` on `epochStream.Items`, which creates a **NEW enumerator starting from the beginning**, not continuing from where the previous actor left off.

**Result**: 
- Actor 1 processes items 1-3, requests rotation
- Actor 2 gets `epochStream.Items`, calls `GetAsyncEnumerator()`, and starts over at item 1!
- Infinite loop or duplicate processing

### Comparison with Deprecated ActorBlock

The deprecated `ActorBlock` (for plain streams) does NOT need this wrapper:

```csharp
public override async IAsyncEnumerable<TOut> ExecuteAsync(
    IAsyncEnumerable<TIn> input,
    IExecutionContext context)
{
    await using var scope = _scopeFactory.CreateAsyncScope();
    var actor = scope.ServiceProvider.GetRequiredService<TActor>();
    
    // Direct pass - no wrapper needed
    var output = actor.RunAsync(input, _context);
    
    await foreach (var item in output.WithCancellation(context.CancellationToken))
    {
        yield return item;
    }
}
```

**Why it doesn't need the wrapper**: Plain `ActorBlock` creates the actor ONCE outside any rotation loop. There's only one actor instance for the entire input stream, so no need to share enumerator state across multiple instances.

---

## Validation

### Test 1: Enumerator Wrapping Works Correctly

Created test `Test_Enumerator_Behavior_With_Multiple_Wraps` which validates that:
- Wrapping an enumerator creates a new `IAsyncEnumerable` view
- Multiple wraps can be created from the same enumerator
- Each wrap continues from where the previous one left off
- **Result**: ✅ PASSED

### Test 2: Alternative Without Wrapper

Created `EpochActorBlockAlt` which passes `epochStream.Items` directly instead of using `CreateActorInputStream`.

Created test `EpochActorBlockAlt_Without_CreateActorInputStream_Should_Fail_With_Rotation` to validate the failure.

**Result**: Test **hangs** (as expected) because each actor restarts enumeration from the beginning, creating an infinite loop.

---

## Technical Deep Dive

### IAsyncEnumerable vs IAsyncEnumerator

Understanding the distinction is critical:

| Type | Purpose | State | Reusability |
|------|---------|-------|-------------|
| `IAsyncEnumerable<T>` | Factory for enumerators | Stateless | Can call `GetAsyncEnumerator()` multiple times |
| `IAsyncEnumerator<T>` | Iterator with position | Stateful | Single-use, maintains current position |

**Key insight**: 
- Calling `GetAsyncEnumerator()` on an `IAsyncEnumerable` creates a FRESH enumerator
- To share progress, you need to share the ENUMERATOR, not the enumerable

### Why the Pattern Works

```csharp
// Step 1: Create shared enumerator (stateful)
await using var inputEnumerator = epochStream.Items.GetAsyncEnumerator(cancellationToken);

// Step 2: Inside rotation loop
while (true)
{
    // Step 3: Wrap shared enumerator in fresh IAsyncEnumerable
    var actorInput = CreateActorInputStream(inputEnumerator, cancellationToken);
    
    // Step 4: Actor enumerates, advancing the SHARED enumerator
    var actorOutput = actor.RunAsync(actorInput, _context);
    await foreach (var item in actorOutput)
    {
        yield return item;
    }
    
    // Step 5: If rotation requested, loop continues with same enumerator
    // Next actor picks up where previous left off
}
```

---

## Conclusion

**Answer**: `CreateActorInputStream` **IS NECESSARY** for correctness.

**Reason**: The DI scope rotation feature requires multiple actor instances to share progress through the input stream. This is achieved by:
1. Creating a single shared `IAsyncEnumerator` outside the rotation loop
2. Wrapping it in a fresh `IAsyncEnumerable` for each actor via `CreateActorInputStream`
3. Each actor advances the shared enumerator's position
4. Next actor continues from where previous left off

**Without this wrapper**:
- Each actor would create its own enumerator from `epochStream.Items`
- Each enumerator would start from the beginning
- Rotation would fail (infinite loop or duplicate processing)

### Performance Considerations

The wrapper has minimal overhead:
- It's a simple `yield return` loop
- No buffering or copying
- No additional allocations beyond the state machine

### Recommendation

**Keep `CreateActorInputStream` as-is**. It's a necessary implementation detail for the rotation feature to work correctly.

---

## Test Coverage

Existing test coverage is adequate:
- `ActorBlockTests.cs` extensively tests rotation scenarios
- Tests validate that items flow correctly across rotation boundaries
- Tests verify DI scope rotation with scoped services

**Recommendation**: No additional test coverage needed. The existing tests implicitly validate this pattern.

---

## Code References

- **Implementation**: `/poc/DataFlow.POC/Blocks/EpochActorBlock.cs`
- **Tests**: `/poc/DataFlow.POC.Tests/ActorBlockTests.cs`
- **Research Tests**: `/poc/DataFlow.POC.Tests/EpochActorBlockResearchTests.cs`
- **Alternative Implementation**: `/poc/DataFlow.POC/Blocks/EpochActorBlockAlt.cs` (research only)

---

## Related Patterns

This pattern is similar to:
- **Iterator chaining** in LINQ
- **Stream decorators** in Java streams
- **Generator composition** in Python

The key is that you can't re-enumerate an `IAsyncEnumerable` and expect to continue from the same position - you need to share the underlying enumerator.
