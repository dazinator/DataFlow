# Implementation Handover: Selective Content-Based Routing

**Research Work Item**: [#512](https://github.com/uniun-technology/lib-dataflow/issues/512)  
**Research Documentation**: `/research/selective-content-routing/README.md`  
**Analysis Documentation**: `/poc/docs/analysis/routing/selective-routing-analysis.md`

---

## Implementation Objective

Integrate `SelectiveRoutingEdgeStrategy<TItem>` into the POC to provide efficient selective content-based routing.

---

## Approach (Validated by Research)

**`SelectiveRoutingEdgeStrategy<TItem>`** - A generic edge strategy that:
1. Routes items based on user-provided selector function
2. Sends each item ONLY to matching route (no broadcasting)
3. Works with any type T (no wrapper records needed)
4. Provides O(1) route lookup regardless of route count

**Prototype Location**: `/research/selective-content-routing/handover/prototype/`

---

## Success Criteria

### Functional Requirements

✅ **Core Functionality**
- [ ] Can route items based on content without broadcasting
- [ ] Routes defined statically at build time
- [ ] Type-safe route selector functions
- [ ] Clear error messages for unknown routes

✅ **Performance Requirements**
- [ ] Zero allocation overhead (no wrapper records)
- [ ] O(1) route lookup per item
- [ ] Better throughput than broadcast-and-filter for N>2 routes

✅ **Quality Requirements**
- [ ] Maintains type safety and testability
- [ ] Fits with existing POC architecture
- [ ] Clear API and documentation
- [ ] Comprehensive test coverage

### Test Scenarios

**Critical Test Scenarios** (from research):

1. **Two-Route Routing** (`SelectiveRouting_WithTwoRoutes_RoutesItemsCorrectly`)
   - Route integers by even/odd
   - Verify correct distribution
   - Reference: `/poc/DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs`

2. **Multi-Route Routing** (`SelectiveRouting_WithFiveRoutes_RoutesItemsCorrectly`)
   - Route integers across 5 routes (modulo 5)
   - Verify correct distribution
   - Validate scalability

3. **Complex Type Routing** (`SelectiveRouting_WithComplexType_RoutesCorrectly`)
   - Route Order objects by CustomerId
   - Verify works with complex types
   - Validate type safety

4. **Error Handling** (`SelectiveRouting_WithUnknownRouteKey_ThrowsException`)
   - Unknown route keys throw clear exceptions
   - Configuration errors caught at edge creation

**Additional Tests Recommended**:
- Edge cases (empty route mapping, single route)
- Concurrent routing scenarios
- High-volume stress testing

---

## Design References

### Core Implementation

**File**: `SelectiveRoutingEdgeStrategy.cs`  
**Location**: `/poc/DataFlow.POC/Core/`

**Key Design Points**:
```csharp
public class SelectiveRoutingEdgeStrategy<TItem> : EdgeStrategy
{
    private readonly Dictionary<string, IBlock> _routeKeyToBlock;
    private readonly Func<TItem, string> _routeSelector;
    
    public SelectiveRoutingEdgeStrategy(
        Dictionary<string, IBlock> routeKeyToBlock,
        Func<TItem, string> routeSelector,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100);
    
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        var typedItem = (TItem)(object)item!;
        var routeKey = _routeSelector(typedItem);
        
        if (_routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
        {
            if (typedWriters.TryGetValue(targetBlock, out var writer))
            {
                await writer.WriteAsync(item, cancellationToken);
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Route '{routeKey}' not found. Available routes: ...");
        }
    }
}
```

### Usage Pattern

**Basic Usage**:
```csharp
var routeMapping = new Dictionary<string, IBlock>
{
    ["CustomerA"] = customerAProcessor,
    ["CustomerB"] = customerBProcessor
};

var strategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.CustomerId);

var edge = new Edge(producer, new[] { customerAProcessor, customerBProcessor }, strategy);

builder
    .AddBlock(producer)
    .AddBlock(customerAProcessor)
    .AddBlock(customerBProcessor)
    .AddEdge(edge);
```

---

## Implementation Checklist

### Phase 1: Core Integration (Required)

- [ ] Copy `SelectiveRoutingEdgeStrategy.cs` from prototype to `/poc/DataFlow.POC/Core/`
- [ ] Remove "RESEARCH PROTOTYPE" comments
- [ ] Copy test file to `/poc/DataFlow.POC.Tests/`
- [ ] Remove "RESEARCH PROTOTYPE" comments from tests
- [ ] Verify all tests pass
- [ ] Run existing routing tests to ensure no regressions

### Phase 2: Documentation (Required)

- [ ] Update `/poc/docs/analysis/routing/README.md`
  - Add `SelectiveRoutingEdgeStrategy` to routing mechanisms section
  - Update "Broadcast-and-Filter vs Selective Routing" section
  - Add usage examples

- [ ] Update `/poc/docs/analysis/routing/COMPARISON_TABLES.md`
  - Add `SelectiveRoutingEdgeStrategy` to feature comparison
  - Add to performance characteristics table
  - Update use case matrix

- [ ] Create or update `/poc/docs/guides/routing.md` (if exists)
  - Document selective routing with examples
  - Provide migration guidance from broadcast-and-filter
  - Include performance considerations

### Phase 3: Ergonomics (Optional - Nice to Have)

- [ ] Create builder extension for easier configuration
  ```csharp
  public static class SelectiveRoutingExtensions
  {
      public static DataFlowGraphBuilder ConnectWithSelectiveRouting<TItem>(
          this DataFlowGraphBuilder builder,
          string sourceName,
          Func<TItem, string> routeSelector,
          Dictionary<string, string> routes)
      {
          // Implementation to simplify usage
      }
  }
  ```

- [ ] Add helper methods for common patterns
- [ ] Update builder documentation

### Phase 4: Validation (Optional - Nice to Have)

- [ ] Create benchmarks comparing to broadcast-and-filter
  - File: `/poc/DataFlow.POC.Benchmarks/SelectiveRoutingBenchmark.cs`
  - Already prototyped in research
  - Measure throughput with 2, 5, 10 routes
  - Document results

- [ ] Run stress tests
  - High-volume scenarios
  - Concurrent routing
  - Validate performance at scale

---

## Performance Requirements

### Expected Performance Characteristics

**Per-Item Cost**:
- 1 dictionary lookup: O(1)
- 1 channel write operation
- 0 allocations (no wrapper records)

**Scalability**:
- Independent of route count (O(1) lookup)
- No broadcast overhead
- No filtering overhead

**Throughput** (estimated, validate with benchmarks):
- 2 routes: ~2× vs broadcast-and-filter
- 5 routes: ~5× vs broadcast-and-filter
- 10 routes: ~10× vs broadcast-and-filter

---

## Potential Issues and Mitigations

### Issue 1: API Discoverability

**Problem**: Users may not discover selective routing because it requires explicit edge configuration.

**Mitigation**:
- Create builder extensions (Phase 3)
- Document clearly in routing guide
- Provide migration examples from broadcast-and-filter

### Issue 2: Learning Curve

**Problem**: Users must understand edge strategies vs blocks.

**Mitigation**:
- Clear documentation with examples
- Side-by-side comparison with broadcast-and-filter
- Link from routing guide to edge strategy docs

### Issue 3: Configuration Verbosity

**Problem**: Creating route mapping dictionary is verbose.

**Mitigation**:
- Builder extensions can simplify (Phase 3)
- Helper methods for common patterns
- Consider fluent API for route configuration

---

## Testing Guidance

### Unit Tests

**Location**: `/poc/DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs`

**Coverage**:
- ✅ Two-route scenario
- ✅ Five-route scenario (scalability)
- ✅ Complex types (Order class)
- ✅ Error handling (unknown routes)

**Additional Tests to Consider**:
- Single route (edge case)
- Empty route mapping (should throw at construction)
- Null route selector (should throw at construction)
- Concurrent writes from multiple sources
- Route key collisions

### Integration Tests

**Scenarios**:
- End-to-end pipeline with selective routing
- Multiple selective routing edges in same graph
- Combination with other edge strategies (broadcast, competing)
- Error propagation through pipeline

### Performance Tests (Optional)

**Benchmarks**:
- Throughput comparison vs broadcast-and-filter
- Memory allocation comparison
- CPU usage comparison
- Scalability test with increasing route count

---

## Documentation Updates

### Files to Update

1. **`/poc/docs/analysis/routing/README.md`**
   - Add `SelectiveRoutingEdgeStrategy` to mechanisms section
   - Update recommendations
   - Add usage examples

2. **`/poc/docs/analysis/routing/COMPARISON_TABLES.md`**
   - Add to all comparison tables
   - Document performance characteristics
   - Update use case matrix

3. **`/poc/docs/guides/routing.md`** (create if doesn't exist)
   - Comprehensive routing guide
   - When to use each strategy
   - Migration guidance
   - Performance considerations

### New Documentation

4. **API Documentation** (XML comments in code)
   - Already complete in prototype
   - Comprehensive comments on class and methods
   - Usage examples in comments

---

## Dependencies

### No New Dependencies Required

- Uses existing `EdgeStrategy` base class
- Uses existing typed channel infrastructure
- Uses existing `Edge` class
- No external packages needed

---

## Rollout Plan

### Stage 1: Soft Launch

1. Integrate core implementation
2. Add comprehensive tests
3. Document in analysis docs
4. Mark as "Available" but not "Recommended"

### Stage 2: Validation

1. Internal usage in POC tests/benchmarks
2. Performance validation
3. API feedback collection

### Stage 3: Promotion

1. Update docs to "Recommended" for content-based routing
2. Add to routing guide prominently
3. Create migration guide from broadcast-and-filter
4. Consider deprecating broadcast-and-filter (optional)

---

## References

### Research Documentation

- Research findings: `/research/selective-content-routing/README.md`
- Analysis: `/poc/docs/analysis/routing/selective-routing-analysis.md`
- Research plan: `/research/selective-content-routing/research-plan.md`
- Option 1 analysis: `/research/selective-content-routing/notes/option1-analysis.md`

### Prototype Code (to be saved in handover/prototype/)

- Implementation: `/poc/DataFlow.POC/Core/SelectiveRoutingEdgeStrategy.cs` (RESEARCH PROTOTYPE)
- Tests: `/poc/DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs` (RESEARCH PROTOTYPE)
- Benchmarks: `/poc/DataFlow.POC.Benchmarks/SelectiveRoutingBenchmark.cs` (RESEARCH PROTOTYPE)

### Existing Code

- Edge strategy base: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`
- Edge class: `/poc/DataFlow.POC/Core/Edge.cs`
- Current routing: `/poc/DataFlow.POC/Blocks/RouterBlock.cs`

---

## Notes

**Architecture Compatibility**:
- ✅ Extends existing `EdgeStrategy` pattern
- ✅ Uses typed channels (no boxing)
- ✅ Maintains pull-based semantics
- ✅ Supports backpressure
- ✅ No breaking changes

**Type Safety**:
- ✅ Route selector is `Func<TItem, string>` - typed at construction
- ✅ Type checking at edge creation time
- ✅ Clear compile-time errors for type mismatches

**Error Handling**:
- ✅ Unknown routes throw `InvalidOperationException`
- ✅ Clear error messages with available routes listed
- ✅ Configuration errors fail fast at edge creation
