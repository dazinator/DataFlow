# Research Plan: Epoch Test Coverage Analysis

## Research Objective

Analyze current test coverage for epoch propagation across different DataFlow topologies to answer:
- Do we have tests that verify epoch boundaries are correctly maintained when a source emits incrementing epoch sequences?
- Do competing consumer actors pulling from buffer nodes correctly scope their processing within each epoch?
- Are there sufficient integration tests covering all main block types and edge strategies with multi-epoch scenarios?

## Research Questions

1. **Multi-Epoch Transition Coverage**: Do we have tests where a source emits multiple incrementing epochs (epoch 1, 2, 3, etc.) and verify that downstream blocks correctly transition between them?

2. **Buffer Node + Competing Consumers**: Do we have tests where:
   - A source emits incrementing epochs into a buffer node
   - Multiple consumer actors pull from that buffer node concurrently
   - Each epoch boundary is correctly observed by all consumers
   - Consumer actor scopes are properly instantiated per epoch

3. **Topology Coverage Matrix**: Do we have sufficient test coverage across:
   - **Block Types**: BufferBlock, BatchBlock, TransformBlock, ProcessorBlock, RoutingBlock, MergeBlock
   - **Edge Strategies**: Direct, Broadcast, Selective Routing
   - **Epoch Scenarios**: Single epoch, multi-epoch transitions, epoch merging
   - **Concurrency**: Single consumer, competing consumers

4. **Epoch Stream Lifetime**: Do tests validate that:
   - EpochStream instances are coupled to epoch lifetime
   - New epochs trigger new EpochStream instantiation
   - Downstream participants see new epochs as the source emits them

## Success Metrics

- **Comprehensive Inventory**: Complete list of all epoch-related tests (~149 found)
- **Coverage Matrix**: Document showing which topology + epoch scenario combinations are tested
- **Gap Identification**: Clear list of missing test scenarios
- **Validation Confidence**: Can point to specific tests covering the requested scenario (buffer + competing consumers + multi-epoch)

## Validation Approach

1. **Test Inventory Phase**:
   - List all epoch-related test files and test methods
   - Categorize by what they test (block type, topology, epoch scenario)

2. **Coverage Analysis Phase**:
   - Create matrix of: Block Types × Edge Strategies × Epoch Scenarios × Concurrency Patterns
   - Mark which combinations have existing tests
   - Identify gaps

3. **Specific Scenario Validation**:
   - Search for tests matching: source → buffer → competing consumers + multi-epoch
   - Verify epoch boundary and scope handling in those tests

4. **Documentation Phase**:
   - Create comprehensive coverage matrix
   - Document findings and gaps
   - Provide specific test references for covered scenarios

## Expected Outcomes

- Research documentation in `/research/epoch-test-coverage/`
- Coverage matrix document showing test distribution across topologies
- Specific answers to the research questions
- Implementation-ready issue if gaps are identified
- Clear recommendations for additional test scenarios (if needed)

## Timeline

**Estimated Duration**: 1-2 days

- Day 1: Test inventory and coverage analysis
- Day 2: Documentation and handover preparation
