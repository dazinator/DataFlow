# Testing Guide for DataFlow

This guide provides comprehensive guidance on testing DataFlow pipelines and actors, including best practices, patterns, and examples.

## Table of Contents

1. [Overview](#overview)
2. [Test Helper Utilities](#test-helper-utilities)
3. [Testing Strategies](#testing-strategies)
4. [Common Testing Patterns](#common-testing-patterns)
5. [Testing with Dependencies](#testing-with-dependencies)
6. [Business Logic Decoupling Pattern](#business-logic-decoupling-pattern)
7. [Troubleshooting](#troubleshooting)

---

## Overview

Testing DataFlow pipelines involves several aspects:
- **Unit Testing**: Testing individual actors in isolation
- **Integration Testing**: Testing complete pipelines
- **Dependency Mocking**: Testing actors with external dependencies
- **Error Handling**: Verifying error propagation and handling

### Key Testing Challenges

1. **Boilerplate Code**: Service provider setup, custom collectors, stream creation
2. **Actor Isolation**: Testing business logic separate from orchestration
3. **Dependency Management**: Mocking external dependencies
4. **Async Complexity**: Dealing with `IAsyncEnumerable<T>`

### Test Helper Solution

The `TestHelpers` namespace provides utilities that reduce test boilerplate by **40-60%**:
- `TestServiceBuilder` - Fluent DI setup
- `TestStreams` - Stream creation and collection utilities
- `TestContext` - Execution context creation
- `CollectorActor<T>` - Generic collector
- `TransformActor<TIn, TOut>` - Generic transformer
- `FilterActor<T>` - Generic filter

---

## Test Helper Utilities

### TestServiceBuilder

Reduces service provider setup from 8-10 lines to 1-2 lines.

**Before:**
```csharp
var services = new ServiceCollection();
services.AddScoped<MyActor>();
services.AddScoped<IDependency>(_ => mockDependency);
var serviceProvider = services.BuildServiceProvider();
var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
```

**After:**
```csharp
var scopeFactory = TestServiceBuilder.Create()
    .WithActor<MyActor>()
    .WithScoped(mockDependency)
    .BuildScopeFactory();
```

**API:**
- `Create()` - Creates a new builder
- `WithActor<TActor>()` - Registers actor as transient
- `WithScoped<TService>(instance)` - Registers scoped service with instance
- `WithScoped<TService>(factory)` - Registers scoped service with factory
- `WithSingleton<TService>(instance)` - Registers singleton service
- `Build()` - Builds service provider
- `BuildScopeFactory()` - Builds and returns scope factory

### TestStreams

Stream creation and collection utilities.

**Creating Streams:**
```csharp
// From array
var stream = TestStreams.FromArray(1, 2, 3, 4, 5);

// From list
var stream = TestStreams.FromList(myList);

// Integers 1 to N
var stream = TestStreams.Integers(10); // 1, 2, 3, ..., 10

// Integer range
var stream = TestStreams.Range(10, 20); // 10, 11, 12, ..., 20

// Empty stream
var stream = TestStreams.Empty<int>();
```

**Collecting Results:**
```csharp
var results = await TestStreams.CollectAsync(stream);
```

### TestContext

Simplified context creation.

```csharp
// Create execution context
var context = TestContext.CreateExecution();

// With custom service provider
var context = TestContext.CreateExecution(serviceProvider);

// Create actor execution context
var context = TestContext.CreateActor();

// With cancellation token
var context = TestContext.CreateActor(cancellationToken);
```

### CollectorActor<T>

Generic collector eliminates the need for custom collector implementations.

```csharp
var collected = new List<string>();
var collector = new CollectorActor<string>(collected);

// Use in pipeline or unit test
await foreach (var item in actor.RunAsync(input, context))
{
    // collector accumulates items into 'collected' list
}
```

### Generic Actors

Reusable actors for simple test scenarios.

**TransformActor<TIn, TOut>:**
```csharp
var actor = new TransformActor<int, string>(i => $"Item-{i}");
var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));
```

**FilterActor<T>:**
```csharp
var actor = new FilterActor<int>(i => i % 2 == 0); // Keep even numbers
var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));
```

---

## Testing Strategies

### Unit Testing Actors

Test actors in isolation without pipelines.

```csharp
[Fact]
public async Task Actor_Should_Transform_Items()
{
    // Arrange
    var actor = new MyTransformActor();
    var input = TestStreams.FromArray(1, 2, 3);
    var context = TestContext.CreateActor();

    // Act
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    // Assert
    results.Count.ShouldBe(3);
    results.ShouldBe(new[] { "1", "2", "3" });
}
```

### Integration Testing Pipelines

Test complete pipelines end-to-end.

```csharp
[Fact]
public async Task Pipeline_Should_Process_All_Items()
{
    // Arrange
    var collected = new List<string>();
    
    var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(10));
    
    var transformer = new ActorBlock<int, string, TransformActor<int, string>>(
        "transformer",
        TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"Item-{i}"))
            .BuildScopeFactory());

    var collector = new ActorBlock<string, object, CollectorActor<string>>(
        "collector",
        TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<string>(collected))
            .BuildScopeFactory());

    var builder = new DataFlowGraphBuilder("test-pipeline");
    builder.AddBlock(producer)
        .AddBlock(transformer)
        .Connect(producer, transformer)
        .AddBlock(collector)
        .Connect(transformer, collector);

    var graph = builder.Build();
    var context = TestContext.CreateExecution();

    // Act
    await graph.ExecuteAsync(context);

    // Assert
    collected.Count.ShouldBe(10);
}
```

---

## Common Testing Patterns

### Pattern 1: Simple Transform Test

```csharp
[Fact]
public async Task Transform_Should_Convert_Values()
{
    var actor = new TransformActor<int, string>(i => i.ToString());
    var input = TestStreams.Range(1, 5);
    var context = TestContext.CreateActor();

    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    results.ShouldBe(new[] { "1", "2", "3", "4", "5" });
}
```

### Pattern 2: Filter Test

```csharp
[Fact]
public async Task Filter_Should_Keep_Valid_Items()
{
    var actor = new FilterActor<int>(i => i > 5);
    var input = TestStreams.Range(1, 10);
    var context = TestContext.CreateActor();

    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    results.ShouldBe(new[] { 6, 7, 8, 9, 10 });
}
```

### Pattern 3: Testing Empty Streams

```csharp
[Fact]
public async Task Actor_Should_Handle_Empty_Stream()
{
    var actor = new MyActor();
    var input = TestStreams.Empty<int>();
    var context = TestContext.CreateActor();

    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    results.ShouldBeEmpty();
}
```

### Pattern 4: Testing Cancellation

```csharp
[Fact]
public async Task Actor_Should_Respect_Cancellation()
{
    var cts = new CancellationTokenSource();
    var actor = new MyActor();
    var input = TestStreams.Integers(1000);
    var context = TestContext.CreateActor(cts.Token);

    cts.CancelAfter(TimeSpan.FromMilliseconds(10));

    await Should.ThrowAsync<OperationCanceledException>(async () =>
    {
        await TestStreams.CollectAsync(actor.RunAsync(input, context), cts.Token);
    });
}
```

---

## Testing with Dependencies

### Using NSubstitute for Mocking

NSubstitute provides powerful mocking capabilities with minimal boilerplate.

**Install Package:**
```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
```

### Example: Testing Actor with Service Dependency

```csharp
public interface IValidationService
{
    Task<bool> ValidateAsync(string value, CancellationToken ct);
}

public class ValidationActor : IStreamActor<string, ValidationResult>
{
    private readonly IValidationService _validator;

    public ValidationActor(IValidationService validator)
    {
        _validator = validator;
    }

    public async IAsyncEnumerable<ValidationResult> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            var isValid = await _validator.ValidateAsync(item, context.CancellationToken);
            yield return new ValidationResult(item, isValid);
        }
    }
}

[Fact]
public async Task ValidationActor_Should_Use_Service()
{
    // Arrange - Mock the service
    var mockValidator = Substitute.For<IValidationService>();
    mockValidator.ValidateAsync("valid", Arg.Any<CancellationToken>())
        .Returns(true);
    mockValidator.ValidateAsync("invalid", Arg.Any<CancellationToken>())
        .Returns(false);

    var actor = new ValidationActor(mockValidator);
    var input = TestStreams.FromArray("valid", "invalid", "valid");
    var context = TestContext.CreateActor();

    // Act
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    // Assert
    results.Count.ShouldBe(3);
    results[0].IsValid.ShouldBeTrue();
    results[1].IsValid.ShouldBeFalse();
    results[2].IsValid.ShouldBeTrue();

    // Verify service was called correctly
    await mockValidator.Received(3).ValidateAsync(
        Arg.Any<string>(),
        Arg.Any<CancellationToken>());
}
```

### Verifying Side Effects

```csharp
public interface IRepository
{
    Task SaveAsync(string item, CancellationToken ct);
}

[Fact]
public async Task SaveActor_Should_Save_Valid_Items()
{
    // Arrange
    var mockRepository = Substitute.For<IRepository>();
    var actor = new SaveActor(mockRepository);
    var input = TestStreams.FromArray("item1", "item2", "item3");

    // Act
    var results = await TestStreams.CollectAsync(
        actor.RunAsync(input, TestContext.CreateActor()));

    // Assert - Verify side effects
    await mockRepository.Received(3).SaveAsync(
        Arg.Any<string>(),
        Arg.Any<CancellationToken>());

    await mockRepository.Received().SaveAsync(
        Arg.Is<string>(s => s == "item1"),
        Arg.Any<CancellationToken>());
}
```

### Testing Error Handling

```csharp
[Fact]
public async Task Actor_Should_Propagate_Service_Errors()
{
    // Arrange - Simulate service failure
    var mockService = Substitute.For<IValidationService>();
    mockService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns<bool>(_ => throw new InvalidOperationException("Service unavailable"));

    var actor = new ValidationActor(mockService);
    var input = TestStreams.FromArray("test");

    // Act & Assert
    await Should.ThrowAsync<InvalidOperationException>(async () =>
    {
        await TestStreams.CollectAsync(
            actor.RunAsync(input, TestContext.CreateActor()));
    });
}
```

---

## Business Logic Decoupling Pattern

### The Problem

Testing actors with complex business logic can be difficult because the logic is mixed with streaming orchestration.

```csharp
// Hard to test - logic mixed with streaming
public class ComplexActor : IStreamActor<Input, Output>
{
    public async IAsyncEnumerable<Output> RunAsync(
        IAsyncEnumerable<Input> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            // Complex business logic here - hard to unit test!
            var result = ComplexCalculation(item);
            yield return result;
        }
    }
}
```

### The Solution: Extract Business Logic to Services

**Step 1: Define Service Interface**
```csharp
public interface IBusinessLogicService
{
    Output Process(Input input);
}
```

**Step 2: Implement Service (Easy to Unit Test)**
```csharp
public class BusinessLogicService : IBusinessLogicService
{
    public Output Process(Input input)
    {
        // Pure business logic - no async, no streaming
        return new Output(input.Value * 2);
    }
}

// Pure unit test - no DataFlow complexity
[Fact]
public void Service_Should_Process_Input()
{
    var service = new BusinessLogicService();
    var result = service.Process(new Input(5));
    result.Value.ShouldBe(10);
}
```

**Step 3: Thin Actor (Orchestration Only)**
```csharp
public class DecoupledActor : IStreamActor<Input, Output>
{
    private readonly IBusinessLogicService _service;

    public DecoupledActor(IBusinessLogicService service)
    {
        _service = service;
    }

    public async IAsyncEnumerable<Output> RunAsync(
        IAsyncEnumerable<Input> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return _service.Process(item);
        }
    }
}
```

**Step 4: Test Actor with Mock**
```csharp
[Fact]
public async Task DecoupledActor_Should_Use_Service()
{
    // Arrange - Mock the service
    var mockService = Substitute.For<IBusinessLogicService>();
    mockService.Process(Arg.Any<Input>())
        .Returns(x => new Output(((Input)x[0]).Value * 2));

    var actor = new DecoupledActor(mockService);
    var input = TestStreams.FromArray(new Input(5), new Input(10));

    // Act
    var results = await TestStreams.CollectAsync(
        actor.RunAsync(input, TestContext.CreateActor()));

    // Assert
    results.Count.ShouldBe(2);
    mockService.Received(2).Process(Arg.Any<Input>());
}
```

### When to Use This Pattern

**Use when:**
- Business logic is complex
- Logic needs thorough unit testing
- Multiple actors share similar logic
- Logic requires frequent changes
- Performance is critical (can optimize service separately)

**Don't use when:**
- Logic is trivial (e.g., simple mapping)
- Actor is already simple
- Abstraction adds unnecessary complexity

---

## Troubleshooting

### Issue: Tests Hang or Timeout

**Cause:** Actor not properly handling cancellation or infinite loops.

**Solution:**
```csharp
// Always respect cancellation token
await foreach (var item in input.WithCancellation(context.CancellationToken))
{
    context.CancellationToken.ThrowIfCancellationRequested();
    // ... process item
}
```

### Issue: Collector Not Capturing Items

**Cause:** Not properly collecting from async enumerable.

**Solution:**
```csharp
// Use CollectAsync helper
var results = await TestStreams.CollectAsync(stream);

// Or manually iterate
var collected = new List<T>();
await foreach (var item in stream)
{
    collected.Add(item);
}
```

### Issue: DI Scope Issues

**Cause:** Services registered with wrong lifetime or not properly scoped.

**Solution:**
```csharp
// Actors should be transient or scoped, not singleton
var scopeFactory = TestServiceBuilder.Create()
    .WithActor<MyActor>()  // Registered as transient
    .WithScoped(dependency)  // Dependency as scoped
    .BuildScopeFactory();
```

### Issue: Mock Not Being Called

**Cause:** Mock setup doesn't match actual call or wrong argument matchers.

**Solution:**
```csharp
// Use Arg.Any for flexible matching
mockService.Method(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(true);

// Or specific matching
mockService.Method(Arg.Is<string>(s => s.StartsWith("test")), Arg.Any<CancellationToken>())
    .Returns(true);
```

---

## Best Practices Summary

1. **Use Test Helpers**: Reduce boilerplate with `TestServiceBuilder`, `TestStreams`, etc.
2. **Decouple Business Logic**: Extract complex logic to services for better testability
3. **Mock Dependencies**: Use NSubstitute for clean, powerful mocking
4. **Test in Isolation**: Unit test actors separately from pipelines
5. **Test Integration**: Also test complete pipelines end-to-end
6. **Handle Cancellation**: Always test cancellation scenarios
7. **Test Edge Cases**: Empty streams, errors, boundary conditions
8. **Verify Side Effects**: Use `Received()` to verify mock interactions

---

## Additional Resources

- **Demo Tests**: See `TestHelpersDemoTests.cs` for before/after examples
- **NSubstitute Examples**: See `NSubstituteExamplesTests.cs` for 6 concrete examples
- **Test Helpers Design**: `/research/testing-approaches/design/test-helpers-design.md`
- **ADR - Test Helpers**: `/poc/docs/adr/2025-11-07-test-helper-utilities.md`
- **ADR - Business Logic Decoupling**: `/poc/docs/adr/2025-11-07-business-logic-decoupling.md`

---

## Questions or Feedback?

If you have questions or suggestions for improving this guide, please open an issue or contribute to the documentation.
