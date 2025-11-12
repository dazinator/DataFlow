# Integration Test Scenarios

**Purpose**: System-level integration tests for the layered prompt architecture

---

## Overview

This directory contains integration test scenarios that validate the complete system behavior. These tests verify:

1. **Duty Dispatch**: Orchestration correctly dispatches to each duty
2. **Required Context Loading**: Kernel and procedures are loaded correctly
3. **Duty Assignment**: Work items are assigned to correct duties
4. **End-to-End Flows**: Complete workflows from assignment to completion
5. **Handovers**: Transitions between duties work correctly

---

## Test Methodology

All integration tests use **tabletop simulation**:

1. Agent reads the scenario
2. Agent follows orchestration layer instructions
3. Agent follows dispatched duty procedure
4. Agent records observations and results
5. Scenario is marked PASS/FAIL with notes

**See**: [Testing Framework - Integration Testing](../../../../docs/design/prompt-engineering/testing-framework.md#node-type-4-orchestration)

---

## Test Scenarios

### Dispatch Tests

1. **[scenario-001-dispatch-triage.md](scenario-001-dispatch-triage.md)** - Dispatch to triage duty
2. **[scenario-002-dispatch-research.md](scenario-002-dispatch-research.md)** - Dispatch to research duty
3. **[scenario-003-dispatch-implementation.md](scenario-003-dispatch-implementation.md)** - Dispatch to implementation duty
4. **[scenario-004-dispatch-tech-debt.md](scenario-004-dispatch-tech-debt.md)** - Dispatch to tech debt duty
5. **[scenario-005-dispatch-product-prioritization.md](scenario-005-dispatch-product-prioritization.md)** - Dispatch to product prioritization duty
6. **[scenario-006-dispatch-process-modeling.md](scenario-006-dispatch-process-modeling.md)** - Dispatch to process modeling duty
7. **[scenario-007-dispatch-unassigned.md](scenario-007-dispatch-unassigned.md)** - Dispatch to unassigned duty (fallback)

### Duty Assignment Tests

8. **[scenario-008-duty-assignment-multiple-labels.md](scenario-008-duty-assignment-multiple-labels.md)** - Handle multiple duty labels
9. **[scenario-009-duty-assignment-missing-label.md](scenario-009-duty-assignment-missing-label.md)** - Handle missing duty label

### End-to-End Flow Tests

10. **[scenario-010-e2e-research-to-implementation.md](scenario-010-e2e-research-to-implementation.md)** - Research → Implementation flow
11. **[scenario-011-e2e-triage-to-research.md](scenario-011-e2e-triage-to-research.md)** - Triage → Research flow
12. **[scenario-012-required-context-loading.md](scenario-012-required-context-loading.md)** - Required Context loads correctly

---

## Running Tests

### Manual Tabletop Simulation

1. Read scenario file
2. Follow orchestration instructions from `.github/copilot-instructions.md`
3. Apply duty assignment procedure
4. Dispatch to appropriate duty
5. Record observations
6. Mark scenario PASS/FAIL

### Expected Pass Rate

**Target**: >90% pass rate on initial run

**If scenario fails**:
- Document why it failed
- Update orchestration or duty files if needed
- Re-run scenario to verify fix

---

## Persistence Strategy

**All integration tests are ALWAYS persisted** (high value for system validation):
- Catch regressions in orchestration layer
- Validate layer integration
- Reference for understanding complete system behavior
- Used for Phase 5 validation

---

## Success Criteria

- [ ] All 12 scenarios created
- [ ] >90% pass rate on tabletop simulation
- [ ] Zero kernel leaks detected
- [ ] Zero dependency leaks detected
- [ ] All handovers work correctly
- [ ] Required Context loading operational

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial integration test suite for Phase 4 |
