# Research Plan: Clarify Status of EpochSegmenterBlock

## Research Objective

Determine whether `EpochSegmenterBlock` and `EpochSegmentationPolicy` are superseded by the newer epoch architecture (`ConfigureEpochs` API and `EpochSourceBlock`), and if so, create a comprehensive plan for their removal from the codebase.

## Research Context

The issue suggests that `EpochSegmenterBlock` may relate to an earlier implementation where epochs were optional components added at the block level during DAG composition. The architecture has since pivoted to making epochs a mandatory, native concept enabled by default at the graph level.

**Key Hypothesis**: With the mandatory epochs architecture:
- Graphs not requiring epoch-segmented streams simply use a single epoch by default
- This eliminates the need for a separate epoch segmenter block concept
- The `ConfigureEpochs` API provides graph-level epoch configuration
- `EpochSourceBlock` produces epoch streams natively

## Research Questions

### 1. Current Usage Analysis
- Where is `EpochSegmenterBlock` currently used in the codebase?
- Where is `EpochSegmentationPolicy` currently used?
- What are the primary use cases for these components?

### 2. Architectural Comparison
- How does `EpochSegmenterBlock` relate to the `ConfigureEpochs` API?
- How does it relate to `EpochSourceBlock` and source actors?
- Is there functional overlap or are they complementary?

### 3. Migration Strategy
- If superseded, how should existing usages be migrated?
- What is the recommended approach for users who need epoch segmentation?
- Are there any unique capabilities that need to be preserved?

### 4. Test and Benchmark Value
- Which tests using `EpochSegmenterBlock` provide unique value?
- Which benchmarks should be preserved (possibly refactored)?
- Which tests/benchmarks are redundant with newer epoch tests?

### 5. Removal Impact
- What would be the impact of removing these components?
- What breaking changes would occur?
- What migration guidance would users need?

## Success Metrics

**Quantitative**:
- Number of usage sites identified
- Number of tests affected
- Number of benchmarks affected
- Lines of code to be removed

**Qualitative**:
- Clear understanding of architectural relationship
- Comprehensive migration strategy documented
- Identification of unique vs redundant tests
- Clear removal plan with minimal disruption

**Validation**:
- Research findings reviewed against mandatory epochs ADR
- Migration strategy validated against similar past migrations
- Stakeholder review of removal impact

## Validation Approach

1. **Code Analysis**: Use grep to find all usages across codebase
2. **Documentation Review**: Check design docs and ADRs
3. **Test Analysis**: Review test files for coverage and uniqueness
4. **Benchmark Analysis**: Evaluate benchmark value and coverage
5. **Architectural Comparison**: Compare with `ConfigureEpochs` and `EpochSourceBlock`
6. **Migration Validation**: Propose migration patterns and validate feasibility

## Expected Outcomes

1. **Research Documentation**: Complete analysis in `/research/epoch-segmenter-clarification/`
2. **Findings Report**: Clear verdict on whether components are superseded
3. **Removal Strategy**: If superseded, detailed plan for removal
4. **Test Preservation Plan**: Identification of tests/benchmarks to refactor vs remove
5. **Implementation Handover**: Work item with complete specifications for implementation
6. **Formal Documentation**: Updates to relevant ADRs if needed

## Timeline

- **Day 1**: Code analysis and usage identification
- **Day 2**: Architectural comparison and findings documentation
- **Day 3**: Migration strategy and test analysis
- **Day 4**: Implementation handover creation and documentation
- **Day 5**: Review, feedback, and finalization

## Related Research

- `/research/mandatory-epochs/` - Mandatory epochs architecture
- `/docs/adr/poc/2025-11-20-mandatory-epochs-unified-architecture.md` - ADR for unified architecture
- `/research/epoch-stream-separation/` - Earlier epoch research
