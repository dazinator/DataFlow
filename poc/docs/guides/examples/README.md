# Testing Examples

This directory contains complete, working examples of modern DataFlow test patterns.

## Available Examples

### ModernEpochGraphTestExample.cs

**Purpose**: Demonstrates the modern, recommended approach for testing epoch-based DataFlow graphs.

**Key Patterns Shown**:
- ✅ Using `AddDataFlows()` which auto-registers `IEpochCoordinator`
- ✅ Using `AddSourceBlock<T, TActor>()` for simple source registration
- ✅ Using `AddActorBlock<TIn, TOut, TActor>()` for actor registration
- ✅ Clean DI-based graph construction
- ✅ Proper resource cleanup with `DisposeAsync()`
- ✅ Using `IAsyncLifetime` for test lifecycle management
- ✅ Reusable `CollectorActor<T>` pattern

**Use this when**:
- You're writing new tests for epoch-based graphs
- You're migrating from the old manual setup pattern
- You need a template for testing source actors and stream actors

## Migrating from Old Patterns

If you have existing test code that looks like this:

```csharp
// ❌ OLD PATTERN - Verbose and deprecated
var coordinator = new EpochCoordinator(scopeFactory);
services.AddSingleton<IEpochCoordinator>(coordinator);

var sourceBlock = new EpochSourceBlock<int, MySource>(
    new BlockContext("source"),
    scopeFactory,
    coordinator);

var builder = new DataFlowGraphBuilder("test");  // Shows deprecation warning
builder.AddBlock(sourceBlock);
// ...
```

**Replace it with the modern pattern** shown in `ModernEpochGraphTestExample.cs`:

```csharp
// ✅ MODERN PATTERN - Clean and simple
services.AddDataFlows("test", df =>
{
    df.AddSourceBlock<int, MySource>("source");
    df.AddActorBlock<int, int, MyActor>("processor");
    
    df.AddGraph("main", g =>
    {
        g.UseBlock("source")
         .UseBlock("processor")
         .Connect("source", "processor");
    });
});
```

## Additional Resources

- **Testing Guide**: See `/poc/docs/guides/testing-guide.md` for comprehensive testing documentation
- **Getting Started**: See `/poc/docs/guides/getting-started.md` for introductory examples
- **Source Blocks Guide**: See `/poc/docs/guides/source-blocks.md` for source actor patterns
- **Working with Blocks**: See `/poc/docs/guides/working-with-blocks.md` for block patterns

## Running These Examples

These examples are provided as reference code. To run them in your own project:

1. Copy the example file to your test project
2. Update namespaces to match your project
3. Ensure you have the required dependencies:
   - `DataFlow.POC.Core`
   - `DataFlow.POC.DependencyInjection`
   - `Microsoft.Extensions.DependencyInjection`
   - `xunit` (or your preferred test framework)

## Common Issues

### Issue: "Type 'DataFlowGraphBuilder' could not be found"

**Solution**: Use `AddDataFlows()` extension method instead of direct instantiation.

### Issue: "IEpochCoordinator is not registered"

**Solution**: `AddDataFlows()` automatically registers it. Don't manually register.

### Issue: "Deprecated constructor warning"

**Solution**: Use the builder pattern via `AddDataFlows()` lambda parameter.

For more troubleshooting, see the "Common Migration Issues" section in `/poc/docs/guides/testing-guide.md`.
