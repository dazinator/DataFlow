# GitHub Copilot Instructions for DataFlow

## Repository Overview

This is **DataFlow** - a high-performance, pull-based data processing pipeline library built on modern .NET features using `System.Threading.Channels`. It provides a fluent API for building concurrent data processing pipelines with efficient backpressure handling.

## Key Architecture Principles

### Pull-Based Architecture
- The library uses a pull-based model where downstream blocks pull data from upstream blocks when ready
- This provides natural backpressure - if a downstream block is slow, upstream blocks automatically slow down
- All blocks communicate via `System.Threading.Channels` for efficient async data flow

### Block System
The library is built around modular blocks that compose into pipelines:
- **Source Blocks** (e.g., `ProducerBlock`, `InputChannelBlock`) - supply data into the flow
- **Propagator Blocks** (e.g., `TransformBlock`, `BatchBlock`, `ProjectorBlock`) - process and pass data onwards
- **Target Blocks** (e.g., `ProcessorBlock`, `OutputBlock`) - terminal blocks that consume data

### Dependency Injection & Scoping
- First-class DI support throughout the pipeline
- Blocks use an "Actor" model where each concurrent worker runs in its own DI scope
- This allows scoped dependencies (like `DbContext`) to be used safely in concurrent processing
- Always use constructor injection for dependencies in `IProducer<T>`, `IProcessor<T>`, `ITransformer<TIn, TOut>`, etc.

## Code Organization

### Source Structure
```
src/
├── DataFlow/                    # Main library
│   ├── Blocks/                 # Block implementations
│   ├── Builder/                # Fluent builder API
│   ├── Actor/                  # Actor-based execution model
│   └── Metrics/                # Metrics and monitoring
├── DataFlow.OpenTelemetry/     # OpenTelemetry integration
├── Tests/                      # Unit and integration tests
├── Tests.Shared/               # Shared test utilities
└── Benchmarks/                 # Performance benchmarks
```

### Key Namespaces
- `Uniun.DataFlow` - Main library namespace
- `Uniun.DataFlow.Builder` - Fluent builder API
- `Uniun.DataFlow.Blocks` - Block implementations
- `Uniun.DataFlow.Metrics` - Metrics and monitoring

## Coding Standards

### C# Style
- Use **file-scoped namespaces** (`namespace Uniun.DataFlow;`)
- Use **var** for local variable declarations when type is apparent
- Prefer **expression-bodied members** where appropriate
- Use **implicit object creation** when type is apparent (`new()` instead of `new Type()`)
- Use **primary constructors** for simple cases
- Follow the `.editorconfig` rules in `src/.editorconfig`
- 4 spaces for indentation
- Place `using` directives **inside namespace** (except for global usings)

### Async/Await Patterns
- All data processing operations are async
- Use `IAsyncEnumerable<T>` for streaming operations
- Use `ValueTask<T>` for hot-path operations where appropriate
- Always respect `CancellationToken` - pass it through all async operations
- Use `ConfigureAwait(false)` in library code (not in test code)

### Testing Standards

#### Test Organization
Tests are in the `Tests` project and follow these conventions:

#### Test Categories
Use attributes to categorize tests:
- `[UnitTest]` - Fast, isolated unit tests
- `[IntegrationTest]` - Tests that involve multiple components or external dependencies
- `[Category("Performance")]` - Performance/benchmark tests
- `[Exploratory]` - Exploratory or diagnostic tests

#### Test Setup Pattern
Tests should use this pattern:
```csharp
public class MyBlockTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public MyBlockTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }
    
    public IServiceCollection Services { get; }
    public ITestOutputHelper Output => _testOutputHelper;
}
```

#### Test Utilities
Reuse existing test helpers from `Tests.Shared`:
- **Processors**: `TestProcessor<T>` - configurable delay, callbacks, error injection
- **Producers**: 
  - `TestProducer<T>` - yields items with configurable delay and callbacks
  - `ConcurrencyTestProducer<T>` - tracks concurrency during production
  - `ErrorProducer<T>` - can throw errors based on predicates
