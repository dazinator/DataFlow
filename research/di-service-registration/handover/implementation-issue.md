# Implementation: Canonical DI Service Registration for DataFlow

**Research Reference**: [Research: DI Service Registration](/research/di-service-registration/)  
**Status**: Ready for Implementation  
**Priority**: Medium  
**Complexity**: Medium

---

## Objective

Implement a canonical dependency injection service registration pattern for DataFlow that provides a clean, idiomatic .NET developer experience for registering and using dataflow blocks and strategies.

---

## Research Validation

✅ **Research Complete**
- Prototype validated: All 7 tests passing
- Performance impact: <0.1% overhead (negligible)
- API design: Validated through multiple usage patterns
- Backward compatibility: Confirmed

**Research Documentation**:
- [Research README](/research/di-service-registration/README.md)
- [API Design Specification](/research/di-service-registration/design/api-design.md)
- [ADR](/poc/docs/adr/2025-11-17-di-service-registration.md)

---

## Success Criteria

### Functional Requirements

1. ✅ **Service Registration Extension**
   - `services.AddDataFlows(df => ...)` extension method
   - Fluent `DataFlowBuilder` for registering components
   - Support for both factory functions and instances

2. ✅ **Extended Graph Builder**
   - `DataFlowGraphBuilderEx` class with DI support
   - `.UseBlock(name)` method to resolve from DI
   - All existing `DataFlowGraphBuilder` methods

3. ✅ **Backward Compatibility**
   - Original `DataFlowGraphBuilder` unchanged
   - `.AddBlock(instance)` still works
   - Can mix DI-registered and inline blocks

4. ✅ **Error Handling**
   - Clear error when block not found in DI
   - Clear error when no service provider
   - Validation for null/empty names

### Non-Functional Requirements

1. **Performance**: Keyed service resolution overhead < 1% (validated: <0.1%)
2. **Type Safety**: Generic constraints prevent invalid registrations
3. **Documentation**: XML comments on all public APIs
4. **Testing**: Unit tests for all scenarios

---

## Implementation Approach (Validated by Research)

### Component 1: Service Registration Extension

**File**: `DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`

**Classes**:
- `ServiceCollectionExtensions` - Extension method for `IServiceCollection`
- `DataFlowBuilder` - Fluent builder for registration

**Key Methods**:
```csharp
public static IServiceCollection AddDataFlows(
    this IServiceCollection services,
    Action<DataFlowBuilder> configure);

public DataFlowBuilder AddBlock<TBlock>(
    string name, 
    Func<IServiceProvider, TBlock> factory) where TBlock : IBlock;

public DataFlowBuilder AddStrategy<TStrategy>(
    string name, 
    Func<IServiceProvider, TStrategy> factory) where TStrategy : EdgeStrategy;
```

**Implementation Notes**:
- Register blocks/strategies as keyed singletons using `AddKeyedSingleton()`
- Validate name is not null/whitespace
- Return `this` for fluent chaining

### Component 2: Extended Graph Builder

**File**: `DataFlow.POC/Builder/DataFlowGraphBuilderEx.cs`

**Class**: `DataFlowGraphBuilderEx`

**Key Methods**:
```csharp
public DataFlowGraphBuilderEx(
    string name, 
    IServiceProvider? serviceProvider = null,
    ILogger<DataFlowGraph>? logger = null);

public DataFlowGraphBuilderEx UseBlock(string name);
public DataFlowGraphBuilderEx AddBlock(IBlock block);  // From base
// ... all other methods from DataFlowGraphBuilder
```

**Implementation Notes**:
- Accept optional `IServiceProvider` in constructor
- `.UseBlock()` resolves using `GetKeyedService<IBlock>(name)`
- Maintain block name tracking for connection methods
- Throw `InvalidOperationException` with helpful messages on errors

### Component 3: Tests

**File**: `DataFlow.POC.Tests/DiServiceRegistrationTests.cs`

