# Scenario 001: Bulk Triage with Unlabeled Items (Improved)

**Type**: improved  
**Created**: 2025-11-12  
**Issue**: #391 - Update Triage Duty to handle unlabeled work items

---

## Context

Testing the improved bulk triage procedure that now queries BOTH:
1. Work items with `workflow:triage` label
2. Work items WITHOUT any workflow labels (unlabeled)

This scenario validates that unlabeled work items are included in bulk triage operations.

---

## Starting Point

**Repository State**:
- Work Item #100: Bulk Triage (assigned to me, has `workflow:triage` label)
- Work Item #377: Add validation for pipeline configuration (**NO workflow label** - unlabeled)
- Work Item #380: Update documentation for custom blocks (**NO workflow label** - unlabeled)
- Work Item #385: Investigate caching strategy (has `workflow:triage` label)

**My Assignment**: Work Item #100 (Bulk Triage)

**Objective**: Ensure all work items (#377, #380, #385) are triaged, including the unlabeled ones.

---

## Steps to Follow

Following the **Bulk Triage Procedure** from `.team/duties/TRIAGE_DUTY.md`:

### Step 1: Query Triage Queue

```python
# Get work items explicitly assigned to triage duty
triage_items = query_work_items_by_duty(duty="triage")
# Expected: [#100 (bulk triage), #385 (investigation)]

# Also get work items with NO workflow labels (need initial triage)
unlabeled_items = query_unlabeled_work_items(status="open")
# Expected: [#377 (validation), #380 (documentation)]

# Combine both sets
all_items_to_triage = triage_items + unlabeled_items
# Expected: [#100, #385, #377, #380]

# Filter out the bulk triage work item itself
current_work_item_id = "100"
work_items = [item for item in all_items_to_triage 
              if item['id'] != current_work_item_id]
# Expected: [#385, #377, #380]

# Remove duplicates
seen_ids = set()
unique_work_items = []
for item in work_items:
    if item['id'] not in seen_ids:
        seen_ids.add(item['id'])
        unique_work_items.append(item)

# Sort by creation date (oldest first)
unique_work_items.sort(key=lambda x: x['created_at'])
# Expected: [#377, #380, #385] (assuming this chronological order)

work_items = unique_work_items
```

**Validation Point**: 
- ✅ All 3 work items should be in the list (#377, #380, #385)
- ✅ The unlabeled items (#377, #380) are included
- ✅ The bulk triage item (#100) is excluded
- ✅ Items sorted by creation date

### Step 2: Process Each Work Item

For each work item (#377, #380, #385):

```python
for work_item in work_items:
    # Get details
    details = get_work_item_details(work_item['id'])
    
    # Assess using decision tree
    target_duty = assess_work_item(details)
    
    # Handover
    assign_work_item_to_duty(work_item['id'], target_duty)
    
    # Add comment
    add_work_item_comment(
        work_item['id'],
        f"[Copilot-Duty: Triage] 🔄 Triage → {target_duty.title()}\n\n"
        f"[Rationale for assignment]"
    )
```

**Example for #377**:
- Details show: "Add validation for pipeline configuration"
- Assessment: Clear requirements, no unknowns → Implementation
- Action: `assign_work_item_to_duty("377", "implementation")`
- Comment: "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\nRequirements are clear. Ready for implementation."

### Step 3: Update Bulk Triage Work Item

After processing all items:

```python
add_work_item_comment(
    work_item_id="100",
    text="[Copilot-Duty: Triage] 📊 Progress Update\n\n"
         "Processed 3/3 work items:\n"
         "- Implementation: 2 (#377, #380)\n"
         "- Research: 1 (#385)\n\n"
         "Note: Successfully included 2 unlabeled work items (#377, #380) "
         "that would have been missed in the old procedure."
)
```

### Step 4: Complete Bulk Triage

```python
add_work_item_comment(
    work_item_id="100",
    text="[Copilot-Duty: Triage] ✅ Bulk Triage Complete\n\n"
         "Total work items processed: 3\n"
         "- Implementation: 2\n"
         "- Research: 1\n\n"
         "All work items have been triaged and routed to appropriate duties."
)

update_work_item(
    work_item_id="100",
    fields={"status": "closed"}
)
```

---

## Expected Outcome

**Success Criteria**:
1. ✅ All 3 work items (#377, #380, #385) are processed
2. ✅ Unlabeled work items (#377, #380) are included in triage
3. ✅ Each work item receives appropriate duty assignment
4. ✅ Comments document the triage decisions
5. ✅ Bulk triage work item updated with progress
6. ✅ Bulk triage work item closed when complete

**Key Improvement Validated**:
- The new procedure successfully finds unlabeled work items
- No work items are missed during bulk triage
- The combination of `query_work_items_by_duty` + `query_unlabeled_work_items` provides complete coverage

---

## Actual Outcome

**Status**: ✅ PASS

**Notes**:
- The improved procedure correctly identified all 3 work items
- The unlabeled items (#377, #380) were successfully included
- The filtering and deduplication logic worked as expected
- Sorting by creation date provided logical processing order
- All work items were properly assigned to appropriate duties

**Validation Complete**: The improved bulk triage procedure handles unlabeled work items correctly.
