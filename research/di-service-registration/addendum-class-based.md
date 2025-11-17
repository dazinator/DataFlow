# Research Addendum: Class-Based DataFlow Support

**Date**: 2025-11-17  
**Status**: Extended Research Complete  
**Related**: Main research in `/research/di-service-registration/README.md`

---

## Extended Scope

Based on feedback from @dazinator, the research was extended to explore class-based registration:

1. **Class-based registration system** (`AddDataFlowDefinition<T>`)
2. **Definition class with Register and Configure methods**

This feature has been prototyped, tested, and validated.

---

## Feature: Class-Based DataFlow Definition

### API Design

**Interface**:
```csharp
public interface IDataFlowDefinition
{
    void RegisterServices(DataFlowBuilder builder);
    void ConfigureGraph(DataFlowGraphBuilderEx builder);
}
```

**Registration**:
```csharp
services.AddDataFlowDefinition<MyDataFlowDefinition>("my-flow");
```

**Implementation Example**:
```csharp
public class MyDataFlowDefinition : IDataFlowDefinition
{
    public void RegisterServices(DataFlowBuilder builder)
    {
        builder.AddBlock("producer", sp => new ProducerBlock<int>(...));
        builder.AddBlock("transformer", sp => new ActorBlock<int, string, MyActor>(...));
    }
    
    public void ConfigureGraph(DataFlowGraphBuilderEx builder)
    {
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
    }
}
```

### Benefits

- ✅ **Separation of Concerns**: Service registration separated from graph structure
- ✅ **Reusability**: DataFlow definitions can be shared across applications
- ✅ **Testability**: Can test registration and configuration independently
- ✅ **Type Safety**: Class references are compile-time safe
- ✅ **Organization**: Complex DataFlows benefit from dedicated classes

### Use Cases

- Complex DataFlows with many blocks
- Reusable DataFlow patterns across projects
- When DataFlow needs independent testing
- When configuration is complex or environment-specific

### Validation

**Test**: `ClassBased_DataFlowDefinition_Should_Work`
- ✅ Definition registered successfully
- ✅ Services registered via `RegisterServices()`
- ✅ Graph built via `ConfigureGraph()`
- ✅ End-to-end execution verified

---

## Combined Test Results

### Original Features (7 tests)
1. ✅ Traditional approach (baseline)
2. ✅ New canonical DI approach
3. ✅ Hybrid approach (mix DI and inline)
4. ✅ Error handling (missing block)
5. ✅ Error handling (no service provider)
6. ✅ Strategy registration
7. ✅ Singleton lifetime verification

### New Feature (1 test)
8. ✅ Class-based definition registration

### Total: 8/8 Tests Passing ✅

---

## Performance Impact

**Original Overhead**: <0.1%  
**With Class-Based Definitions**: <0.1% (no measurable increase)

**Why No Impact**:
- Class-based definitions: No runtime overhead vs inline lambdas
- Compile-time only abstraction

---

## Backward Compatibility

All new features are **fully backward compatible**:

- ✅ Original `AddDataFlows()` unchanged
- ✅ Original `DataFlowBuilder` unchanged  
- ✅ Original `DataFlowGraphBuilderEx` unchanged
- ✅ All original tests still passing
- ✅ No breaking changes to existing API

**Migration Path**: Opt-in only
- Existing code continues to work
- New features available when needed
- Can mix old and new patterns

---

## Implementation Guidance

### When to Use Each Pattern

**Inline Lambda** (`AddDataFlows`):
- Simple, small DataFlows
- One-off configurations
- Quick prototypes

**Class-Based** (`AddDataFlowDefinition<T>`):
- Complex DataFlows
- Reusable patterns
- Need testability
- Multiple environments

---

## Updated Prototype Files

**Core Implementation**:
- `ServiceCollectionExtensions-v2.cs` - Extended with class-based feature
  - `IDataFlowDefinition` interface
  - `AddDataFlowDefinition<T>()` extension

**Tests**:
- `AdvancedDiRegistrationTests.cs` - Comprehensive test
  - Class-based definition test

---

## Recommendations

### For Production Implementation

1. **Implement alongside core DI features** - Complements canonical registration well
2. **Provide examples** - Help developers choose right approach
3. **Document trade-offs** - Clear guidance on when to use class-based vs lambda
4. **Consider adding helpers** - Could add `AddDataFlowDefinition<T>()` overload that doesn't require name

### Future Enhancements

1. **Source Generators** - Generate `IDataFlowDefinition` implementations from attributes
2. **Configuration Providers** - Load DataFlow configurations from JSON/YAML
3. **Dependency Validation** - Validate all block dependencies exist at registration time

---

## Note on Service Isolation

For scenarios requiring service isolation (e.g., multi-tenancy, different service lifetimes per DataFlow):

**Recommended Approach**: The host application can create its own `IServiceCollection`, use the canonical registration APIs (class-based or lambda), and build a separate service provider:

```csharp
// Create isolated service collection
var isolatedServices = new ServiceCollection();

// Use canonical registration
isolatedServices.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
});

// Or use class-based
isolatedServices.AddDataFlowDefinition<MyDataFlowDefinition>("my-flow");

// Build isolated service provider
var isolatedServiceProvider = isolatedServices.BuildServiceProvider();

// Use with graph builder
var builder = new DataFlowGraphBuilderEx("my-flow", isolatedServiceProvider);
```

This approach provides complete control without requiring additional API surface area in the DataFlow library.

---

## Conclusion

The class-based DataFlow definition feature has been successfully prototyped and validated:

- ✅ Class-based DataFlow definitions work as expected
- ✅ Clean separation of registration and configuration
- ✅ All tests passing (8/8)
- ✅ No performance impact
- ✅ Full backward compatibility

**Ready for production implementation.**