- **Transformers**:
  - `TestProjector` - projects one item to many with callbacks
  - `PassthroughTransformer` - identity transformation
  - `NumberTransformer` - transforms numbers to strings

#### Test Naming
- Use descriptive test method names: `Should_ProcessItemsConcurrently_When_MaxConcurrencyIsSet()`
- Arrange-Act-Assert pattern
- Use `Shouldly` assertions: `result.ShouldBe(expected)`, `list.ShouldContain(item)`

## Building and Testing

### .NET SDK Version
- Target: .NET 8.0
- SDK requirement defined in `src/global.json`
- Use `dotnet tool restore` to restore required tools

### Build Commands
```bash
# Restore tools
dotnet tool restore

# Build
dotnet build

# Run tests
dotnet test

# Run specific test category
dotnet test --filter "Category=UnitTest"
```

### Linting and Formatting
- The project uses `dotnet format`
- Format code before committing: `dotnet format`
- EditorConfig rules are enforced

## Common Patterns

### Defining a Data Flow
```csharp
public class MyFlowConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        builder
            .AddProducer<int>("source", sp => sp.GetRequiredService<MyProducer>())
            .AddBatch<int>("batcher", maxBatchSize: 100, windowPeriod: TimeSpan.FromSeconds(5))
            .ReceiveFrom("source")
            .AddProcessor<int[]>("writer", sp => sp.GetRequiredService<MyBatchWriter>())
            .ReceiveFrom("batcher");
    }
}
```

### Registering and Running
```csharp
// Register
services.AddDataFlows(maxConcurrentFlows: 4);
services.AddDataFlow<MyFlowConfig>();

// Execute
var executor = serviceProvider.GetRequiredService<FlowExecutor<MyFlowConfig>>();
await executor.ExecuteAsync(context, cancellationToken);
```

### Implementing Block Dependencies
```csharp
public class MyProducer : IProducer<int>
{
    private readonly ILogger<MyProducer> _logger;
    
    public MyProducer(ILogger<MyProducer> logger) => _logger = logger;
    
    public async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Delay(10, cancellationToken);
        }
    }
}
```

## Metrics and Monitoring

- Built-in metrics support via `IDataFlowMetrics`
- OpenTelemetry integration available in `DataFlow.OpenTelemetry` package
- Channel buffer utilization, throughput, and processing time metrics
- Use `DataFlowMetricsTagsContext` for contextual tags

## Performance Considerations

- **Concurrency**: Each block can have configurable max concurrency via actor pools
- **Backpressure**: Automatically handled via bounded channels
- **Memory**: Blocks use object pooling where appropriate (e.g., `BatchBlock`)
- **Order Preservation**: 
  - Single transformer preserves order
  - Multiple concurrent transformers may interleave results
  - Batch blocks maintain order

## License

This is a **dual-licensed** project:
- **Non-Commercial**: AGPL-3.0
- **Commercial**: Requires separate license (contact repository owner)

When suggesting code changes, ensure they are compatible with AGPL-3.0 requirements.

## Don't Do

- Don't add new external dependencies without careful consideration
- Don't break the pull-based architecture principles
- Don't use synchronous blocking operations in async code paths
- Don't ignore cancellation tokens
- Don't modify working code without tests that validate the changes
- Don't use `Task.Result` or `.Wait()` - always use `await`
- Don't create new block types without discussing the design first

## When Making Changes

1. **Understand the flow**: Data flows from source → propagators → targets
2. **Test concurrency**: Many bugs only appear under concurrent load
3. **Check metrics**: Ensure metrics are properly tracked for new blocks
4. **Validate backpressure**: Ensure bounded channels work correctly
5. **Document new blocks**: Update README.md with new block types
6. **Add benchmarks**: Performance-sensitive changes should have benchmarks
7. **Follow the actor pattern**: New concurrent blocks should use the actor model

## Helpful Context

- The library is similar to TPL Dataflow but with modern .NET features
- Performance is competitive with TPL Dataflow (within ~10%)
- The builder pattern is fluent and chainable - maintain this style
- Blocks are connected via `.ReceiveFrom()` - this creates the pipeline topology
- The library emphasizes correctness and composability over raw speed
