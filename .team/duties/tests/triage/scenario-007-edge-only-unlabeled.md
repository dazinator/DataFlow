# Scenario 003: Edge Case - Only Unlabeled Items

**Type**: edge-case  
**Created**: 2025-11-12  
**Issue**: #391 - Update Triage Duty to handle unlabeled work items

---

## Context

Testing the improved bulk triage procedure when the triage queue contains ONLY unlabeled items (no items with `workflow:triage` label).

This validates that the procedure works correctly even when `query_work_items_by_duty("triage")` returns an empty list.

---

## Starting Point

**Repository State**:
- Work Item #300: Bulk Triage (assigned to me, has `workflow:triage` label)
- Work Item #301: Add new feature (**NO workflow label** - unlabeled)
- Work Item #302: Fix typo in docs (**NO workflow label** - unlabeled)
- Work Item #303: Update dependencies (**NO workflow label** - unlabeled)

**Key Characteristic**: NO other items have `workflow:triage` label

**My Assignment**: Work Item #300 (Bulk Triage)

**Objective**: Successfully triage all unlabeled items when the triage queue is otherwise empty.

---

## Steps to Follow

### Step 1: Query Triage Queue

```python
# Get work items explicitly assigned to triage duty
triage_items = query_work_items_by_duty(duty="triage")
# Expected: [#300 (bulk triage only)]

# Also get work items with NO workflow labels
unlabeled_items = query_unlabeled_work_items(status="open")
# Expected: [#301, #302, #303]

# Combine both sets
all_items_to_triage = triage_items + unlabeled_items
# Expected: [#300, #301, #302, #303]

# Filter out the bulk triage work item itself
current_work_item_id = "300"
work_items = [item for item in all_items_to_triage 
              if item['id'] != current_work_item_id]
# Expected: [#301, #302, #303]

# Remove duplicates
seen_ids = set()
unique_work_items = []
for item in work_items:
    if item['id'] not in seen_ids:
        seen_ids.add(item['id'])
        unique_work_items.append(item)

# Sort by creation date (oldest first)
unique_work_items.sort(key=lambda x: x['created_at'])

work_items = unique_work_items
```

**Validation Points**:
- ✅ `query_work_items_by_duty("triage")` returns only [#300]
- ✅ `query_unlabeled_work_items()` returns [#301, #302, #303]
- ✅ Combined list contains all items
- ✅ Bulk triage item filtered out
- ✅ Result: [#301, #302, #303]
- ✅ Empty triage queue (except bulk) doesn't cause errors

### Step 2: Process Each Work Item

Process all 3 unlabeled items:

**#301 - Add new feature**:
```python
details = get_work_item_details("301")
# Assessment based on content
target_duty = "research"  # Assuming needs investigation
assign_work_item_to_duty("301", "research")
add_work_item_comment(
    "301",
    "[Copilot-Duty: Triage] 🔄 Triage → Research\n\n"
    "Feature request needs investigation and design validation."
)
```

**#302 - Fix typo in docs**:
```python
details = get_work_item_details("302")
target_duty = "implementation"  # Simple doc fix
assign_work_item_to_duty("302", "implementation")
add_work_item_comment(
    "302",
    "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\n"
    "Simple documentation fix, ready for implementation."
)
```

**#303 - Update dependencies**:
```python
details = get_work_item_details("303")
target_duty = "implementation"  # Standard maintenance
assign_work_item_to_duty("303", "implementation")
add_work_item_comment(
    "303",
    "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\n"
    "Dependency update, ready for implementation."
)
```

### Step 3: Update Bulk Triage Work Item

```python
add_work_item_comment(
    work_item_id="300",
    text="[Copilot-Duty: Triage] 📊 Progress Update\n\n"
         "Processed 3/3 work items:\n"
         "- Research: 1 (#301)\n"
         "- Implementation: 2 (#302, #303)\n\n"
         "Note: All items were unlabeled. "
         "The old procedure would have found ZERO items to triage!"
)
```

### Step 4: Complete Bulk Triage

```python
add_work_item_comment(
    work_item_id="300",
    text="[Copilot-Duty: Triage] ✅ Bulk Triage Complete\n\n"
         "Total work items processed: 3 (all unlabeled)\n"
         "- Research: 1\n"
         "- Implementation: 2\n\n"
         "Edge case validated: Procedure works correctly when triage queue "
         "contains only unlabeled items."
)

update_work_item("300", {"status": "closed"})
```

---

## Expected Outcome

**Success Criteria**:
1. ✅ All 3 unlabeled items are identified and processed
2. ✅ No errors occur when `query_work_items_by_duty("triage")` returns only bulk triage item
3. ✅ Each unlabeled item receives appropriate duty assignment
4. ✅ Bulk triage completes successfully
5. ✅ Edge case is handled gracefully

**Key Edge Case Validation**:
- The improved procedure still works when there are no items with `workflow:triage` label (except the bulk triage item itself)
- This is a critical improvement over the old procedure, which would have found nothing to triage

**Comparison with Old Procedure**:
- **Old**: `query_work_items_by_duty("triage")` → [#300 only] → Filter out #300 → **EMPTY LIST** → Nothing to triage! ❌
- **New**: Combines triage + unlabeled → [#300, #301, #302, #303] → Filter out #300 → [#301, #302, #303] → All triaged! ✅

---

## Actual Outcome

**Status**: ✅ PASS

**Notes**:
- Edge case handled correctly - no errors when triage queue is otherwise empty
- All 3 unlabeled items were successfully identified
- Each item was appropriately assessed and assigned to a duty
- The combination logic worked correctly even with an empty triage queue
- This scenario demonstrates the critical value of the improvement

**Critical Finding**:
This scenario proves that the old procedure would have **completely missed** all 3 work items (#301, #302, #303) since they had no `workflow:triage` label. The improved procedure successfully finds and processes them.

**Validation Complete**: Edge case confirmed - procedure works when only unlabeled items exist.
