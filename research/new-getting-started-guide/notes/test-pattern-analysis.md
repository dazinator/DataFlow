# Test Pattern Analysis

**Purpose**: Extract key patterns from test files to inform the getting started guide

## Key Patterns from RevisedDiRegistrationTests

### 1. Basic DI Registration Pattern

```csharp
var services = new ServiceCollection();
services.AddDataFlows("global", df =>
{
    // Register blocks with names
    df.AddBlock("producer", sp => new TestProducerBlock());
    df.AddBlock("transformer", sp => new TestTransformerBlock());
    
    // Define graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});

var serviceProvider = services.BuildServiceProvider();
```

**Key Insights**:
- Namespace ("global") organizes blocks and graphs
- Blocks registered with names for reusability
- Graph defined using `UseBlock` pattern
- Fluent API with method chaining

### 2. Keyed Service Resolution

```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");
```

**Key Insights**:
- Format: `"{namespace}:{graphname}"`
- Uses .NET's keyed services feature
- Clean way to resolve specific graphs

### 3. Scoped Block Lifetime

```csharp
df.AddScopedBlock("test", sp => new TestProducerBlock());
```

**Key Insights**:
- Default is `Scoped` lifetime (safe for EF Core)
- Each scope gets a new instance
- Prevents sharing state across executions

### 4. Multiple AddDataFlows Calls

```csharp
// First call
services.AddDataFlows("global", df =>
{
    df.AddBlock("block1", sp => new TestProducerBlock());
});

// Second call - can add more blocks
services.AddDataFlows("global", df =>
{
    df.AddBlock("block2", sp => new TestProducerBlock());
});
```

**Key Insights**:
- Multiple calls allowed for same namespace
- Enables modular registration
- Duplicate names throw exception

### 5. Graph Definition Pattern

```csharp
df.AddGraphDefinition<TestGraphDefinition>("main");

// Where TestGraphDefinition implements IDataFlowDefinition
public class TestGraphDefinition : IDataFlowDefinition
{
    public void Configure(DataFlowGraphBuilder builder)
    {
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
    }
}
```

**Key Insights**:
- Can extract graph definition to a class
- Promotes separation of concerns
- Reusable graph definitions

## Key Patterns from BlockLifetimeAndGraphReuseTests

### 1. Graph Execution with Scopes

```csharp
var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:main");

using (var scope = serviceProvider.CreateScope())
{
    var context = new ExecutionContext(scope.ServiceProvider, CancellationToken.None);
    await graph.ExecuteAsync(context);
}
```

**Key Insights**:
- Create scope for execution
- ExecutionContext needs service provider and cancellation token
- Scope disposal cleans up scoped services

### 2. Concurrent Graph Execution

```csharp
using var scope1 = serviceProvider.CreateScope();
using var scope2 = serviceProvider.CreateScope();

var context1 = new ExecutionContext(scope1.ServiceProvider, CancellationToken.None);
var context2 = new ExecutionContext(scope2.ServiceProvider, CancellationToken.None);

var task1 = graph.ExecuteAsync(context1);
var task2 = graph.ExecuteAsync(context2);

await Task.WhenAll(task1, task2);
```

**Key Insights**:
- Same graph can run concurrently
- Each execution gets its own scope
- Blocks are instantiated per scope (safe concurrency)

### 3. Multiple Graphs Sharing Blocks

```csharp
services.AddDataFlows("global", df =>
{
    // ONE shared block registration
    df.AddScopedBlock("shared-transformer", sp => /* ... */);
    
    // TWO different graphs
    df.AddGraph("graph-a", g =>
    {
        g.UseBlock("producer-a")
         .UseBlock("shared-transformer")
         .UseBlock("collector-a");
    });
    
    df.AddGraph("graph-b", g =>
    {
        g.UseBlock("producer-b")
         .UseBlock("shared-transformer")
         .UseBlock("collector-b");
    });
});
```

**Key Insights**:
- Register blocks once, use in multiple graphs
- Namespace isolation
- Each graph execution gets fresh block instances

### 4. Namespace Organization

```csharp
// Different namespaces for different concerns
services.AddDataFlows("orders", df => { /* order processing */ });
services.AddDataFlows("inventory", df => { /* inventory management */ });
services.AddDataFlows("shipping", df => { /* shipping */ });
```

**Key Insights**:
- Namespaces prevent naming conflicts
- Organize complex applications
- Each namespace is independent

## Essential Concepts to Teach

### Priority 1 (Must Have)
1. **What is a DataFlow** - Brief conceptual overview
2. **DI Registration** - The `AddDataFlows` pattern
3. **Block Registration** - Using names for reusability
4. **Graph Definition** - The `UseBlock` and `Connect` pattern
5. **Keyed Services** - How to resolve and execute graphs

### Priority 2 (Should Have)
6. **Execution Context** - Creating and using execution context
7. **Scopes** - Why scopes matter
8. **Multiple Graphs** - Reusing blocks across graphs
9. **Namespacing** - Organizing larger applications

### Priority 3 (Nice to Have)
10. **Concurrent Execution** - Running same graph multiple times
11. **Graph Definition Classes** - Extracting to `IDataFlowDefinition`

## What to Defer

- **Epochs** → Separate advanced guide
- **EF Core Integration** → Separate guide
- **Custom Block Types** → Advanced guide
- **Topology Patterns** → Topology guides
- **Testing** → Testing guide
- **Actors with Dependencies** → DI registration guide

## Recommended Guide Structure

1. **Introduction** (50 lines)
   - What is DataFlow?
   - When to use it
   - Key concepts overview

2. **Your First DataFlow** (150 lines)
   - Create console app
   - Register blocks and graph
   - Execute the graph
   - Complete working example

3. **Understanding Keyed Services** (100 lines)
   - How keyed services work
   - Resolution pattern
   - Namespace:graph format

4. **Reusing Blocks Across Graphs** (100 lines)
   - Register once, use many times
   - Multiple graphs example
   - Namespace organization

5. **Execution Contexts and Scopes** (100 lines)
   - Why scopes matter
   - Creating execution contexts
   - Safe concurrency

6. **Next Steps** (50 lines)
   - Links to advanced guides
   - Common patterns
   - Troubleshooting tips

**Total Estimated**: ~550 lines (vs 1051 current)

## Code Example Strategy

### Example 1: Minimal Working Example
- Console input → uppercase → console output
- Demonstrates: DI registration, graph definition, keyed services
- Lines: ~40

### Example 2: Block Reuse
- Two graphs sharing a transformer block
- Demonstrates: Block reuse, namespacing
- Lines: ~50

### Example 3: Concurrent Execution (Optional)
- Same graph running twice
- Demonstrates: Scopes, safe concurrency
- Lines: ~40

**Total Example Code**: ~130 lines (vs 300+ in current guide)
