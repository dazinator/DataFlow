# Developer Migration Guide: Modernizing DataFlow Tests

## Summary

Your test code is using **deprecated patterns** from an older version of DataFlow. We've created comprehensive documentation and working examples to help you modernize your tests.

## What's Wrong with Your Current Code?

Your test has several issues:

1. ❌ **Deprecated `DataFlowGraphBuilder` constructor** - Shows compiler warning
2. ❌ **Manual `IEpochCoordinator` creation** - No longer necessary
3. ❌ **Manual block instantiation** - Verbose and error-prone
4. ❌ **Manual `BlockContext` creation** - Boilerplate
5. ❌ **Manual `BlockTypeRegistry` management** - No longer needed

**Result**: 30+ lines of setup code, deprecation warnings, hard to maintain

## Modern Solution

We've created:

### 1. **Updated Testing Guide** 
📖 `/poc/docs/guides/testing-guide.md`

New section: "Testing Epoch-Based Graphs" with:
- Modern API documentation
- Before/after comparisons
- Complete working examples
- Common migration issues and solutions

### 2. **Complete Working Example**
📄 `/poc/docs/guides/examples/ModernEpochGraphTestExample.cs`

Production-ready test file showing:
- ✅ Auto-registered `IEpochCoordinator`
- ✅ One-line block registration with `AddSourceBlock` / `AddActorBlock`
- ✅ Clean DI-based setup
- ✅ Proper resource cleanup
- ✅ Three test patterns (basic, terminal, cancellation)
- ✅ Alternative using `IAsyncLifetime`

**This file compiles without errors and serves as a complete template.**

### 3. **Quick Reference**
📄 `/poc/docs/guides/examples/README.md`

Quick migration guide and troubleshooting.

## How to Modernize Your Tests

### Step 1: Replace Your Setup Code

**Before (your code):**
```csharp
// ❌ OLD - 30+ lines of manual setup
var coordinator = new EpochCoordinator(
    new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
services.AddSingleton<IEpochCoordinator>(coordinator);

var sourceBlock = new EpochSourceBlock<int, SimpleIntegerSource>(
    new BlockContext("source"),
    scopeFactory,
    coordinator);

var actorBlock = new EpochActorBlock<int, int, SimpleDoublerActor>(
    new BlockContext("doubler"),
    scopeFactory);

#pragma warning disable CS0618
var builder = new DataFlowGraphBuilder("minimal-test");
#pragma warning restore CS0618

builder.AddBlock(sourceBlock);
builder.AddBlock(actorBlock);
builder.Connect(sourceBlock, actorBlock);

var registry = new BlockTypeRegistry();
var graph = builder.Build(serviceProvider, registry);
```

**After (modern pattern):**
```csharp
// ✅ MODERN - 10 lines, no warnings, auto-wired
services.AddDataFlows("test", df =>
{
    // One-line registrations
    df.AddSourceBlock<int, SimpleIntegerSource>("source");
    df.AddActorBlock<int, int, SimpleDoublerActor>("doubler");
    df.AddActorBlock<int, object, CollectorActor<int>>("collector");
    
    // Define graph topology
    df.AddGraph("main", g =>
    {
        g.UseBlock("source")
         .UseBlock("doubler")
         .UseBlock("collector")
         .Connect("source", "doubler")
         .Connect("doubler", "collector");
    });
});

var serviceProvider = services.BuildServiceProvider();
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("test:main");
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

### Step 2: Use Reusable CollectorActor

Replace your custom collectors with the reusable pattern:

```csharp
public class CollectorActor<T> : IStreamActor<T, object>
{
    private readonly List<T> _results;

    public CollectorActor(List<T> results) => _results = results;

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _results.Add(item);
        }
        yield break;
    }
}
```

Register it:
```csharp
var results = new List<int>();
services.AddScoped(_ => new CollectorActor<int>(results));
```

### Step 3: Copy the Complete Example

**Option 1: Copy the working example**
1. Copy `/poc/docs/guides/examples/ModernEpochGraphTestExample.cs`
2. Adapt namespaces and actor names to your needs
3. Run and verify

**Option 2: Follow the pattern in testing-guide.md**
1. Read the "Testing Epoch-Based Graphs" section
2. Apply the patterns to your existing tests
3. Reference the common migration issues section

## Key Changes from PR #129

Recent improvements (merged in PR #129):

1. **Auto-registered `IEpochCoordinator`**: `AddDataFlows()` now automatically registers `IEpochCoordinator` as a scoped service - no manual registration needed

2. **`AddSourceBlock<T, TActor>()` extension**: Simple one-line source block registration that auto-wires coordinator and scope factory

3. **Scoped coordinator lifetime**: Each graph execution gets its own coordinator instance for proper isolation

## Benefits

✅ **70% less boilerplate**  
✅ **No deprecation warnings**  
✅ **Type-safe and maintainable**  
✅ **Consistent with production patterns**  
✅ **Auto-wired dependencies**  

## Need Help?

1. **Working example**: `/poc/docs/guides/examples/ModernEpochGraphTestExample.cs`
2. **Full guide**: `/poc/docs/guides/testing-guide.md` → "Testing Epoch-Based Graphs"
3. **Quick ref**: `/poc/docs/guides/examples/README.md`
4. **Getting started**: `/poc/docs/guides/getting-started.md`

## Common Issues & Solutions

### "Type 'DataFlowGraphBuilder' could not be found"
**Solution**: Use `services.AddDataFlows("name", df => ...)` instead

### "IEpochCoordinator is not registered"
**Solution**: It's auto-registered by `AddDataFlows()` - don't manually register

### "CS0618: DataFlowGraphBuilder is obsolete"
**Solution**: Use the builder pattern via `AddDataFlows()` lambda

### "ExecutionContext is ambiguous"
**Solution**: Add `using ExecutionContext = DataFlow.POC.Core.ExecutionContext;`

For more, see the "Common Migration Issues" section in the testing guide.

---

**Last Updated**: 2026-02-16  
**Related PR**: #129 - Modern API improvements  
**Target Version**: DataFlow POC (latest)
