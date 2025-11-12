# Unassigned Duty Test Scenarios

**Duty**: Unassigned  
**Purpose**: Validate unassigned duty procedures through tabletop simulation

---

## Test Methodology

**See**: [Testing Framework](../../../../../docs/design/prompt-engineering/testing-framework.md#node-type-3-duty-procedures)

**Test Type**: End-to-End Tabletop Simulation

**Focus**: Verify that the unassigned duty:
1. Correctly uses semantic operations (not platform-specific code)
2. References procedures instead of duplicating content
3. Infers duty correctly when possible
4. Requests clarification when duty is ambiguous
5. Creates proper handovers to appropriate duties

---

## Scenario List

### Core Scenarios

1. **[scenario-001-clear-inference.md](scenario-001-clear-inference.md)**
   - Work item with clear duty indicators
   - Tests: Duty inference, automatic handover

2. **[scenario-002-ambiguous-request.md](scenario-002-ambiguous-request.md)**
   - Work item with unclear or vague description
   - Tests: Clarification request, human response handling

3. **[scenario-003-cross-cutting-concern.md](scenario-003-cross-cutting-concern.md)**
   - Work item spanning multiple duty areas
   - Tests: Primary duty selection, multi-phase workflow

---

## Test Execution

### Running Tests

1. **Read the duty document**: `.team/duties/UNASSIGNED_DUTY.md`
2. **For each scenario**:
   - Review starting state
   - Follow the duty procedure step-by-step
   - Compare actual steps with expected steps
   - Verify semantic operations used (not platform-specific)
   - Check procedure references (not duplicated content)
   - Validate expected outcome

### Pass Criteria

**Scenario PASSES** if:
- ✅ All steps follow duty procedure correctly
- ✅ Only semantic operations used (zero platform-specific code)
- ✅ Procedures referenced (not duplicated)
- ✅ Duty inference logic works correctly
- ✅ Clarification requests are clear and actionable
- ✅ Handovers executed properly
- ✅ Expected outcome achieved

**Scenario FAILS** if:
- ❌ Steps deviate from duty procedure
- ❌ Platform-specific code used (kernel leak)
- ❌ Content duplicated from procedures (dependency leak)
- ❌ Duty inference fails or is incorrect
- ❌ Clarification requests unclear
- ❌ Expected outcome not achieved

---

## Leak Detection

**After creating/updating duty**, run leak detection:

```bash
# Check for kernel leaks (platform-specific code)
.team/scripts/check-kernel-leaks.sh .team/duties/UNASSIGNED_DUTY.md

# Check for dependency leaks (duplicated content)
.team/scripts/check-dependency-leaks.sh .team/duties/UNASSIGNED_DUTY.md
```

**Expected Results**: Zero leaks detected

---

## Scenario Persistence

**Default**: Scenarios are temporary and reverted after validation

**Persistence Criteria**: Archive scenario if:
- High complexity (tests multiple interacting features)
- Regression risk (scenario caught real bug)
- Edge case documentation (unusual but important case)
- Training value (good example for learning)

**Archive Location**: `/research/workflow-modeling/regression-tests/unassigned/`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenarios for Unassigned Duty (Phase 3.2) |
