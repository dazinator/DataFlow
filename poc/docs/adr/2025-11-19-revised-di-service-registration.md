# ADR: Revised DI Service Registration Design

**Date**: 2025-11-19  
**Status**: Accepted  
**Supersedes**: ADR 2025-11-17 (DI Service Registration)  
**Related**: Research in `/research/design-revision-2025-11/`

---

## Context

The DI service registration design from issue #473 (documented in ADR 2025-11-17) was successfully prototyped and validated. However, when attempting implementation, four architectural concerns were identified:

1. **Parallel Builder Structures**: Introduction of `DataFlowGraphBuilderEx` alongside existing `DataFlowGraphBuilder` created confusion
2. **Unsafe Default Lifetime**: Singleton default was problematic for scoped dependencies and multi-instance execution
3. **No Registration Idempotence**: Duplicate registrations weren't detected or handled
4. **Disjointed API**: Graph topology building was disconnected from service registration

These concerns needed resolution before production implementation.

---

## Decision

We will revise the DI service registration design to address all four concerns:

### 1. Single Enhanced Builder

**Decision**: Enhance existing `DataFlowGraphBuilder` instead of creating parallel `DataFlowGraphBuilderEx`.

**Implementation**:
```csharp
// Enhanced constructor (backward compatible)
public DataFlowGraphBuilder(
    string name, 
    IServiceProvider? serviceProvider = null,  // NEW: optional
    ILogger<DataFlowGraph>? logger = null)

// NEW: Resolve blocks from DI
public DataFlowGraphBuilder UseBlock(string name)
{
    var block = _serviceProvider.GetKeyedService<IBlock>(name);
    return AddBlock(block);
}

// UNCHANGED: Direct block registration
public DataFlowGraphBuilder AddBlock(IBlock block)
```

### 2. Scoped Default Lifetime

**Decision**: Change default block lifetime from singleton to scoped, with explicit lifetime methods.

**Implementation**:
```csharp
// Default: scoped (safe for most scenarios)
df.AddBlock("name", sp => new MyBlock(...))  

// Explicit lifetime methods
df.AddScopedBlock("name", sp => new MyBlock(...))
df.AddSingletonBlock("name", sp => new MyBlock(...))
df.AddTransientBlock("name", sp => new MyBlock(...))
```

### 3. Duplicate Detection

**Decision**: Throw `InvalidOperationException` on duplicate block/strategy/graph names.

**Implementation**:
```csharp
private void CheckDuplicateRegistration(string name, string componentType)
{
    bool isDuplicate = _services.Any(sd => 
        sd.ServiceKey?.ToString() == name && 
        (sd.ServiceType == typeof(IBlock) || 
         sd.ServiceType == typeof(EdgeStrategy) || 
         sd.ServiceType == typeof(DataFlowGraph)));

    if (isDuplicate)
    {
        throw new InvalidOperationException(
            $"{componentType} '{name}' is already registered. " +
            "Each block, strategy, and graph must have a unique name.");
    }
}
```

### 4. Unified Registration

**Decision**: Add `AddGraph()` method to integrate graph topology into `AddDataFlows()` registration.

**Implementation**:
```csharp
// Inline graph configuration
df.AddGraph("main", g =>
{
    g.UseBlock("producer")
     .UseBlock("transformer")
     .Connect("producer", "transformer");
});

// Class-based graph definition
df.AddGraphDefinition<MyGraphDefinition>("complex");
```

---

## Rationale

### Why Enhance Existing Builder?

**Alternatives Considered**:
- Keep both builders permanently
- Replace old builder with new one
- Use facade pattern

**Why This Decision**:
- ✅ No confusing parallel structures
- ✅ Backward compatible (service provider is optional)
- ✅ Clear upgrade path
- ✅ Minimal code changes (additive only)
- ✅ Single canonical API

### Why Scoped Default?

**Alternatives Considered**:
- Keep singleton default
- Use transient default
- Require explicit lifetime always

**Why This Decision**:
- ✅ Safe for scoped dependencies (DbContext, HttpClient, etc.)
- ✅ Supports multi-instance graph execution
- ✅ Each scope gets fresh instances
- ✅ Aligns with .NET DI best practices
- ✅ Explicit methods available for other lifetimes

### Why Throw on Duplicates?

**Alternatives Considered**:
- Silent first-wins (TryAdd pattern)
- Silent last-wins (override)
- Allow duplicates, runtime resolution chooses

**Why This Decision**:
- ✅ Catches configuration errors early
- ✅ Clear, actionable error messages
- ✅ Prevents ambiguous behavior
- ✅ Aligns with structural registration patterns (AddDbContext throws)
- ✅ Different from infrastructure services (which can use TryAdd)

### Why Add AddGraph()?

**Alternatives Considered**:
- Keep topology separate from registration
- Only support class-based definitions
- Force all graphs to be predefined

