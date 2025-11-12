# Scenario 003: Bulk Triage Mode

**Duty**: Triage  
**Type**: End-to-End Scenario  
**Complexity**: High  
**Created**: 2025-11-12

---

## Purpose

Test triage duty's bulk mode functionality where multiple work items are triaged in a single session.

---

## Context

A "Bulk Triage" work item has been created to process all pending work items in the triage queue.

---

## Starting State

**Work Item #1000** (Bulk Triage):
- **Title**: "[Triage] Bulk triage - Process all pending work items"
- **Description**:
  ```
  Bulk Triage Instructions for @copilot
  
  Process all work items in the triage queue. For each:
  1. Assess and determine appropriate duty
  2. Hand over with clear rationale
  3. Update this work item with progress
  
  Close this work item when triage queue is empty.
  ```
- **Duty**: `triage`
- **Status**: `open`

**Triage Queue** (via `query_work_items_by_duty("triage")`):
- Work Item #1001: "Fix NullReferenceException in BatchBlock" (bug, reproducible)
- Work Item #1002: "Investigate memory usage in long-running pipelines" (unclear, needs research)
- Work Item #1003: "Refactor ActorPool to modern C#" (code quality)
- Work Item #1000: Bulk triage work item itself (should be excluded)

---

## Procedure to Follow

[Triage Duty - Bulk Triage Procedure](../../../.team/duties/TRIAGE_DUTY.md#bulk-triage-procedure)

---

## Expected Steps

### Step 1: Query Triage Queue

```python
# Get all triage work items
triage_items = query_work_items_by_duty(duty="triage")

# Filter out bulk triage work item itself
current_work_item_id = "1000"
work_items = [item for item in triage_items 
              if item['id'] != current_work_item_id]

# Sort by creation date
work_items.sort(key=lambda x: x['created_at'])
```

**Result**: 3 work items to process (#1001, #1002, #1003)

### Step 2: Process Work Item #1001

```python
# Assess: Clear bug, reproducible → implementation
assign_work_item_to_duty("1001", "implementation")
add_work_item_comment(
    "1001",
    "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\n"
    "Clear bug with reproduction steps. Ready for fix."
)
```

### Step 3: Process Work Item #1002

```python
# Assess: Unclear issue, needs investigation → research
assign_work_item_to_duty("1002", "research")
add_work_item_comment(
    "1002",
    "[Copilot-Duty: Triage] 🔄 Triage → Research\n\n"
    "Root cause unclear. Needs profiling and investigation."
)
```

### Step 4: Process Work Item #1003

```python
# Assess: Code quality improvement → tech-debt
assign_work_item_to_duty("1003", "tech-debt")
add_work_item_comment(
    "1003",
    "[Copilot-Duty: Triage] 🔄 Triage → Tech Debt\n\n"
    "Code modernization task. Tech debt duty will analyze and create backlog items."
)
```

### Step 5: Update Bulk Triage Progress

```python
add_work_item_comment(
    "1000",
    "[Copilot-Duty: Triage] 📊 Progress Update\n\n"
    "Processed 3/3 work items:\n"
    "- Research: 1\n"
    "- Implementation: 1\n"
    "- Tech Debt: 1"
)
```

### Step 6: Complete Bulk Triage

```python
add_work_item_comment(
    "1000",
    "[Copilot-Duty: Triage] ✅ Bulk Triage Complete\n\n"
    "Total work items processed: 3\n"
    "- Research: 1 (#1002)\n"
    "- Implementation: 1 (#1001)\n"
    "- Tech Debt: 1 (#1003)\n\n"
    "All work items have been triaged and routed to appropriate duties."
)

update_work_item("1000", {"status": "closed"})
```

---

## Expected Outcome

**Work Item #1001**:
- Duty changed to `implementation`
- Handover comment added

**Work Item #1002**:
- Duty changed to `research`
- Handover comment added

**Work Item #1003**:
- Duty changed to `tech-debt`
- Handover comment added

**Work Item #1000** (Bulk Triage):
- Progress comments added
- Final summary comment added
- Status changed to `closed`

---

## Success Criteria

- [ ] Triage queue queried using semantic operation
- [ ] Bulk triage work item excluded from processing
- [ ] All 3 work items processed correctly
- [ ] Each work item routed to correct duty
- [ ] Progress updates added to bulk triage work item
- [ ] Final summary accurate
- [ ] Bulk triage work item closed
- [ ] No platform-specific code used

---

## Test Result

**Status**: [PENDING]

**Agent Observations**: [To be filled when executing tabletop test]

**Issues Found**: [Any unclear steps, missing info, etc.]

**Notes**: This scenario tests the duty's ability to handle multiple work items and maintain progress tracking.

---

## Semantic Operations Used

- `query_work_items_by_duty(duty)` - Find work items in triage queue
- `get_work_item_details(work_item_id)` - Retrieve details for assessment
- `assign_work_item_to_duty(work_item_id, duty)` - Change duty for each work item
- `add_work_item_comment(work_item_id, text)` - Add handover comments and progress updates
- `update_work_item(work_item_id, fields)` - Close bulk triage work item
