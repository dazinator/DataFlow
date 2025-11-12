# Product Prioritization Duty Test Scenarios

**Duty**: Product Prioritization  
**Purpose**: Validate product prioritization duty procedures through tabletop simulation

---

## Test Methodology

**See**: [Testing Framework](../../../../../docs/design/prompt-engineering/testing-framework.md#node-type-3-duty-procedures)

**Test Type**: End-to-End Tabletop Simulation

**Focus**: Verify that the product prioritization duty:
1. Correctly uses semantic operations (not platform-specific code)
2. References procedures instead of duplicating content
3. Applies prioritization policy criteria correctly
4. Handles edge cases appropriately
5. Creates proper handovers to implementation duty

---

## Scenario List

### Core Scenarios

1. **[scenario-001-standard-prioritization.md](scenario-001-standard-prioritization.md)**
   - Simple backlog prioritization with mixed priorities
   - Tests: Basic prioritization flow, policy application, selection

2. **[scenario-002-security-first-policy.md](scenario-002-security-first-policy.md)**
   - Security vulnerability prioritization (core vs non-core)
   - Tests: Security risk assessment, CVE handling, priority override

3. **[scenario-003-queue-capacity-full.md](scenario-003-queue-capacity-full.md)**
   - Implementation queue at capacity
   - Tests: Capacity checking, graceful handling, no selection

4. **[scenario-004-priority-overrides.md](scenario-004-priority-overrides.md)**
   - Manual priority overrides from stakeholders
   - Tests: Override processing, swapping logic, policy compliance

5. **[scenario-005-tech-debt-inclusion.md](scenario-005-tech-debt-inclusion.md)**
   - Tech debt policy requirement (at least 1 item)
   - Tests: Tech debt identification, quick wins, policy enforcement

---

## Test Execution

### Running Tests

1. **Read the duty document**: `.team/duties/PRODUCT_PRIORITIZATION_DUTY.md`
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
- ✅ Expected outcome achieved
- ✅ Edge cases handled appropriately

**Scenario FAILS** if:
- ❌ Steps deviate from duty procedure
- ❌ Platform-specific code used (kernel leak)
- ❌ Content duplicated from procedures (dependency leak)
- ❌ Expected outcome not achieved
- ❌ Edge cases not handled

---

## Leak Detection

**After creating/updating duty**, run leak detection:

```bash
# Check for kernel leaks (platform-specific code)
.team/scripts/check-kernel-leaks.sh .team/duties/PRODUCT_PRIORITIZATION_DUTY.md

# Check for dependency leaks (duplicated content)
.team/scripts/check-dependency-leaks.sh .team/duties/PRODUCT_PRIORITIZATION_DUTY.md
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

**Archive Location**: `/research/workflow-modeling/regression-tests/product-prioritization/`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenarios for Product Prioritization Duty (Phase 3.2) |
