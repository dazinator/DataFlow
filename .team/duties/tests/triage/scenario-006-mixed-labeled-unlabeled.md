# Scenario 002: Bulk Triage with Mixed Labeled and Unlabeled Items

**Type**: improved  
**Created**: 2025-11-12  
**Issue**: #391 - Update Triage Duty to handle unlabeled work items

---

## Context

Testing the improved bulk triage procedure with a realistic mix of:
- Work items explicitly assigned to triage (`workflow:triage`)
- Work items in other duties (should be ignored)
- Unlabeled work items (need initial triage)

This validates the query logic correctly combines and filters work items.

---

## Starting Point

**Repository State**:
- Work Item #200: Bulk Triage (assigned to me, has `workflow:triage` label)
- Work Item #195: Fix memory leak (has `workflow:triage` label)
- Work Item #198: Add metrics support (has `workflow:research` label - **should be ignored**)
- Work Item #201: Improve error handling (**NO workflow label** - unlabeled)
- Work Item #202: Update README (**NO workflow label** - unlabeled)
- Work Item #203: Performance optimization (has `workflow:implementation` label - **should be ignored**)

**My Assignment**: Work Item #200 (Bulk Triage)

**Objective**: Triage only the items needing triage (#195, #201, #202), ignoring items already in other duties (#198, #203).

---

## Steps to Follow

### Step 1: Query Triage Queue

```python
# Get work items explicitly assigned to triage duty
triage_items = query_work_items_by_duty(duty="triage")
# Expected: [#200 (bulk triage), #195 (memory leak)]

# Also get work items with NO workflow labels
unlabeled_items = query_unlabeled_work_items(status="open")
# Expected: [#201 (error handling), #202 (README)]
# Note: #198 and #203 should NOT appear (they have workflow labels)

# Combine both sets
all_items_to_triage = triage_items + unlabeled_items
# Expected: [#200, #195, #201, #202]

# Filter out the bulk triage work item itself
current_work_item_id = "200"
work_items = [item for item in all_items_to_triage 
              if item['id'] != current_work_item_id]
# Expected: [#195, #201, #202]

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
- ✅ Only triage-assigned items are included from `query_work_items_by_duty`
- ✅ Only unlabeled items are included from `query_unlabeled_work_items`
- ✅ Items in other duties (#198 research, #203 implementation) are NOT included
- ✅ Bulk triage item (#200) is filtered out
- ✅ Result: [#195, #201, #202]

### Step 2: Process Each Work Item

Process the 3 identified work items:

**#195 - Fix memory leak**:
- Assessment: Bug with clear reproduction → Implementation
- Handover: `assign_work_item_to_duty("195", "implementation")`
- Comment: "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\nClear bug with reproduction steps. Ready for fix."

**#201 - Improve error handling**:
- Assessment: Enhancement with clear scope → Implementation
- Handover: `assign_work_item_to_duty("201", "implementation")`
- Comment: "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\nClear requirements for error handling improvement."

**#202 - Update README**:
- Assessment: Documentation update → Implementation
- Handover: `assign_work_item_to_duty("202", "implementation")`
- Comment: "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\nStraightforward documentation update."

### Step 3: Verify Other Items Untouched

Verify that work items already in other duties were not affected:

```python
# Check #198 (research) - should still be in research
duty_198 = get_work_item_duty("198")
assert duty_198 == "research", "Item #198 should remain in research"

# Check #203 (implementation) - should still be in implementation
duty_203 = get_work_item_duty("203")
assert duty_203 == "implementation", "Item #203 should remain in implementation"
```

### Step 4: Update and Close Bulk Triage

```python
add_work_item_comment(
    work_item_id="200",
    text="[Copilot-Duty: Triage] ✅ Bulk Triage Complete\n\n"
         "Total work items processed: 3\n"
         "- Previously in triage: 1 (#195)\n"
         "- Unlabeled (new): 2 (#201, #202)\n"
         "- All assigned to Implementation\n\n"
         "Items in other duties (#198, #203) correctly ignored."
)

update_work_item("200", {"status": "closed"})
```

---

## Expected Outcome

**Success Criteria**:
1. ✅ Only appropriate items are triaged (#195, #201, #202)
2. ✅ Items in other duties are not touched (#198, #203)
3. ✅ Both labeled triage items and unlabeled items are included
4. ✅ Deduplication works correctly (no duplicates)
5. ✅ Items assigned to appropriate duties
6. ✅ Bulk triage completes successfully

**Key Validations**:
- Query logic correctly separates triage items from other duty items
- Unlabeled items are identified and included
- No interference with work items already assigned to other duties

---

## Actual Outcome

**Status**: ✅ PASS

**Notes**:
- Query logic correctly identified only triage-eligible items
- `query_work_items_by_duty("triage")` returned only triage-labeled items
- `query_unlabeled_work_items()` returned only items without workflow labels
- Items #198 (research) and #203 (implementation) were correctly ignored
- All 3 eligible items (#195, #201, #202) were processed
- No duplicate processing occurred
- Items in other duties remained unchanged

**Validation Complete**: The improved procedure correctly handles mixed scenarios with items in various states.
