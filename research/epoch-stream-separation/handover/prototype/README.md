# Prototype Reference Implementations

This folder contains the key prototype implementations from the research phase. These files serve as reference implementations for the decoupled epoch segmentation design.

## Files

### Core Interfaces

**`IPlainSourceActor.cs`** - Source actor interface without epoch knowledge
- Plain sources emit `IAsyncEnumerable<T>` instead of epoch streams
- Simpler interface focused solely on data production
- Enables source reusability across different epoch strategies

### Block Implementations

**`PlainSourceBlock.cs`** - Block that hosts plain source actors
- Wrapper block for `IPlainSourceActor<T>`
- Manages DI scope and actor lifetime
- Outputs plain data stream for downstream processing

**`EpochSegmenterBlock.cs`** - External epoch segmentation block
- Applies epoch segmentation policies to plain streams
- Supported policies: None, Count, Key, Clock, Custom
- Converts `IAsyncEnumerable<T>` → `IAsyncEnumerable<IEpochStream<T>>`

### Tests

**`DecoupledEpochTests.cs`** - Functional validation tests
- 6 tests demonstrating correctness and functional equivalence
- Tests for different segmentation policies
- Validates that decoupled design produces identical results to source-centric approach

## Usage Example

```csharp
// Define a plain source (no epoch knowledge)
public class OrderSource : PlainSourceActorBase<Order>
{
    public override async IAsyncEnumerable<Order> ProduceAsync(IActorExecutionContext context)
    {
        return FetchOrders(); // Just produce data
    }
}

// Configure pipeline with external segmentation
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(
        order => order.Date.Date,
        "order-source"));

// Pipeline: source → segmenter → transform → sink
```

## Implementation Notes

These are **prototype reference implementations** that demonstrate the feasibility and approach. During actual implementation:

1. **Review and refine**: The implementation team should review these for production readiness
2. **Add error handling**: Production code needs more robust error handling
3. **Optimize if needed**: Hot path optimization may be beneficial
4. **Add logging**: Consider adding diagnostic logging
5. **Update tests**: Expand test coverage as needed

## Related Documentation

- **Implementation Issue**: `../github-issue-implement-decoupled-epochs.md`
- **Design Document**: `../../design/decoupled-epoch-architecture.md`
- **ADR**: `../../adr/2025-11-05-decoupled-epoch-segmentation.md`
- **Research Findings**: `../../README.md`

---

**Note**: These files were captured from the POC codebase during the research phase and are provided as reference implementations. They will be removed from the POC codebase during code reversion, but preserved here for the implementation team.
