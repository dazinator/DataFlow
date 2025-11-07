# Research Plan: Better Testing Approaches

## Research Objective

To comprehensively evaluate and improve the testability of DataFlow library, covering both POC and production codebases. This research will examine current testing patterns, identify areas of duplication, explore how end users should test complex dataflows, and assess whether design changes could improve testability.

## Research Questions

### 1. Current Testing Patterns Analysis
- What patterns are currently used in POC and production tests?
- Where is there duplication in test helper blocks and actors?
- Can we identify common patterns that could be abstracted into reusable test utilities?
- Are there consistency issues across the test suites?

### 2. End-User Testing Guidance
- How should users functionally test large, complex dataflows (e.g., 10+ blocks)?
- For business logic embedded in dataflows (e.g., cashflow aggregation by company code and value date, conditional database writes):
  - Should business logic be decoupled from dataflow blocks for easier testing?
  - How can users mock/test routing, merging, and other native block behaviors?
  - What patterns enable unit testing of individual components vs integration testing of full pipelines?

### 3. Design Evaluation
- **ActorBlock Naming**: Should `ActorBlock` be renamed to `ScopeBlock` (same for `EpochActorBlock`)?
  - Does "Actor" accurately describe the block's purpose?
  - Would "Scope" better convey DI scope management?
  - Are there other naming alternatives that improve clarity?

- **Actor Interface Design**: Current `IStreamActor<TIn, TOut>` pattern
  - Takes `IAsyncEnumerable<TIn>` and returns `IAsyncEnumerable<TOut>`
  - Actor controls enumeration, providing flexibility (filtering, transformation, cardinality changes)
  - **Question**: Does this flexibility come at the cost of testability?
    - Are actors harder to mock/stub during testing?
    - Would a simpler interface (e.g., `Task<TOut> ProcessAsync(TIn input)`) be more testable?
    - What would be the trade-offs of a simpler interface?

### 4. Testing Framework Enhancements
- Are there opportunities for test helper libraries or base classes?
- Could we provide testing utilities for common scenarios (e.g., collecting outputs, asserting order, timing validation)?
- What patterns from mature testing frameworks (e.g., xUnit, Shouldly) could we adopt?

## Validation Approach

### Phase 1: Current State Analysis
1. **Survey existing tests**:
   - Catalog test patterns in POC (`DataFlow.POC.Tests`)
   - Catalog test patterns in production (`src/Tests`)
   - Identify duplication and inconsistencies
   - Document common test helper patterns

2. **Measure test coverage and complexity**:
   - Analyze test file sizes and complexity
   - Identify tests that are difficult to understand or maintain

### Phase 2: Real-World Scenario Creation
1. **Build non-trivial dataflow scenario**:
   - Implement a realistic business scenario (e.g., cashflow processing pipeline)
   - Include: aggregation, routing, conditional logic, database operations
   - Document challenges encountered during testing

2. **Test the scenario using current patterns**:
   - Write tests for the scenario using existing approaches
   - Document pain points and difficulties
   - Identify what's hard to test and why

### Phase 3: Alternative Approaches
1. **Prototype alternative actor interfaces**:
   - Create simpler actor interfaces for comparison
   - Implement same scenario with alternative designs
   - Compare testability and flexibility

2. **Develop test helper utilities**:
   - Create reusable test helpers based on identified patterns
   - Prototype base test classes or fixtures
   - Test these utilities with existing test scenarios

### Phase 4: Comparative Analysis
1. **Compare approaches**:
   - Testability: ease of mocking, isolation, assertions
   - Flexibility: ability to express complex logic
   - Maintainability: test readability and DRY principles
   - Performance: impact on test execution time

2. **Gather metrics**:
   - Lines of test code required
   - Number of test helpers needed
   - Cognitive complexity of tests

## Expected Outcomes

### Documentation Deliverables
- [ ] Comprehensive analysis of current testing patterns in `/research/testing-approaches/README.md`
- [ ] Catalog of test duplication and opportunities for consolidation
- [ ] Real-world testing scenario with challenges documented
- [ ] Comparison of actor interface designs for testability
- [ ] Test helper library design proposals
- [ ] Naming recommendations (ActorBlock vs ScopeBlock)

### Design Documentation
- [ ] Design document for recommended testing patterns in `/research/testing-approaches/design/`
- [ ] Test utilities architecture design
- [ ] User testing guidance document (how to test dataflows)

### ADRs
- [ ] ADR on ActorBlock naming decision (in `/poc/docs/adr/`)
- [ ] ADR on actor interface design trade-offs (in `/poc/docs/adr/`)
- [ ] ADR on test helper utilities approach (if applicable)

### Implementation Handover
- [ ] Implementation-ready issue in `/research/testing-approaches/handover/`
- [ ] Prototype code for test utilities in `/research/testing-approaches/handover/prototype/`
- [ ] Guidance document for end users on testing dataflows

### Code Exploration (to be reverted after review)
- Create real-world testing scenario in POC or src (for validation)
- Prototype alternative actor interfaces
- Prototype test helper utilities
- Implement examples of recommended patterns

## Timeline

**Estimated Duration**: 3-5 days

**Phase Breakdown**:
- Phase 1 (Analysis): 1 day
- Phase 2 (Scenario Creation): 1 day  
- Phase 3 (Alternatives): 1-2 days
- Phase 4 (Comparison & Documentation): 1 day

## Success Criteria

- [ ] Clear understanding of current testing pain points
- [ ] Real-world scenario demonstrates testing challenges
- [ ] Concrete recommendations for improving testability
- [ ] Actionable guidance for end users on testing dataflows
- [ ] Decision on ActorBlock naming backed by analysis
- [ ] Decision on actor interface design backed by testability comparison
- [ ] Identified opportunities for test helper utilities
- [ ] Implementation-ready issue contains complete context for engineering team
