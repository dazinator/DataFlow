# ADR: Canonical DI Service Registration for DataFlow

**Status**: Accepted  
**Date**: 2025-11-17  
**Authors**: Research Duty (Copilot)  
**Related Research**: `/research/di-service-registration/`

---

## Context

DataFlow currently requires blocks and strategies to be instantiated inline when building graphs:

```csharp
var builder = new DataFlowGraphBuilder("my-flow");
builder.AddBlock(new ProducerBlock<int>("producer", _ => GenerateData()))
    .AddBlock(new ActorBlock<int, string, MyActor>("transform", scopeFactory))
    .Connect("producer", "transform");
```

**Problems with Current Approach**:
1. Components created at graph-building time, not registered with DI
2. Doesn't follow standard .NET service registration patterns
3. Difficult to leverage DI benefits (lifetime management, scoping, testability)
4. No central configuration point for DataFlow components
5. Not idiomatic for .NET developers familiar with ASP.NET patterns

**Business Value**:
- Improved developer experience through familiar patterns
- Better testability through DI
- Centralized configuration
- Alignment with .NET ecosystem conventions

---

## Decision

We will introduce a canonical dependency injection service registration pattern for DataFlow components using:

1. **`services.AddDataFlows()` extension method** - Standard .NET pattern
2. **Named component registration** - Using .NET 8 keyed services
3. **Extended graph builder** - `DataFlowGraphBuilderEx` with DI support
4. **Backward compatibility** - Keep existing inline approach working

### API Design

**Registration**:
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new ActorBlock<int, string, MyActor>(...));
    df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
});
```

**Graph Building**:
```csharp
var builder = new DataFlowGraphBuilderEx("my-flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
```

---

## Alternatives Considered

### Alternative 1: Type-Based Registration (Rejected)

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock<MyProducer>();
    df.AddBlock<MyTransformer>();
});

builder.UseBlock<MyProducer>()
    .UseBlock<MyTransformer>();
```

**Why Rejected**:
- Only allows one instance per type
- Can't have multiple producers/transformers of same type
- Less flexible for real-world scenarios
- Doesn't match how DataFlow graphs actually work (many similar blocks)

### Alternative 2: Descriptor Pattern (Rejected)

```csharp
public class ProducerDescriptor : IBlockDescriptor<ProducerBlock<int>> { ... }

services.AddDataFlows(df => df.AddDescriptor<ProducerDescriptor>());
```

**Why Rejected**:
- Significant boilerplate (extra class per block)
- Overkill for simple scenarios
- Doesn't provide meaningful benefits over selected approach
- Not a common .NET pattern

### Alternative 3: Modify Existing Builder (Rejected)

Add DI methods to existing `DataFlowGraphBuilder`:

```csharp
public class DataFlowGraphBuilder
{
    public DataFlowGraphBuilder(IServiceProvider? serviceProvider = null) { ... }
    public DataFlowGraphBuilder UseBlock(string name) { ... }
}
```

**Why Rejected**:
- Breaking change (constructor signature)
- Forces DI dependency even when not needed
- Violates single responsibility (does too much)
- Migration more difficult

---

## Selected Approach: Rationale

### Why Named Registration (Keyed Services)

**Advantages**:
- ✅ Supports multiple instances of same type
- ✅ Familiar pattern (.NET 8 keyed services)
- ✅ Matches DataFlow's named-block paradigm
- ✅ Flexible and extensible

**Trade-offs Accepted**:
- ⚠️ String keys not compile-time safe
- ⚠️ Runtime validation required

**Mitigation**: 
- Clear error messages when blocks not found
- Could add constants for common block names
- Future: source generators for type-safe wrappers

### Why Separate Builder Class

**Advantages**:
- ✅ Full backward compatibility (original builder unchanged)
- ✅ Clear upgrade path (opt-in to DI)
- ✅ Separation of concerns
- ✅ No breaking changes

**Trade-offs Accepted**:
- ⚠️ Two similar classes (potential confusion)

**Mitigation**:
- Clear naming (`Ex` suffix indicates extended)
- Documentation shows when to use which
- Can mix both approaches (gradual migration)

### Why Singleton Lifetime

**Advantages**:
- ✅ Efficient resource usage (one instance per name)
- ✅ Matches typical block usage (stateless)
- ✅ Consistent with DataFlow execution model

**Assumptions**:
- Blocks are thread-safe (already a DataFlow requirement)
- State belongs in execution context, not block instance
- For stateful scenarios, use ActorBlock with scoped actors

---

## Consequences

### Positive

1. **Developer Experience**: Familiar .NET pattern, immediately understandable
2. **Testability**: Easier to mock blocks in tests
3. **Centralization**: All DataFlow configuration in one place
4. **Flexibility**: Supports DI benefits (logging, configuration, etc.)
5. **Backward Compatible**: Existing code continues to work
6. **Migration Path**: Gradual adoption possible

### Negative

1. **String Keys**: Not compile-time safe (runtime errors possible)
2. **Two Builders**: Slight complexity having two similar classes
3. **Learning Curve**: Developers need to learn new pattern (minimal)

### Neutral

1. **Performance**: Negligible overhead (<0.1% measured in prototype)
2. **Breaking Changes**: None (new API is additive)
3. **.NET Version**: Requires .NET 8+ (already a requirement)

---

## Implementation Guidance

### Phase 1: Core Implementation
1. Implement `ServiceCollectionExtensions.AddDataFlows()`
2. Implement `DataFlowBuilder` for registration
3. Implement `DataFlowGraphBuilderEx` for consumption
4. Add comprehensive unit tests

### Phase 2: Documentation
1. XML documentation on all public APIs
2. Update README with new pattern
3. Create migration guide
4. Add code examples

### Phase 3: Integration
1. Update existing examples (where beneficial)
2. Integration tests with realistic scenarios
3. Performance validation with large graphs

### Phase 4: Future Enhancements
1. Validation at registration time (optional)
2. Constants for common block names
3. Source generators for type safety (if needed)

---

## Validation

### Research Prototype
- Location: `/research/di-service-registration/`
- Status: ✅ Complete and validated
- Tests: 7/7 passing
- Performance: <0.1% overhead

### Test Coverage
- ✅ Basic registration and usage
- ✅ Backward compatibility
- ✅ Error scenarios
- ✅ Strategy registration
- ✅ Lifetime verification

---

## References

**Research Documentation**:
- [Research README](/research/di-service-registration/README.md)
- [API Design Spec](/research/di-service-registration/design/api-design.md)
- [Exploration Notes](/research/di-service-registration/notes/exploration-notes.md)

**Prototype Code**:
- `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
- `/poc/DataFlow.POC/Builder/DataFlowGraphBuilderEx.cs`
- `/poc/DataFlow.POC.Tests/DiServiceRegistrationTests.cs`

**.NET Documentation**:
- [Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Keyed Services (.NET 8)](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#keyed-services)

---

## Approval

**Research Status**: ✅ Validated  
**Recommendation**: Proceed to implementation  
**Breaking Changes**: None  
**Risk Level**: Low

---

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-17 | 1.0 | Initial ADR based on research findings |
