# Research Plan: Block Context Constructor Injection

## Research Objective

Improve the IBlockContext initialization pattern by enabling constructor injection instead of post-construction SetContext method calls. The goal is to eliminate reflection-based context setting and make the pattern more idiomatic for dependency injection.

## Research Questions

1. Can we pass IBlockContext through block constructors without breaking the terse registration API?
2. How do we handle blocks that need both DI dependencies AND block context in their constructors?
3. What is the impact on existing block implementations?
4. Are there alternative patterns that achieve the same goal more elegantly?
5. How does this affect the typed helper methods (e.g., AddActorBlock)?

## Success Metrics

### Quantitative
- No performance regression in block creation time
- Same or fewer lines of code in registration methods
- Reduced reflection usage (from current usage to zero)

### Qualitative
- More idiomatic dependency injection pattern
- Clearer constructor signatures
- Better discoverability for developers
- Maintains backward compatibility where possible

### Baseline
- Current: SetContext called via reflection in AddActorBlock
- Current: 8 lines of code in AddActorBlock factory registration
- Current: Block context not available during construction

### Validation
- Prototype implementations
- Comparison of registration code
- Test coverage for new pattern
- Migration path documentation

## Validation Approach

1. **Prototype Current Approach**: Document exact current implementation
2. **Prototype Proposed Approach**: Constructor injection with typed helpers
3. **Prototype Alternative Approaches**: 
   - Factory pattern variations
   - Builder pattern
   - Hybrid approaches
4. **Comparative Analysis**: 
   - Code complexity
   - API ergonomics
   - Performance
   - Maintainability
5. **Create Demo Tests**: Before/after examples showing the improvement

## Expected Outcomes

- Research documentation in `/research/block-context-constructor-injection/`
- Implementation-ready work item with detailed specifications
- Prototype code demonstrating the recommended approach
- Migration guide for existing block implementations
- Analysis document comparing all explored approaches

## Timeline

- Day 1-2: Analysis of current implementation and exploration
- Day 2-3: Prototype development and comparative testing
- Day 3-4: Documentation and handover preparation

## Constraints

- Must maintain API terseness (no name duplication)
- Must work with DI container and scoped services
- Should not break existing code patterns (backward compatibility preferred)
- Must support namespace-based registration
