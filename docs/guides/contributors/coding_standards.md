# Getting Started with Codebase

This guide provides essential coding standards and patterns for working with the codebase.

---

## C# Coding Style

The solution follows modern C# practices and conventions:

### Namespace and Type Declarations
- Use **file-scoped namespaces** (e.g `namespace Foo;`)
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

Follow these patterns:

### Core Principles
- All data processing operations are async wherver possible
- Use `IAsyncEnumerable<T>` for streaming operations
- Use `ValueTask<T>` for hot-path operations where appropriate
- Always respect `CancellationToken` - pass it through all async operations
- Understand the effect of `ConfigureAwait(false)` in library code and use if deemed applicable, if not sure, ask.

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

Use appropriate attributes to categorize tests - we use attributes from `xunit.categories` dependency which provides a range includign:

- `[UnitTest]` - Fast, isolated unit tests
- `[IntegrationTest]` - Tests involving multiple components
- `[Category("Performance")]` - Performance/benchmark tests
- `[Exploratory]` - Exploratory or diagnostic tests

### Test Pattern

Follow established pattern for test classes. If unable to see one use:

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
        // add services / dependencies that will be applicable for all test methods in this test class
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }
    
    public IServiceCollection Services { get; }
    public ITestOutputHelper Output => _testOutputHelper;

    // in test methods, consider whether to use test substitution library NSubstitute to replace specific depenencies. 
    // Additionally for integration testing, can use Services.AddXyz then .BuildServiceProvider and use to resolve test subject with services injected.
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

Use centralized package version management.

**For adding new packages or updating versions**, see guies in `/docs/guides/contributors/*`

**Quick Reference:**
- All package versions defined in `src/Directory.Packages.props`
- Project files reference packages WITHOUT versions
- Build errors about "PackageVersion" indicate you need to use central management

---

## Don't Do

- Don't add new external dependencies without careful consideration
- Don't break established architecture principles
- Don't use synchronous blocking operations in async code paths
- Don't ignore cancellation tokens
- Don't modify working code without tests that validate the changes
- Don't use `Task.Result` or `.Wait()` - always use `await`
- Don't create new subsystems without clarifying the design first

---