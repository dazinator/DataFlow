# Research Plan: Selective Content-Based Routing

**Date**: 2025-11-20  
**Research Issue**: [#512](https://github.com/uniun-technology/lib-dataflow/issues/512)  
**Research Type**: Approach validation and design exploration  
**POC Target**: `/poc` folder

---

## Research Objective

Determine the optimal approach for implementing selective content-based routing that eliminates the inefficiency of the current broadcast-and-filter pattern while maintaining type safety, testability, and performance.

---

## Research Questions

### Primary Questions

1. **Which architectural approach is best for selective content routing?**
   - Option 1: Edge strategy pattern (SelectiveRoutingEdgeStrategy)
   - Option 2: Enhanced block pattern (SelectiveRouterBlock with direct channel access)

2. **What are the performance characteristics of each approach?**
   - CPU overhead reduction vs current broadcast-and-filter
   - Memory pressure reduction (allocation overhead)
   - Throughput improvements with N routes

3. **How do the approaches compare in terms of maintainability and extensibility?**
   - Separation of concerns
   - Testability
   - API complexity
   - Future extensibility (dynamic routes, complex routing logic)

### Secondary Questions

4. **Can we eliminate the `RoutedItem<T>` record allocation?**
   - Is this allocation necessary for selective routing?
   - Performance impact of eliminating it

5. **How does each approach handle error cases?**
   - Unknown route keys
   - Type safety violations
   - Configuration errors

---

## Success Metrics

### Quantitative Metrics

**Baseline (Current Broadcast-and-Filter)**:
- With N routes, broadcasts to N channels concurrently
- Allocates 1 `RoutedItem<T>` per item
- Each route filter processes 100% of items, drops (N-1)/N items

**Target Performance** (for N=5 routes with 10,000 items/sec):
- ✅ **Zero wasted filtering** - each item sent only to matching route
- ✅ **Reduced allocations** - ideally zero additional records (or at most 1 vs N)
- ✅ **Better throughput** - >2x throughput improvement with N>3 routes
- ✅ **Lower CPU** - <50% CPU usage vs broadcast-and-filter

### Qualitative Metrics

- ✅ **Maintains type safety** - compile-time type checking
- ✅ **Testable** - clear separation of concerns, easy to unit test
- ✅ **Clear API** - simple and intuitive for users
- ✅ **Consistent** - fits well with existing POC architecture
- ✅ **Documented** - clear examples and guidance

### Validation Approach

**Comparative Testing**:
- Create "before/after" benchmark tests
- Measure CPU, memory, and throughput for 2, 5, and 10 routes
- Validate correctness with integration tests

**Prototyping**:
- Working prototype for each approach
- Integration tests demonstrating routing correctness
- Benchmark tests measuring performance

---

## Validation Approach

### Phase 1: Prototype Development (Days 1-3)

**Option 1: SelectiveRoutingEdgeStrategy**
1. Implement edge strategy that routes based on route selector function
2. Integrate with existing edge architecture
3. Create unit tests

**Option 2: Enhanced RouterBlock**
1. Implement RouterBlock variant with direct channel access
2. Eliminate RouteFilterBlock dependency
3. Create unit tests

### Phase 2: Comparative Testing (Days 4-5)

**Benchmarks**:
- Throughput: items/second with 2, 5, 10 routes
- Memory: allocation overhead per item
- CPU: processing time per item
- Comparison vs current broadcast-and-filter

**Integration Tests**:
- Correctness validation
- Error handling
- Complex routing scenarios

### Phase 3: Analysis and Documentation (Days 6-7)

**Analysis**:
- Performance comparison tables
- Design trade-offs analysis
- Recommendation with justification

**Documentation**:
- Research findings document
- ADR (if design decision warrants it)
- Implementation guidance

---

## Expected Outcomes

### Research Deliverables

1. **Research Documentation** (`/research/selective-content-routing/`)
   - Research plan (this document)
   - Research findings (README.md)
   - Design comparison analysis
   - Benchmark results

2. **Formal Documentation** (NOT reverted)
   - Analysis document: `/poc/docs/analysis/routing/selective-routing-analysis.md`
   - ADR (if needed): `/poc/docs/adr/YYYY-MM-DD-selective-routing-strategy.md`

3. **Implementation Handover**
   - Implementation-ready work item with specifications
   - Saved prototype code in `/research/selective-content-routing/handover/prototype/`
   - Test scenarios and benchmarks

4. **Exploratory Code** (REVERTED after approval)
   - Prototype implementations in `/poc/DataFlow.POC/`
   - Benchmark tests in `/poc/DataFlow.POC.Benchmarks/`
   - Integration tests in `/poc/DataFlow.POC.Tests/`

### Decision Criteria

**Select Option 1 (Edge Strategy) if**:
- Better performance with existing architecture
- Cleaner separation of concerns
- Easier to test and maintain
- More flexible for future enhancements

**Select Option 2 (Enhanced Block) if**:
- Better performance overall
- Simpler API for users
- More intuitive mental model
- Easier integration with existing code

**Hybrid Approach if**:
- Both approaches have compelling benefits
- Can be combined for better overall solution

---

## Timeline

**Total Duration**: 7 days

| Phase | Duration | Activities |
|-------|----------|------------|
| **Phase 1: Setup** | Day 1 | Research plan, folder structure, baseline analysis |
| **Phase 2: Prototype Option 1** | Days 2-3 | SelectiveRoutingEdgeStrategy implementation & tests |
| **Phase 3: Prototype Option 2** | Days 3-4 | Enhanced RouterBlock implementation & tests |
| **Phase 4: Benchmarking** | Day 5 | Performance comparison, analysis |
| **Phase 5: Documentation** | Days 6-7 | Findings, ADR, handover preparation |

**Target Completion**: 2025-11-27

---

## Risk Assessment

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Edge strategy incompatible with current arch | Low | High | Early prototype to validate integration |
| Performance gains insufficient | Medium | Medium | Benchmark early, pivot if needed |
| Type safety issues with generic routing | Low | High | Leverage existing typed channel infrastructure |
| Complex API increases learning curve | Medium | Low | Focus on clear examples and documentation |

### Schedule Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Benchmarking takes longer than expected | Medium | Low | Use existing benchmark infrastructure |
| Both approaches have trade-offs requiring more analysis | Medium | Medium | Document trade-offs clearly, recommend best fit for common cases |

---

## Out of Scope

- **Dynamic route creation** - This research focuses on static routes defined at build time
- **Multi-block routes** - Complex routing with multiple blocks per route (that's Structured Routing in `/src`)
- **Production code changes** - This research is POC-only (`/poc` folder)
- **Backwards compatibility** - Can introduce breaking changes in POC if needed for better design

---

## References

### Related Issues
- [#512](https://github.com/uniun-technology/lib-dataflow/issues/512) - This research work item
- [#510](https://github.com/uniun-technology/lib-dataflow/issues/510) - Routing analysis identifying the gap
- [#75](https://github.com/uniun-technology/lib-dataflow/issues/75) - Original selective routing concern

### Existing Documentation
- `/poc/docs/analysis/routing/README.md` - Current routing analysis
- `/poc/docs/analysis/routing/COMPARISON_TABLES.md` - Routing mechanism comparison
- `/poc/DataFlow.POC/Core/EdgeStrategy.cs` - Edge strategy architecture
- `/poc/DataFlow.POC/Blocks/RouterBlock.cs` - Current routing implementation

---

## Notes

**Architecture Constraints**:
- Must use typed channels (no boxing)
- Must fit within existing edge/block architecture
- Should maintain pull-based semantics
- Should support backpressure

**Design Principles**:
- Separation of concerns (routing logic vs business logic)
- Type safety (compile-time checking)
- Testability (unit testable components)
- Performance (eliminate waste, reduce allocations)