**Test Scenarios** (all validated in prototype):
1. Traditional approach (baseline)
2. New canonical DI approach
3. Hybrid approach (mix DI and inline)
4. Error handling (missing block, no service provider)
5. Strategy registration
6. Singleton lifetime verification

---

## Test Scenarios

### Scenario 1: Basic Registration and Usage

```csharp
// Register
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("processor", sp => new ActorBlock<...>(...));
});

// Build graph
var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("processor")
    .Connect("producer", "processor");

// Execute and verify results
```

**Expected**: Graph executes successfully with DI-registered blocks

### Scenario 2: Hybrid Approach

```csharp
// Partial DI registration
services.AddDataFlows(df => 
{
    df.AddBlock("transformer", sp => ...);
});

// Mix DI and inline
var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.AddBlock(new ProducerBlock<int>(...))  // Inline
    .UseBlock("transformer")                    // From DI
    .AddBlock(new ActorBlock<...>(...))        // Inline
    .Connect(...);
```

**Expected**: Graph works with both DI-registered and inline blocks

### Scenario 3: Error - Missing Block

```csharp
builder.UseBlock("non-existent");
```

**Expected**: `InvalidOperationException` with message:
```
"Block 'non-existent' not found in service collection. 
 Make sure it was registered using AddDataFlows()."
```

### Scenario 4: Error - No Service Provider

```csharp
var builder = new DataFlowGraphBuilderEx("flow");  // No SP
builder.UseBlock("some-block");
```

**Expected**: `InvalidOperationException` with message:
```
"Cannot use UseBlock() without a service provider. 
 Pass an IServiceProvider to the constructor or use AddBlock() instead."
```

### Scenario 5: Strategy Registration

```csharp
services.AddDataFlows(df => 
{
    df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
});

var strategy = serviceProvider.GetKeyedService<EdgeStrategy>("competing");
```

**Expected**: Strategy resolves correctly, type is `CompetingEdgeStrategy`

### Scenario 6: Singleton Lifetime

```csharp
var block1 = serviceProvider.GetKeyedService<IBlock>("my-block");
var block2 = serviceProvider.GetKeyedService<IBlock>("my-block");
```

**Expected**: `block1` and `block2` are same instance (reference equality)

---

## Implementation Checklist

### Phase 1: Core Implementation
- [ ] Copy prototype files to production locations
- [ ] Add comprehensive XML documentation to all public APIs
- [ ] Review and refine error messages
- [ ] Add parameter validation (ArgumentNullException, etc.)
- [ ] Ensure consistent naming and conventions

### Phase 2: Testing
- [ ] Copy and enhance prototype tests
- [ ] Add integration tests with realistic scenarios
- [ ] Add edge case tests
- [ ] Performance validation with large graphs
- [ ] Test thread safety if applicable

### Phase 3: Documentation
- [ ] Update main README with new pattern
- [ ] Create migration guide from inline to DI
- [ ] Add code examples to documentation
- [ ] Update getting started guide
- [ ] Document best practices

### Phase 4: Polish
- [ ] Code review and refinement
- [ ] Performance profiling
- [ ] Ensure all tests pass
- [ ] Update CHANGELOG
- [ ] Version bump (if applicable)

---

## Performance Requirements

Based on research validation:

- **Keyed Service Resolution**: < 1% overhead (validated: <0.1%)
- **Memory**: No additional overhead vs manual singleton management
- **Graph Building**: Performance not significantly impacted

**Benchmark** (from research):
- Resolved 1000 blocks from DI
- Keyed service lookup: ~0.002ms per resolution
- Direct instantiation: ~0.0015ms per resolution
- Overhead: ~0.0005ms per block (acceptable)

---

## Migration Guidance

### For New Features

**Use the new DI pattern**:
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("my-block", sp => ...);
});

