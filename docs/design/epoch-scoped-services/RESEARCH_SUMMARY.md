# Research Summary: Epoch-Scoped Services Design

**Research Type**: Design Document  
**Status**: Complete  
**Duty**: Research  
**Date**: 2025-11-13

---

## Objective

Design a higher-level abstraction for epochs that allows blocks to:
1. Access an **epoch object** representing the current epoch's lifetime
2. Resolve **services scoped to the epoch** via dependency injection
3. Share **service instances** across concurrent blocks processing the same epoch
4. Automatically handle **epoch vector subsume operations**

This simplifies block development by hiding low-level epoch tracking concerns and providing a clean DI scope per epoch.

---

## Research Approach

### Method

1. **Analyzed Current Implementation**
   - Reviewed EpochAnchoringDemo POC code
   - Studied `IEpochLifecycleObserver` and `IEpochLifecycleParticipant` patterns
   - Examined `WriteContextBlock` pattern for epoch-scoped DbContext
   - Identified manual tracking complexity in blocks

2. **Explored Alternatives**
   - Manual tracking (current state)
   - AsyncLocal ambient context pattern
   - Epoch object with DI scope (proposed)
   - Block-level DI scopes
   - Hybrid approach

3. **Designed Solution**
   - Defined core interfaces (`IEpoch`, `IEpochManager`)
   - Designed lifecycle management and reference counting
   - Addressed epoch vector subsume semantics
   - Created integration patterns for blocks

4. **Evaluated Trade-Offs**
   - Weighted decision matrix across 6 criteria
   - Detailed comparison of 5 alternative approaches
   - Analyzed block lifetime models

---

## Key Findings

### Current State Pain Points

❌ **High Duplication**: Each block reimplements epoch tracking  
❌ **Cannot Share Resources**: Blocks can't share epoch-scoped services  
❌ **Complexity**: Manual handling of epoch vector subsume operations  
❌ **No DI Integration**: Can't use standard DI patterns  

### Recommended Solution

✅ **Explicit Epoch Object**: Passed via `IBlockContext.CurrentEpoch`  
✅ **DI Scope Per Epoch**: Each epoch creates its own `IServiceScope`  
✅ **Service Sharing**: Multiple blocks access same service instances  
✅ **Automatic Lifecycle**: Framework manages reference counting and disposal  

### Key Design Decisions

1. **Epoch Object vs AsyncLocal**: Chose explicit epoch object for better testability and clearer dependencies

2. **Reference Transfer for Subsume**: When epoch A is subsumed into epoch B, transfer the epoch object reference rather than merging scopes

3. **Singleton Blocks**: Blocks remain singleton-like (graph lifetime), resolve epoch-scoped services on demand

4. **Standard Scoped Lifetime**: Use existing `AddScoped` registration; epoch's DI scope provides "one per epoch" semantics

---

## Deliverables

### Design Documentation

All documents created in `/docs/design/epoch-scoped-services/`:

1. **README.md** (Main Design Document)
   - Complete architecture proposal
   - Core interfaces and implementation
   - 6-phase implementation plan
   - Success criteria and testing strategy

2. **subsume-semantics.md**
   - Detailed subsume operation handling
   - Reference counting strategy
   - Test scenarios and edge cases
   - Design decision rationale

