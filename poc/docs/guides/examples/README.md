# Testing Examples

This directory contains complete, working examples of DataFlow test patterns.

## Available Examples

### ModernEpochGraphTestExample.cs

**Purpose**: Demonstrates the recommended approach for testing epoch-based DataFlow graphs.

**Key Patterns Shown**:
- ✅ Using `AddDataFlows()` which auto-registers `IEpochCoordinator`
- ✅ Using `AddSourceBlock<T, TActor>()` for source registration
- ✅ Using `AddActorBlock<TIn, TOut, TActor>()` for actor registration
- ✅ Clean DI-based graph construction
- ✅ Proper resource cleanup with `DisposeAsync()`
- ✅ Using `IAsyncLifetime` for test lifecycle management
- ✅ Reusable `CollectorActor<T>` pattern

**Use this when**:
- Writing tests for epoch-based graphs
- Need a template for testing source actors and stream actors
- Want to see proper DI setup and resource management patterns

## Example Usage

```csharp
services.AddDataFlows("test", df =>
{
    df.AddSourceBlock<int, MySource>("source");
    df.AddActorBlock<int, int, MyActor>("processor");
    df.AddActorBlock<int, object, CollectorActor<int>>("collector");
    
    df.AddGraph("main", g =>
    {
        g.UseBlock("source")
         .UseBlock("processor")
         .UseBlock("collector")
         .Connect("source", "processor")
         .Connect("processor", "collector");
    });
});

var serviceProvider = services.BuildServiceProvider();
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("test:main");
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

## Additional Resources

- **Testing Guide**: See `/poc/docs/guides/testing-guide.md` for comprehensive testing documentation
- **Getting Started**: See `/poc/docs/guides/getting-started.md` for introductory examples
- **Source Blocks Guide**: See `/poc/docs/guides/source-blocks.md` for source actor patterns
- **Working with Blocks**: See `/poc/docs/guides/working-with-blocks.md` for block patterns
