# CLAUDE.md — DataFlow

## Repository Overview

**DataFlow** is a high-performance, pull-based data processing pipeline library built on modern .NET using `System.Threading.Channels`. It provides a fluent API for building concurrent pipelines with natural backpressure.

### Key Architecture Principles

- **Pull-based**: downstream blocks pull from upstream when ready — never push
- **Backpressure**: automatically handled via bounded `System.Threading.Channels`
- **DI-first**: first-class dependency injection; actor model gives each concurrent worker its own DI scope (safe for `DbContext` etc.)
- **Cancellation**: every async code path must respect `CancellationToken` — never ignore it

### Block Types

| Type | Description |
|------|-------------|
| Source | Supplies data — `BlockBase<object, TOut>`, ignores input |
| Propagator | Transforms/routes data — `BlockBase<TIn, TOut>` |
| Target | Terminal consumer — `BlockBase<TIn, object>`, `yield break` |
| Actor | DI-injected worker — implements `IStreamActor<TIn, TOut>`, registered via `AddActorBlock` |

### Repository Structure

| Path | Contents |
|------|----------|
| `src/` | Production library code |
| `poc/` | POC / evolving architecture (Blazor visualisation etc.) |
| `poc/DataFlow.Blazor/` | Blazor WASM visualisation components |
| `poc/DataFlow.Blazor.Server/` | ASP.NET Core server (SignalR, EF persistence) |
| `poc/DataFlow.Blazor.Shared/` | Shared models / events / snapshots |
| `src/Tests/` | Unit and integration tests |
| `research/` | Research findings and handover documents |
| `.github/` | GitHub configuration and Copilot instructions |

---

## Building and Testing

```bash
# Restore tools
dotnet tool restore

# Build
dotnet build

# Run all tests
dotnet test

# Run a specific category
dotnet test --filter "Category=UnitTest"

# Format
dotnet format
```

- Target framework: **.NET 8.0**
- SDK version pinned in `src/global.json`

---

## Testing Requirements

**Always check test coverage for any change you make.**

- Bug fixes must include a test that would have caught the bug — a regression test proving the fix is correct.
- New features must include tests covering the happy path and key edge cases.
- If an existing test file covers the area you changed, add to it rather than creating a new file.
- Do not modify working code without tests that validate the change.

---

## Coding Standards

- Use `await` everywhere — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Always propagate `CancellationToken` through async call chains
- Do not add external dependencies without clear justification
- Do not break the pull-based architecture (no push-based workarounds)
- Do not use synchronous blocking in async code paths
- Do not create new block types without discussing the design first
- Keep changes minimal and focused — do not refactor surrounding code unless asked
- Do not add comments or docstrings to code you didn't change
- AGPL-3.0 compatibility required for all suggested code

---

## Common Patterns (POC — `poc/` directory)

The POC uses a different API from the legacy `src/` library. The core principles (pull-based, backpressure via channels, DI-first, cancellation) are the same.

### Implementing a Block

All blocks extend `BlockBase<TIn, TOut>` and implement `ExecuteAsync`. Source blocks use `object` as their input type. Terminal sinks use `object` as their output type and `yield break` at the end.

```csharp
// Source block — produces items, ignores input
public sealed class MySourceBlock : BlockBase<object, MyItem>
{
    public MySourceBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<MyItem> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(10, context.CancellationToken);
            yield return new MyItem(i);
        }
    }
}

// Transform / propagator block
public sealed class MyTransformBlock : BlockBase<MyItem, MyResult>
{
    public MyTransformBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<MyResult> ExecuteAsync(
        IAsyncEnumerable<MyItem> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return Process(item);
        }
    }
}

// Terminal sink — consumes items, yields nothing
public sealed class MySinkBlock : BlockBase<MyResult, object>
{
    public MySinkBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<MyResult> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
            await HandleAsync(item, context.CancellationToken);
        yield break;
    }
}
```

### Implementing an Actor (DI-injected worker)

For blocks that need DI dependencies (e.g. `DbContext`), implement `IStreamActor<TIn, TOut>` and register with `AddActorBlock`. Each actor instance runs in its own DI scope.

```csharp
public sealed class MyActor : IStreamActor<MyItem, MyResult>
{
    private readonly IMyService _service;

    public MyActor(IMyService service) => _service = service;

    public async IAsyncEnumerable<MyResult> RunAsync(
        IAsyncEnumerable<MyItem> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
            yield return await _service.ProcessAsync(item, context.CancellationToken);
    }
}
```

### Building a Graph Directly

Used in tests, demos, and one-off executions where DI registration isn't needed.

```csharp
var source    = new MySourceBlock("source");
var transform = new MyTransformBlock("transform");
var sink      = new MySinkBlock("sink");

var graph = new DataFlowGraph("my-flow", logger);
graph.AddBlock(source);
graph.AddBlock(transform);
graph.AddBlock(sink);
graph.AddEdge(new Edge(source, transform));   // bounded buffer, default capacity 100
graph.AddEdge(new Edge(transform, sink));

await graph.ExecuteAsync(executionContext);
```

### Registering a Flow with DI

Used in production / hosted services. Blocks resolve from DI; the graph topology is declared once at startup.

```csharp
services.AddDataFlows("my-namespace", df =>
{
    df.DisplayName("My Processing Flow");

    // Register blocks (resolved per execution scope)
    df.AddActorBlock<MyItem, MyResult, MyActor>("processor");
    df.AddBlock<MySinkBlock>("sink", sp => new MySinkBlock("sink"));

    // Declare graph topology
    df.AddGraph("my-graph", g =>
    {
        g.Connect("processor", "sink");
    });
});
```

### Edge Topologies

```csharp
// Fan-out: broadcast same items to all targets
graph.AddEdge(new Edge(source, new[] { targetA, targetB }, new BroadcastEdgeStrategy(...)));

// Competing consumers: each item goes to exactly one worker
graph.AddEdge(new Edge(source, new[] { worker1, worker2, worker3 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100)));

// Selective routing: route items to targets based on a key
var strategy = new SelectiveRoutingEdgeStrategy<MyItem>(
    new Dictionary<string, IBlock> { ["typeA"] = blockA, ["typeB"] = blockB },
    item => item.Type,
    BufferMode.Bounded, 100);
graph.AddEdge(new Edge(router, new[] { blockA, blockB }, strategy));

// Fan-in: multiple sources feeding one target — just add an Edge per source
graph.AddEdge(new Edge(sourceA, fanInBuffer));
graph.AddEdge(new Edge(sourceB, fanInBuffer));
```

---

## Performance Notes

- Concurrency: configurable via actor pools
- Memory: object pooling used where appropriate (e.g. `BatchBlock`)
- Order: single transformer preserves order; multiple concurrent transformers may interleave
- Performance target: within ~10% of TPL Dataflow

---

## License

Dual-licensed:
- **Non-commercial**: AGPL-3.0
- **Commercial**: separate license required
