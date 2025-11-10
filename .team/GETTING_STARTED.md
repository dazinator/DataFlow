# Getting Started with DataFlow Codebase

This guide provides essential coding standards and patterns for working with the DataFlow codebase. These standards apply to both research and implementation work.

---

## C# Coding Style

DataFlow follows modern C# practices and conventions:

### Namespace and Type Declarations
- Use **file-scoped namespaces** (`namespace Uniun.DataFlow;`)
- Use **var** for local variable declarations when type is apparent
- Prefer **expression-bodied members** where appropriate
- Use **implicit object creation** when type is apparent (`new()`)
- Use **primary constructors** for simple cases

### Code Organization
- Follow `.editorconfig` rules in `src/.editorconfig`
- 4 spaces for indentation
- Place `using` directives **inside namespace** (except global usings)

---

## Async/Await Patterns

DataFlow is built on asynchronous data processing. Follow these patterns:

### Core Principles
- All data processing operations are async
- Use `IAsyncEnumerable<T>` for streaming operations
- Use `ValueTask<T>` for hot-path operations where appropriate
- Always respect `CancellationToken` - pass it through all async operations
- Use `ConfigureAwait(false)` in library code (not in test code)

### Example Pattern
```csharp
public async IAsyncEnumerable<T> ProduceAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    foreach (var item in items)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return await ProcessAsync(item, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

---

## Testing Standards

### Test Categories

Use appropriate attributes to categorize tests:

- `[UnitTest]` - Fast, isolated unit tests
- `[IntegrationTest]` - Tests involving multiple components
- `[Category("Performance")]` - Performance/benchmark tests
- `[Exploratory]` - Exploratory or diagnostic tests

### Test Pattern

Follow this standard pattern for test classes:

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

### Test Naming

Use descriptive names that explain what is being tested:

- **Format**: `Should_[ExpectedBehavior]_When_[Condition]()`
- **Example**: `Should_ProcessItemsConcurrently_When_MaxConcurrencyIsSet()`

### Assertions

Use **Shouldly** for fluent assertions:

- `result.ShouldBe(expected)` - Equality checks
- `list.ShouldContain(item)` - Collection checks
- `action.ShouldThrow<Exception>()` - Exception checks

---

## Package Management

DataFlow uses centralized package version management.

**For adding new packages or updating versions**, see:
- [Central Package Management Guide](./CENTRAL_PACKAGE_MANAGEMENT.md) - Adding new packages, resolving conflicts
- [NuGet Dependency Updates Guide](./NUGET_DEPENDENCY_UPDATES.md) - Updating packages, security fixes

**Quick Reference:**
- All package versions defined in `src/Directory.Packages.props`
- Project files reference packages WITHOUT versions
- Build errors about "PackageVersion" indicate you need to use central management

---

## Common Patterns

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

### Defining Data Flows

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

---

## Performance Considerations

- **Concurrency**: Configurable max concurrency via actor pools
- **Backpressure**: Automatically handled via bounded channels
- **Memory**: Object pooling where appropriate (e.g., `BatchBlock`)
- **Order Preservation**:
  - Single transformer preserves order
  - Multiple concurrent transformers may interleave
  - Batch blocks maintain order

---

## Don't Do

- Don't add new external dependencies without careful consideration
- Don't break the pull-based architecture principles
- Don't use synchronous blocking operations in async code paths
- Don't ignore cancellation tokens
- Don't modify working code without tests that validate the changes
- Don't use `Task.Result` or `.Wait()` - always use `await`
- Don't create new block types without discussing the design first

---

## Additional Resources

- **Repository Overview**: See `.github/copilot-instructions.md` for architecture principles
- **POC Work**: See `/poc/README.md` for POC-specific patterns
- **Documentation Standards**: See [Document Hygiene Guide](/.github/docs/DOCUMENT_HYGIENE.md)
