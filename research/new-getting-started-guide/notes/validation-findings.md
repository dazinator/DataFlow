# Validation Notes

## Issue 1: Top-Level Statements Order

**Problem**: In .NET 6+, top-level statements must come BEFORE class definitions.

**Impact**: Code example in guide had blocks defined first, causing compile error.

**Solution**: Move main program logic to the top, block definitions to the bottom.

## Issue 2: Method Signature

**Problem**: Guide used wrong signature for `ExecuteAsync`:
- ❌ Wrong: `ExecuteAsync(IAsyncEnumerable<T>, CancellationToken cancellationToken)`  
- ✅ Correct: `ExecuteAsync(IAsyncEnumerable<T>, IExecutionContext context)`

**Impact**: Code wouldn't compile.

**Solution**: Use `IExecutionContext context` parameter, access cancellation via `context.CancellationToken`.

## Corrected Code Structure

```csharp
using System.Runtime.CompilerServices;

// ===== MAIN PROGRAM (must be first) =====
var services = new ServiceCollection();
services.AddDataFlows("app", df => { ... });
// ... rest of main logic ...

// ===== BLOCK DEFINITIONS (must come after) =====
public class MyBlock : BlockBase<TIn, TOut>
{
    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)  // ← Correct parameter
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            // Process...
            yield return result;
        }
    }
}
```

## Time to Complete

**Actual Time**: ~10 minutes to follow guide and identify issues  
**Target**: 15 minutes (guide states)  
**Result**: Close to target, but issues would add time for new developers

## Clarity Issues

1. **Top-level statements**: Guide doesn't mention this .NET feature or proper structure
2. **Method signature**: Easy to miss that it's `IExecutionContext` not `CancellationToken`
3. **Context usage**: Not clear that `context.CancellationToken` is how you access cancellation

## Recommendations

1. Add note about top-level statements and proper file structure
2. Highlight the `IExecutionContext context` parameter
3. Show how to use `context.CancellationToken` in the example
4. Consider providing complete, copy-paste-ready Program.cs
