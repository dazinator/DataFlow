# Research Duty Test Scenarios

**Purpose**: Validate Research Duty procedures through end-to-end scenario testing

**Methodology**: Tabletop simulation - agents execute scenarios against duty documentation

---

## Test Scenarios

### Core Workflows

1. **[Scenario 001](scenario-001-performance-validation.md)** - Performance validation research with benchmarks
2. **[Scenario 002](scenario-002-multiple-approach-comparison.md)** - Comparing multiple technical approaches
3. **[Scenario 003](scenario-003-feasibility-research.md)** - Feasibility investigation
4. **[Scenario 004](scenario-004-multi-phase-research.md)** - Multi-phase research plan

### Scenario Summary

| Scenario | Complexity | Outcome Type | Focus Area |
|----------|------------|--------------|------------|
| 001 | Medium | Implementation Handover | Performance validation, benchmarks |
| 002 | High | Implementation Handover | Multiple approach comparison |
| 003 | Low | Research closure | Feasibility validation (negative outcome) |
| 004 | High | Implementation Handover | Multi-phase coordination |

---

## Testing Methodology

See [Testing Framework](../../../../docs/design/prompt-engineering/testing-framework.md#node-type-3-duty-procedures) for complete methodology.

**Test Type**: End-to-end tabletop simulation

**Execution**:
1. Agent reads scenario starting state
2. Agent follows Research Duty procedure
3. Agent executes expected steps using semantic operations
4. Compare outcome with expected outcome

**Success Criteria**:
- All semantic operations used correctly
- Zero platform-specific code (kernel leaks)
- Handover procedures followed
- Expected outcome achieved

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenarios for Research Duty |
