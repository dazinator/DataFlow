# Process Modeling Archived Plan - Issue #391

**Issue**: #391 - Update Triage Duty to handle unlabeled work items  
**Started**: 2025-11-12  
**Completed**: 2025-11-12  
**Status**: ✅ **COMPLETE**

---

## Objective

Update the Triage Duty bulk triage procedure to handle work items without workflow labels (unlabeled items needing initial triage).

---

## Problem Statement

The current bulk triage procedure only queries items with `workflow:triage` label using `query_work_items_by_duty("triage")`. This misses newly created issues without any workflow labels.

**Evidence**:
- Issue #377 was created without workflow labels
- Bulk triage in issue #388 initially missed it
- Only discovered when user pointed out the miss

**Root Cause**: The bulk triage procedure relied solely on `query_work_items_by_duty("triage")` which only returns items with the `workflow:triage` label.

---

## Solution Implemented

### 1. New Semantic Operation Added

Added `query_unlabeled_work_items` as the 13th semantic operation to the kernel layer.

**Signature**:
```python
query_unlabeled_work_items(
    status: str = "open",
    limit: int = 100
) -> list[dict]
```

**Purpose**: Find all work items WITHOUT any workflow labels (need initial triage)

**Files Updated**:
- `docs/design/prompt-engineering/semantic-language.md` - Semantic operation specification
- `.team/kernel/README.md` - Updated count (12 → 13 operations)
- `.team/kernel/github/operations.md` - GitHub driver implementation
- `.team/kernel/github/examples.md` - Usage example

### 2. Triage Duty Updated

Updated the Bulk Triage Procedure in `.team/duties/TRIAGE_DUTY.md`:

**Old Procedure (Step 1)**:
```python
# Get all work items in triage duty
triage_items = query_work_items_by_duty(duty="triage")

# Filter out bulk triage item itself
work_items = [item for item in triage_items 
              if item['id'] != current_work_item_id]

# Sort by creation date
work_items.sort(key=lambda x: x['created_at'])
```

**New Procedure (Step 1)**:
```python
# Get work items explicitly assigned to triage duty
triage_items = query_work_items_by_duty(duty="triage")

# Also get work items with NO workflow labels
unlabeled_items = query_unlabeled_work_items(status="open")

# Combine both sets
all_items_to_triage = triage_items + unlabeled_items

# Filter out bulk triage item itself
work_items = [item for item in all_items_to_triage 
              if item['id'] != current_work_item_id]

# Remove duplicates
seen_ids = set()
unique_work_items = []
for item in work_items:
    if item['id'] not in seen_ids:
        seen_ids.add(item['id'])
        unique_work_items.append(item)

# Sort by creation date
unique_work_items.sort(key=lambda x: x['created_at'])

work_items = unique_work_items
```

**Key Improvements**:
- Queries both labeled (`workflow:triage`) and unlabeled items
- Combines results ensuring complete coverage
- Deduplicates (though duplicates shouldn't happen)
- Documented why this matters

---

## Test Results

Created 3 comprehensive test scenarios in `.team/duties/tests/triage/`:

### Scenario 001: Bulk Triage with Unlabeled Items (Improved)
**Type**: improved  
**Status**: ✅ PASS

**What it tested**:
- Basic functionality of improved procedure
- Querying both labeled and unlabeled items
- Combining and deduplicating results
- Proper filtering of bulk triage item

**Key validation**: Unlabeled items (#377, #380) successfully included in triage.

---

### Scenario 002: Mixed Labeled and Unlabeled Items
**Type**: improved  
**Status**: ✅ PASS

**What it tested**:
- Realistic mix of work item states
- Correct filtering of items in other duties
- Both triage-labeled and unlabeled items processed
- Items in other duties remain untouched

**Key validation**: Only triage-eligible items processed; items already in other duties ignored.

---

### Scenario 003: Edge Case - Only Unlabeled Items
**Type**: edge-case  
**Status**: ✅ PASS

**What it tested**:
- Edge case where triage queue has NO items except bulk triage item
- All items needing triage are unlabeled
- Empty result from `query_work_items_by_duty("triage")` handled correctly

**Critical finding**: This scenario proves the old procedure would have missed ALL work items in this case!

**Comparison**:
- **Old**: `query_work_items_by_duty("triage")` → [#300 only] → Filter out #300 → **EMPTY** → Nothing triaged! ❌
- **New**: Combines triage + unlabeled → [#300, #301, #302, #303] → Filter out #300 → [#301, #302, #303] → All triaged! ✅

---

## Test Execution

**Methodology**: Tabletop simulation (mental walkthrough following procedure)

**Pass Rate**: 3/3 (100%)

All scenarios validated the improved procedure works correctly and handles edge cases.

---

## Quality Assurance

### Leak Detection

- ✅ **Kernel Leak Detection**: PASSED (zero leaks)
  - No platform-specific operations outside kernel layer
  - All semantic operations properly abstracted

- ✅ **Dependency Leak Detection**: PASSED (zero leaks)
  - No content duplication across dependency graph
  - All references properly maintained

### Graph Maintenance

Updated `.team/model-graph.yaml`:
- Version: 1.4 → 1.5
- Added post-phase 5 update note
- Documented new semantic operation addition

---

## Deliverables

1. ✅ Semantic language specification updated
2. ✅ Kernel README updated (12 → 13 operations)
3. ✅ GitHub driver operations.md updated with new operation
4. ✅ GitHub driver examples.md updated with usage example
5. ✅ Triage Duty TRIAGE_DUTY.md updated:
   - Semantic operations list
   - Quick Start bulk mode
   - Bulk Triage Procedure Step 1
6. ✅ Test scenarios created (3 scenarios with README)
7. ✅ Graph updated (version 1.5)
8. ✅ History updated (research/workflow-modeling/history.md)

---

## Expected Benefits

1. **Complete Coverage**: All work items needing triage found, including newly created unlabeled issues
2. **Reduced Manual Intervention**: No need for humans to notice and point out missed items
3. **Consistency**: Bulk triage is truly comprehensive
4. **Better Automation**: System automatically handles all work items regardless of how they're created

---

## Success Criteria Met

- [x] New semantic operation added to kernel layer
- [x] GitHub driver implementation complete with two variants
- [x] Triage Duty procedure updated to use both query operations
- [x] All test scenarios pass (100% pass rate)
- [x] Zero kernel leaks detected
- [x] Zero dependency leaks detected
- [x] Graph updated with version history
- [x] History.md updated with entry
- [x] Documentation clear and comprehensive

---

## Completion Notes

Issue #391 successfully completed on 2025-11-12. The Triage Duty now finds all work items needing triage, including unlabeled items, ensuring complete coverage and eliminating the gap that caused issue #377 to be initially missed.

**Archived**: 2025-11-12