3. **di-integration.md**
   - Service registration patterns
   - Common scenarios (shared state, transactions, pooling)
   - Testing patterns
   - Performance considerations
   - Best practices (DOs and DON'Ts)

4. **alternatives/comparison.md**
   - 5 alternative approaches analyzed
   - Detailed pros/cons for each
   - Weighted decision matrix (Epoch Object scored 82/100)
   - Trade-off analysis

5. **block-lifetime-analysis.md**
   - Exploratory analysis: Should blocks be epoch-scoped?
   - Evaluated per-epoch block creation vs singleton blocks
   - **Recommendation**: Singleton blocks + epoch-scoped services
   - Use case evaluation

### Code Impact

**No prototype code created** - This is a design document, not a full research with prototype.

**Estimated Changes** (for implementation):
- **New components**: 4-6 new classes/interfaces
- **Modified components**: 3-4 existing POC classes
- **Lines of code**: ~500-800 LoC estimated

---

## Recommended Approach

### Architecture Summary

```
┌─────────────────────────────────────────────────────────────┐
│                     Epoch Architecture                       │
└─────────────────────────────────────────────────────────────┘

IEpoch Interface
  ├── EpochVector Vector (identification)
  ├── IServiceProvider ServiceProvider (DI scope)
  └── T GetService<T>() (service resolution)

IEpochManager
  ├── GetOrCreateEpoch(vector) → IEpoch
  ├── NotifyEpochCompleted(vector, block)
  └── NotifyEpochSubsumed(from, to)

Integration
  ├── IEpochCompatibleBlock (marker interface)
  └── IBlockContext.CurrentEpoch (provides epoch to blocks)

Lifecycle
  ├── Epoch created → DI scope created
  ├── Blocks access → GetService<T>()
  ├── Block completes → Decrement ref count
  └── All refs released → Dispose scope
```

### Key Patterns

**Service Registration**:
```csharp
services.AddScoped<OrderDbContext>();  // or AddEpochScoped for clarity
```

**Service Resolution**:
```csharp
var epoch = _context.CurrentEpoch;
var dbContext = epoch.GetService<OrderDbContext>();
```

**Shared State**:
```csharp
// Block A
var metrics = epoch.GetService<EpochMetrics>();
metrics.Count++;

// Block B (same epoch)
var metrics = epoch.GetService<EpochMetrics>();
// Same instance! See Block A's changes
```

---

## Implementation Plan

### 6 Phases (3-4 weeks estimated)

**Phase 1** (Week 1): Core epoch object infrastructure  
**Phase 2** (Week 1-2): Block integration and context  
**Phase 3** (Week 2): Epoch vector subsume support  
**Phase 4** (Week 2-3): DI registration helpers  
**Phase 5** (Week 3): Refactor EF Core examples  
**Phase 6** (Week 3-4): Comprehensive testing  

See main design document for detailed tasks per phase.

---

## Success Criteria

| Criterion | Target | Validation Method |
|-----------|--------|-------------------|
| **Service Sharing** | Multiple blocks access same instance | Integration test with 3+ blocks |
| **Lifecycle** | Services disposed when epoch completes | Memory profiling |
| **Subsume** | Lifetime extends through subsume ops | Unit tests |
| **No Races** | Concurrent access is safe | Stress tests |
| **Performance** | Overhead < 5% vs manual | Benchmarks |
| **Memory** | Usage comparable (±10%) | Profiling |
| **DevEx** | Code simpler than manual | Code comparison |
| **EF Core** | DbContext epoch-scoped works | Integration test |

---

## Benefits

### For Block Developers

✅ **Simpler Code**: No manual epoch tracking  
✅ **Shared Resources**: Easy to share state across blocks  
✅ **Standard DI**: Use familiar `AddScoped` pattern  
✅ **Automatic Cleanup**: Framework handles disposal  

### For Library Maintainers

✅ **Less Duplication**: One implementation of epoch management  
✅ **Consistent Behavior**: All blocks use same mechanism  
✅ **Testable**: Mock `IEpoch` for testing  
✅ **Foundation for #125**: Enables epoch-scoped transactions  

### Example Simplification

**Before (Manual)**:
```csharp
await using var dbContext = new DemoDbContext(_dbOptions);
await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
// ... process items
await dbContext.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

**After (Epoch-Scoped)**:
```csharp
var epoch = _context.CurrentEpoch;
var dbContext = epoch.GetService<DemoDbContext>();
// ... process items
// Automatic commit and disposal at epoch completion
```

**LoC Reduction**: ~50% less code per block  
**Complexity Reduction**: Block developers don't need to understand epoch lifecycle

---

## Open Questions

1. **Subsume Strategy**: Reference transfer chosen, but edge cases may emerge during implementation

2. **Reference Counting**: Reactive tracking (as blocks request epochs) is proposed, but may need refinement

3. **Error Recovery**: What happens if a block fails? (Needs implementation-time decisions)

4. **Performance**: Estimated < 5% overhead, but needs benchmarking

These questions are documented in the design and should be addressed during implementation.

---

## Recommendations

### For Implementation Team

1. **Start with Phase 1**: Implement core infrastructure first, validate design
2. **Create Comprehensive Tests**: Tests from success criteria table
3. **Benchmark Early**: Validate performance assumptions
4. **Refactor Incrementally**: Use EF Core example to demonstrate value
5. **Document Patterns**: Create usage examples as you implement

### For Future Work

This design enables:
- **Epoch-scoped EF Core transactions** (#125)
- **Epoch-scoped metrics aggregation**
- **Epoch-scoped caching**
- **Simplified block development patterns**

---

## Conclusion

**Status**: ✅ Design Complete

**Recommendation**: **Proceed with implementation** using the proposed epoch object with DI scope approach.

**Rationale**:
- Best balance of explicitness, testability, and developer experience
- Leverages standard DI patterns
- Simplifies block development
- Provides foundation for future features

**Next Steps**:
1. Team review of design documents
2. Address any open questions
3. Create implementation work item
4. Begin Phase 1 implementation

---

## Related Documentation

- **Main Design**: `/docs/design/epoch-scoped-services/README.md`
- **Subsume Semantics**: `/docs/design/epoch-scoped-services/subsume-semantics.md`
- **DI Integration**: `/docs/design/epoch-scoped-services/di-integration.md`
- **Alternatives Analysis**: `/docs/design/epoch-scoped-services/alternatives/comparison.md`
- **Block Lifetime Analysis**: `/docs/design/epoch-scoped-services/block-lifetime-analysis.md`

---

**Research Complete**: 2025-11-13  
**Reviewed By**: Pending  
**Status**: Ready for implementation handover
