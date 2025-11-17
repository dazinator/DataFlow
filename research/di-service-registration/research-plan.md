# Research Plan: Better DI Service Registration

## Research Objective

Explore and validate a more canonical dependency injection service registration experience for DataFlow that uses standard .NET conventions and provides a cleaner, more intuitive API for registering blocks, strategies, and other dataflow components.

## Current State Analysis

**Current Pattern (Non-Canonical):**
```csharp
// Current approach - manually registering blocks in graph builder
var builder = new DataFlowGraphBuilder("my-flow");
builder.AddBlock(new ProducerBlock<int>("producer", ctx => ProduceData(ctx)));
builder.AddBlock(new TransformBlock<int, string>("transformer", x => x.ToString()));
```

**Issues with Current Approach:**
1. ❌ Components are created inline rather than registered with DI
2. ❌ No central service registration experience
3. ❌ Difficult to leverage DI benefits (scoping, lifetime management)
4. ❌ Not following standard .NET extension method patterns
5. ❌ Strategies and blocks mixed with graph topology

## Proposed Pattern

**Goal: Canonical .NET DI Registration**
```csharp
// Proposed approach - canonical DI registration
services.AddDataFlows(df => 
{
    // Register strategies with names/keys
    df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
    df.AddStrategy("routing", sp => new RoutedItemEdgeStrategy(...));
    
    // Register blocks with names/keys
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new TransformBlock<int, string>(...));
});

// Graph builder then references by name
var builder = new DataFlowGraphBuilder("my-flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .UseStrategy("competing")
    .Connect("producer", "transformer");
```

## Research Questions

1. **API Design**: What is the most intuitive API for registering DataFlow components?
   - Should we use `AddDataFlows()`? `AddDataFlow()`? Something else?
   - How do we register blocks vs strategies vs other components?
   - Should registration be type-safe (generics) or name-based (strings)?

2. **Keyed Services**: How should we leverage .NET's keyed service feature?
   - Use keyed services for block/strategy lookup?
   - What are the performance implications?
   - How does this integrate with existing service resolution?

3. **Graph Builder Integration**: How does the graph builder consume registered services?
   - Resolve by name/key from DI?
   - Support both factory functions AND service resolution?
   - Backward compatibility with existing inline approach?

4. **Lifetime Management**: What service lifetimes make sense?
   - Singleton blocks shared across graphs?
   - Scoped blocks per graph execution?
   - Transient for disposable components?

5. **Type Safety**: Can we maintain type safety while using names/keys?
   - Generic constraints on registration?
   - Compile-time vs runtime validation?
   - IntelliSense support?

## Success Metrics

### Quantitative
- **API Calls Reduced**: Registration + graph building should be fewer lines than current approach
- **Type Safety**: 100% of type mismatches caught at compile time (if possible)
- **Performance**: Keyed service resolution overhead < 5% vs direct instantiation

### Qualitative  
- **Readability**: Registration intent is clear and follows .NET conventions
- **Discoverability**: Developers familiar with ASP.NET can understand immediately
- **Flexibility**: Supports both simple and complex scenarios

### Baseline
- Current: Inline instantiation in graph builder (~3-5 lines per block)
- Target: Centralized registration (~1-2 lines per component) + reference by name (~1 line in builder)

### Validation
- Create side-by-side comparison tests showing before/after
- Build working prototype demonstrating all features
- Test with realistic multi-block scenarios

## Validation Approach

### Phase 1: API Design Exploration (1-2 days)
- Sketch multiple API designs on paper
- Review .NET DI patterns (AddDbContext, AddAuthentication, etc.)
- Choose 2-3 most promising approaches
- Create minimal prototypes for each

### Phase 2: Prototype Implementation (2-3 days)
- Implement chosen API design in POC
- Create `AddDataFlows()` extension method
- Implement keyed service registration for blocks
- Implement keyed service registration for strategies
- Update graph builder to resolve from DI
- Maintain backward compatibility

### Phase 3: Integration Testing (1-2 days)
- Port existing tests to new registration pattern
- Create before/after comparison tests
- Test lifetime scenarios (singleton, scoped, transient)
- Validate type safety and error handling
- Performance benchmark: keyed service resolution overhead

### Phase 4: Documentation (1 day)
- Document new registration patterns
- Create migration guide
- Capture design decisions in ADR
- Write implementation handover issue

## Expected Outcomes

### Primary Deliverables
1. **Research Documentation**: `/research/di-service-registration/README.md`
   - Comparison of API design alternatives
   - Recommended approach with rationale
   - Performance analysis
   - Migration considerations

2. **Design Documentation**: `/research/di-service-registration/design/`
   - API design specification
   - Integration patterns
   - Lifetime management strategy

3. **Prototype Code**: `/research/di-service-registration/handover/prototype/`
   - Working `AddDataFlows()` extension
   - Updated graph builder with service resolution
   - Example usage patterns
   - Comparison tests

4. **ADR**: `/poc/docs/adr/2025-11-17-di-service-registration.md`
   - Decision record for chosen approach
   - Alternatives considered
   - Trade-offs and implications

5. **Implementation Issue**: `/research/di-service-registration/handover/`
   - Complete implementation specification
   - Test scenarios
   - Success criteria
   - References to research artifacts

### Research Outcome
- **Type**: Implementation Handover (Outcome 1)
- **Why**: Prototype validates feasibility, but production implementation needs proper integration, full test coverage, and documentation

## Timeline

- **Phase 1 (API Design)**: 1-2 days
- **Phase 2 (Prototype)**: 2-3 days  
- **Phase 3 (Testing)**: 1-2 days
- **Phase 4 (Documentation)**: 1 day
- **Total**: 5-8 days

## Risks and Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Keyed services too slow | High | Benchmark early, consider alternative lookup |
| Type safety impossible with strings | Medium | Explore generic alternatives, accept trade-off |
| Breaking changes required | High | Design for backward compatibility first |
| Complexity increases | Medium | Keep API simple, hide complexity in implementation |

## References

- .NET DI Documentation: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection
- Keyed Services: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#keyed-services
- Current POC Graph Builder: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
- Current Test Patterns: `/poc/DataFlow.POC.Tests/`
