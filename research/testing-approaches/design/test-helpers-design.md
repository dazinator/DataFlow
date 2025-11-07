# Test Helpers Design

## Overview

A suite of testing utilities that dramatically reduce boilerplate and improve test authoring experience for DataFlow pipelines.

## Architecture

### Core Principles

1. **Reduce Boilerplate**: Eliminate repetitive test setup code
2. **Reusability**: Generic implementations replace specialized versions
3. **Clarity**: Fluent APIs that express intent clearly
4. **Minimal Learning Curve**: Simple, discoverable APIs

### Components

#### 1. TestServiceBuilder

**Purpose**: Fluent DI configuration for tests

**API Design**:
```csharp
public class TestServiceBuilder
{
    public static TestServiceBuilder Create();
    public TestServiceBuilder WithActor<TActor>() where TActor : class;
    public TestServiceBuilder WithScoped<TService>(TService instance) where TService : class;
    public TestServiceBuilder WithScoped<TService>(Func<IServiceProvider, TService> factory);
    public TestServiceBuilder WithSingleton<TService>(TService instance) where TService : class;
    public IServiceProvider Build();
    public IServiceScopeFactory BuildScopeFactory();
}
```

**Design Rationale**:
- Fluent API for readability
- Actors registered as transient (standard pattern)
- Scope factory method for common ActorBlock usage
- Minimal API surface

#### 2. CollectorActor<T>

**Purpose**: Generic collector for test assertions

**API Design**:
```csharp
public class CollectorActor<T> : IStreamActor<T, object>
{
    public CollectorActor(List<T> collected);
    public IAsyncEnumerable<object> RunAsync(IAsyncEnumerable<T> input, IActorExecutionContext context);
}
```

**Design Rationale**:
- Single generic replaces 15+ specialized versions
- Takes list by reference (easy assertions)
- Returns object (terminal actor pattern)

#### 3. TestStreams

**Purpose**: Simplified stream creation and collection

**API Design**:
```csharp
public static class TestStreams
{
    public static IAsyncEnumerable<T> FromArray<T>(params T[] items);
    public static IAsyncEnumerable<T> FromList<T>(List<T> items);
    public static IAsyncEnumerable<int> Integers(int count, CancellationToken ct = default);
    public static IAsyncEnumerable<int> Range(int start, int end, CancellationToken ct = default);
    public static IAsyncEnumerable<T> Empty<T>();
    public static Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> stream, CancellationToken ct = default);
}
```

**Design Rationale**:
- Static methods for discoverability
- Common patterns covered
- Cancellation token support
- Collection helper for assertions

#### 4. TestContext

**Purpose**: Simplified context creation

**API Design**:
```csharp
public static class TestContext
{
    public static IExecutionContext CreateExecution(IServiceProvider? sp = null, CancellationToken ct = default);
    public static IActorExecutionContext CreateActor(CancellationToken ct = default);
}
```

**Design Rationale**:
- Static methods for simplicity
- Sensible defaults
- Optional overrides for advanced scenarios

#### 5. CommonActors

**Purpose**: Reusable actors for common test patterns

**API Design**:
```csharp
public class TransformActor<TIn, TOut> : IStreamActor<TIn, TOut>
{
    public TransformActor(Func<TIn, TOut> transform);
}

public class FilterActor<T> : IStreamActor<T, T>
{
    public FilterActor(Func<T, bool> predicate);
}
```

**Design Rationale**:
- Lambda-based for flexibility
- Covers most common test scenarios
- Type-safe

## Usage Patterns

### Basic Integration Test

```csharp
[Fact]
public async Task Transform_Flow_Should_Process_Items()
{
    var collected = new List<string>();

    var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(5));
    
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

    var graph = new DataFlowGraphBuilder("test-flow")
        .AddBlock(producer)
        .AddBlock(transformer)
        .Connect(producer, transformer)
        .AddBlock(collector)
        .Connect(transformer, collector)
        .Build();

    await graph.ExecuteAsync(TestContext.CreateExecution());

    collected.ShouldBe(new[] { "Item-1", "Item-2", "Item-3", "Item-4", "Item-5" });
}
```

### Unit Test Actor

```csharp
[Fact]
public async Task FilterActor_Should_Filter_Items()
{
    var actor = new FilterActor<int>(i => i % 2 == 0);
    var input = TestStreams.Range(1, 10);
    var context = TestContext.CreateActor();

    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));

    results.ShouldBe(new[] { 2, 4, 6, 8, 10 });
}
```

## Benefits

1. **Code Reduction**: 40-60% less test code
2. **Consistency**: Standard patterns across all tests
3. **Maintainability**: Changes in one place
4. **Readability**: Intent-focused, not ceremony-focused
5. **Speed**: Faster test authoring

## Extension Points

Future enhancements can build on this foundation:
- Additional generic actors (batch, delay, error injection)
- Routing-specific helpers
- Flow builder DSL
- Assertion helpers

## Migration Strategy

1. **Phase 1**: Introduce helpers alongside existing patterns
2. **Phase 2**: Update examples and documentation
3. **Phase 3**: Gradually refactor existing tests
4. **Phase 4**: Establish as standard practice

No breaking changes required - purely additive.