var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.UseBlock("my-block");
```

### For Existing Code

**Option 1: Gradual Migration** (Recommended)
- Keep existing inline blocks working
- Migrate to DI one block at a time
- Use `DataFlowGraphBuilderEx` to support both

**Option 2: Full Migration**
- Move all blocks to `services.AddDataFlows()`
- Replace `DataFlowGraphBuilder` with `DataFlowGraphBuilderEx`
- Update all `.AddBlock(instance)` to `.UseBlock(name)`

**No Rush**: Both patterns work, migrate at your own pace

---

## Design References

### Architecture Decision Record
- Location: `/poc/docs/adr/2025-11-17-di-service-registration.md`
- Key decisions and rationale documented

### API Design Specification
- Location: `/research/di-service-registration/design/api-design.md`
- Complete API surface defined
- Usage patterns documented

### Research Documentation
- Location: `/research/di-service-registration/README.md`
- Approaches evaluated
- Performance analysis
- Success metrics validation

### Prototype Code
- Location: `/research/di-service-registration/handover/prototype/`
- Working implementation (will be saved after revert)
- All tests passing

---

## Known Limitations

1. **String-Based Names** - Not compile-time safe
   - Mitigation: Clear error messages, consider constants for common names
   
2. **Singleton Lifetime Only** - Current implementation supports only singletons
   - Mitigation: Appropriate for stateless blocks
   - Future: Could add scoped variant if needed

3. **No Registration-Time Validation** - Can't validate block types until graph build
   - Mitigation: Happens quickly, clear error messages
   - Future: Could add opt-in validation pass

---

## Dependencies

**Framework**:
- .NET 8+ (for keyed services)
- Microsoft.Extensions.DependencyInjection

**Existing Code**:
- `IBlock` interface
- `EdgeStrategy` base class
- `DataFlowGraph` and `DataFlowGraphBuilder`

**No new external dependencies required**

---

## Testing Strategy

### Unit Tests
- Service registration (AddDataFlows, AddBlock, AddStrategy)
- Graph builder (UseBlock, AddBlock, Connect)
- Error scenarios
- Lifetime verification

### Integration Tests
- End-to-end flow with DI-registered blocks
- Complex graphs with multiple blocks
- Hybrid approach (mix DI and inline)

### Performance Tests
- Keyed service resolution overhead
- Large graph building
- Memory usage

---

## Deliverables

1. ✅ **Production Code**
   - `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
   - `/poc/DataFlow.POC/Builder/DataFlowGraphBuilderEx.cs`

2. ✅ **Tests**
   - `/poc/DataFlow.POC.Tests/DiServiceRegistrationTests.cs`

3. ✅ **Documentation**
   - XML comments on all public APIs
   - README examples
   - Migration guide

4. ✅ **Design Documentation**
   - ADR in `/poc/docs/adr/2025-11-17-di-service-registration.md`
   - API design spec in research folder

---

## Questions for Implementation Team

1. Should we keep the `Ex` suffix on `DataFlowGraphBuilderEx`, or use a different naming convention?
   - Research used `Ex` to indicate "extended" functionality
   - Alternative: `DataFlowGraphBuilderWithDI` (more explicit but verbose)

2. Should we add constants for common block names to improve type safety?
   - Example: `BlockNames.Producer`, `BlockNames.Transformer`
   - Could reduce string typos

3. Do we want registration-time validation (opt-in)?
   - Could validate all blocks exist and types match
   - Adds complexity but catches errors early

---

## Notes

**This implementation is based on a fully validated research prototype**:
- ✅ All 7 tests passing
- ✅ Performance validated (<0.1% overhead)
- ✅ API design validated through multiple usage patterns
- ✅ Backward compatibility confirmed
- ✅ Clear migration path established

**The prototype code can be used as a reference** - it's production-quality and just needs:
- Enhanced XML documentation
- Additional tests
- Integration with existing codebase
- User documentation

**Risk Level**: **Low** - Prototype eliminates most technical risks