**Why This Decision**:
- ✅ Unified registration entry point
- ✅ Graphs stored in DI (easy resolution)
- ✅ Still supports dynamic runtime graphs
- ✅ Optional class-based for complex graphs
- ✅ Meets stated design goal

---

## Consequences

### Positive

1. **Architectural Clarity**: Single builder, no parallel structures
2. **Safety**: Scoped default prevents common pitfalls
3. **Error Prevention**: Duplicate detection catches configuration mistakes
4. **API Cohesion**: Unified registration improves developer experience
5. **Backward Compatibility**: Optional service provider preserves existing usage
6. **Flexibility**: Multiple lifetime options + dynamic graphs still supported

### Negative

1. **Breaking Change**: Default lifetime changes from singleton to scoped
   - **Mitigation**: Use `AddSingletonBlock` to preserve singleton behavior
   - **Impact**: POC only, no production code affected

2. **Additional Methods**: More API surface area
   - **Mitigation**: Explicit naming makes purpose clear
   - **Impact**: Minor, follows .NET conventions

3. **Duplicate Detection Overhead**: Check on every registration
   - **Mitigation**: Negligible performance impact (registration time only)
   - **Impact**: None

### Neutral

1. **Migration Required**: Previous #473 design must be updated
   - Issue #475 (implementation) should be replaced with new specs
   - Previous ADR superseded but retained for historical context

---

## Implementation Impact

### POC Code

**Changes Required**:
1. Enhance `DataFlowGraphBuilder` constructor and add `UseBlock()`
2. Create `DataFlowBuilder` with lifetime-specific methods
3. Add `AddGraph()` and `AddGraphDefinition<T>()` methods
4. Implement duplicate checking

**Estimated Effort**: 3-5 days
- Core registration: 1-2 days
- Graph integration: 1 day
- Testing: 1 day
- Documentation: 1 day

### Production Code

**Not Applicable**: POC only

---

## Testing Strategy

**Validation**: 19 comprehensive tests created and passing

**Coverage**:
- Single builder pattern (backward compat, DI, hybrid)
- All lifetime scopes (scoped, singleton, transient)
- Duplicate detection and error handling
- Graph registration and resolution
- Dynamic graph building
- Class-based definitions

---

## Documentation Impact

**Required Updates**:
1. POC README with new registration patterns
2. Migration guide from previous #473 design
3. Lifetime selection guidance
4. Graph building patterns
5. Examples demonstrating all features

---

## Related Decisions

- **Supersedes**: ADR 2025-11-17 (DI Service Registration)
  - Previous design validated but had implementation concerns
  - This ADR resolves those concerns
  
- **Impacts**: Implementation issue #475
  - Should be replaced with specs based on this revision
  - Previous implementation plan no longer valid

---

## References

**Research**:
- `/research/design-revision-2025-11/` - Complete research findings
- `/research/design-revision-2025-11/notes/analysis.md` - Detailed problem analysis

**Previous Work**:
- Issue #473 - Original DI registration research
- `/research/di-service-registration/` - Previous research
- ADR 2025-11-17 - Previous ADR (superseded)

**Prototype**:
- `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
- `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs` (enhanced)
- `/poc/DataFlow.POC.Tests/RevisedDiDesignTests.cs` (19 tests)

---

## Decision Makers

- Research conducted by: GitHub Copilot (Research Duty)
- Design validated through: Prototype + comprehensive testing
- Approval required from: @dazinator

---

## Review Notes

**Status**: Awaiting approval

**Next Steps**:
1. Review research findings and prototype
2. Approve design decisions
3. Revert prototype code
4. Create implementation issue
5. Begin implementation

---

## Appendix: API Comparison

### Previous Design (Issue #473)

```csharp
// Registration
services.AddDataFlows(df => {
    df.AddBlock("producer", sp => new ProducerBlock<int>(...)); // Singleton
});

// Topology (separate)
var builder = new DataFlowGraphBuilderEx("flow", sp);
builder.UseBlock("producer").Connect(...);
```

### Revised Design (This ADR)

```csharp
// Unified registration
services.AddDataFlows(df => {
    df.AddBlock("producer", sp => new ProducerBlock<int>(...)); // Scoped
    
    df.AddGraph("flow", g => {
        g.UseBlock("producer").Connect(...);
    });
});

// Or dynamic (still supported)
var builder = new DataFlowGraphBuilder("flow", sp);  // Same class
builder.UseBlock("producer").Connect(...);
```

### Key Differences

| Aspect | Previous | Revised |
|--------|----------|---------|
| Builder | DataFlowGraphBuilderEx | DataFlowGraphBuilder (enhanced) |
| Default Lifetime | Singleton | Scoped |
| Topology | Separate | Integrated via AddGraph() |
| Duplicates | Unhandled | Throws exception |
| Backward Compat | New API | Fully compatible |
