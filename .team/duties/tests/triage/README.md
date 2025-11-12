# Triage Duty Test Scenarios

**Duty**: Triage  
**Test Methodology**: End-to-End Tabletop Simulation  
**Created**: 2025-11-12

---

## Purpose

Test scenarios for the Triage Duty to validate:
- Correct duty assignment based on work item characteristics
- Proper use of semantic operations (zero kernel leaks)
- Handover procedures to other duties
- Multi-phase work item handling
- Bulk triage mode functionality

---

## Test Scenarios

### Scenario 001: Clear Feature Request
**File**: `scenario-001-clear-feature-request.md`  
**Complexity**: Low  
**Focus**: Basic triage flow for straightforward feature request → implementation

**Key Operations**:
- `get_work_item_details()`
- `assign_work_item_to_duty()`
- `add_work_item_comment()`

---

### Scenario 002: Feature with Technical Unknowns
**File**: `scenario-002-feature-with-unknowns.md`  
**Complexity**: Medium  
**Focus**: Identifying technical unknowns and routing to research duty

**Key Operations**:
- `get_work_item_details()`
- `assign_work_item_to_duty()`
- `add_work_item_comment()` (with research questions)

---

### Scenario 003: Bulk Triage Mode
**File**: `scenario-003-bulk-triage.md`  
**Complexity**: High  
**Focus**: Processing multiple work items in bulk mode with progress tracking

**Key Operations**:
- `query_work_items_by_duty()`
- `get_work_item_details()` (multiple)
- `assign_work_item_to_duty()` (multiple)
- `add_work_item_comment()` (progress updates)
- `update_work_item()` (close bulk triage)

---

### Scenario 004: Multi-Phase Triage
**File**: `scenario-004-multi-phase.md`  
**Complexity**: High  
**Focus**: Handling sub-work-items in a multi-phase triage plan

**Key Operations**:
- `is_multi_phase()`
- `get_parent_work_item()`
- `get_work_item_details()` (parent and children)
- `update_work_item()` (parent description + status)
- `assign_work_item_to_duty()` (multiple)

---

### Scenario 005: Bulk Triage with Unlabeled Items
**File**: `scenario-005-bulk-triage-unlabeled-items.md`  
**Complexity**: Medium  
**Focus**: Validating that unlabeled work items are included in bulk triage results

**Key Operations**:
- `query_work_items_by_duty()` (triage labeled)
- `query_unlabeled_work_items()` (unlabeled items)
- Deduplication and filtering logic

**Added**: 2025-11-12 (Issue #391)

---

### Scenario 006: Mixed Labeled and Unlabeled Items
**File**: `scenario-006-mixed-labeled-unlabeled.md`  
**Complexity**: Medium  
**Focus**: Realistic scenario with mix of labeled and unlabeled work items

**Key Operations**:
- `query_work_items_by_duty()` (triage labeled)
- `query_unlabeled_work_items()` (unlabeled items)
- Filtering out items in other duties

**Added**: 2025-11-12 (Issue #391)

---

### Scenario 007: Edge Case - Only Unlabeled Items
**File**: `scenario-007-edge-only-unlabeled.md`  
**Complexity**: Low  
**Focus**: Edge case where triage queue is empty but unlabeled items exist

**Key Operations**:
- `query_work_items_by_duty()` (returns empty)
- `query_unlabeled_work_items()` (returns items)

**Critical Finding**: Old procedure would return empty list, missing all items  
**Added**: 2025-11-12 (Issue #391)

---

## Running Tests

### Tabletop Simulation Process

1. **Read Scenario**: Understand starting state and context
2. **Follow Procedure**: Step through the Triage Duty procedure
3. **Execute Steps**: Mentally execute each semantic operation
4. **Verify Outcome**: Compare actual result to expected outcome
5. **Document Issues**: Note any unclear instructions or missing steps
6. **Update Scenario**: Mark as PASS/FAIL and add observations

### Success Metrics

**Pass Criteria**:
- ✅ All semantic operations identified correctly
- ✅ Correct duty determined based on decision tree
- ✅ Handover comments include clear rationale
- ✅ No platform-specific code used (zero kernel leaks)
- ✅ Procedure steps are clear and complete

**Fail Criteria**:
- ❌ Wrong duty selected (mismatch with decision tree)
- ❌ Kernel leaks detected (platform-specific operations)
- ❌ Missing or unclear procedure steps
- ❌ Handover comments lack context

---

## Test Coverage

| Aspect | Covered By Scenarios |
|--------|---------------------|
| **Decision Tree - Implementation** | 001 |
| **Decision Tree - Research** | 002 |
| **Decision Tree - Tech Debt** | 003 |
| **Bulk Mode** | 003 |
| **Multi-Phase** | 004 |
| **Semantic Operations** | All |
| **Handover Procedure** | All |

---

## Regression Testing

**High-Value Scenarios** (persist for regression):
- Scenario 003: Bulk Triage Mode (complex, high regression risk)
- Scenario 004: Multi-Phase (complex, frequently used pattern)

**One-Time Scenarios** (can be reverted after validation):
- Scenario 001: Clear Feature Request (simple validation)
- Scenario 002: Feature with Unknowns (simple validation)

---

## Related Documentation

- [Triage Duty](/.team/duties/TRIAGE_DUTY.md)
- [Testing Framework](/docs/design/prompt-engineering/testing-framework.md)
- [Handover Procedure](/.team/procedures/handover.md)
- [Multi-Phase Work Items](/.team/procedures/multi-phase-work-items.md)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenarios created for Triage Duty |
