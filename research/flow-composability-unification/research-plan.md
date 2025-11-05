# Research Plan: Flow Composability Unification with and without Epochs

## Research Objective

Investigate and validate approaches to enable seamless composability between plain data streams and epoch-structured streams in DataFlow pipelines, addressing the composability gap introduced by the decoupled epoch segmentation (PR #146).

## Problem Statement

The recent decoupled epoch segmentation separated epoch concerns from sources, introducing:
- **PlainSourceBlock**: Produces `IAsyncEnumerable<T>` (plain streams)
- **EpochSegmenterBlock**: Converts plain streams to `IAsyncEnumerable<IEpochStream<T>>` (epoch streams)

However, this creates a composability challenge:
- **Plain blocks** (TransformerBlock, ProcessorBlock, BatchBlock) work with `IAsyncEnumerable<T>`
- **Epoch-aware blocks** (WriteContextBlock) work with `IAsyncEnumerable<IEpochStream<T>>`
- Without a segmenter, downstream blocks expecting epochs cannot connect to plain sources
- This breaks the promise of flexible pipeline composition

## Research Questions

1. **What patterns enable blocks to work with both plain and epoch streams?**
   - Can we create epoch-aware wrapper blocks?
   - Should we provide adapters to lift plain blocks?
   - What are the design trade-offs?

2. **How do we preserve epoch boundaries while processing items?**
   - How do transformations work within epochs?
   - How do batching operations respect epoch boundaries?
   - What are the performance implications?

3. **What composability patterns should we support?**
   - Plain-only pipelines (no epochs)
   - Epoch-only pipelines (full epoch structure)
   - Mixed pipelines (segmentation applied mid-stream)

4. **What are the implementation requirements?**
   - Which block types need epoch-aware versions?
   - What are the testing requirements?
   - What documentation is needed?

## Validation Approach

### Prototype Development
- Create prototype epoch-aware blocks in POC codebase
- Validate that transformations preserve epoch boundaries
- Ensure batching respects epoch boundaries (no cross-epoch batches)
- Test streaming semantics (no unnecessary buffering)

### Test Scenarios
- Single epoch with multiple items
- Multiple epochs with varying item counts
- Transformations (1-to-1, 1-to-many, filtering)
- Batching within epoch boundaries
- Complex pipeline compositions

### Performance Validation
- Measure overhead of epoch-aware wrappers
- Validate streaming performance (no buffering)
- Compare with plain block performance

## Expected Outcomes

### Research Documentation
- `/research/flow-composability-unification/README.md` - Research findings
- `/research/flow-composability-unification/design/` - Architecture documentation
- `/research/flow-composability-unification/adr/` - Architecture decisions

### Prototype Code
- `/research/flow-composability-unification/handover/prototype/` - Reference implementations:
  - EpochTransformerBlock.cs
  - EpochProcessorBlock.cs
  - EpochBatchBlock.cs
  - EpochAwareBlockTests.cs

### Implementation Handoff
- `/research/flow-composability-unification/handover/github-issue-implement-epoch-aware-blocks.md`
  - Complete implementation specification
  - Test scenarios and requirements
  - Design references and patterns
  - Performance requirements

### Documentation Templates
- Block usage patterns
- Composability examples
- Best practices guide

## Timeline

- **Phase 1**: Research and prototyping (Complete)
- **Phase 2**: Documentation and findings (In Progress)
- **Phase 3**: Implementation handoff creation (In Progress)
- **Phase 4**: Code reversion (Pending reviewer approval)

## Success Criteria

- [ ] Prototype demonstrates feasibility of epoch-aware blocks
- [ ] All test scenarios pass
- [ ] Documentation is comprehensive and clear
- [ ] Implementation issue provides complete specification
- [ ] Prototype code available as reference for implementation
- [ ] POC code changes reverted (after reviewer approval)
