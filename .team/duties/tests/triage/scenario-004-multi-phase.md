# Scenario 004: Multi-Phase Triage - Sub-Work-Item Processing

**Duty**: Triage  
**Type**: End-to-End Scenario  
**Complexity**: High  
**Created**: 2025-11-12

---

## Purpose

Test triage duty's handling of multi-phase work items where a sub-work-item needs to be processed as part of a larger triage plan.

---

## Context

A work item is part of a multi-phase triage plan. The agent must check the parent, understand the overall plan, process this phase, and update the parent.

---

## Starting State

**Work Item #2001** (Parent - Multi-Phase Triage Plan):
- **Title**: "[Multi-Phase] Q4 2025 Backlog Triage"
- **Description**:
  ```
  Triage Q4 backlog in phases:
  
  - [ ] Phase 1: Performance-related work items (#2002) - OPEN
  - [ ] Phase 2: Feature requests (#2003) - NOT STARTED
  - [ ] Phase 3: Bug fixes (#2004) - NOT STARTED
  ```
- **Duty**: `triage`
- **Status**: `open`

**Work Item #2002** (Current - Sub-Work-Item):
- **Title**: "[Phase 1] Triage Performance Work Items"
- **Description**:
  ```
  Process all performance-related work items from Q4 backlog.
  
  Work items to triage:
  - #2010: "Optimize pipeline throughput"
  - #2011: "Reduce memory allocation in TransformBlock"
  - #2012: "Benchmark distributed vs local processing"
  
  Update parent (#2001) when complete.
  ```
- **Duty**: `triage`
- **Parent**: #2001
- **Status**: `open`

**Work Items Referenced**:
- #2010: Clear optimization task (performance)
- #2011: Refactoring for perf (tech debt)
- #2012: Needs benchmarking (research)

---

## Procedure to Follow

[Triage Duty](../../../.team/duties/TRIAGE_DUTY.md) with [Multi-Phase Work Items Procedure](../../../.team/procedures/multi-phase-work-items.md)

---

## Expected Steps

### Step 1: Check Multi-Phase Status

```python
# Check if work item is part of multi-phase plan
is_multi = is_multi_phase(work_item_id="2002")
# Result: True

# Get parent work item
parent_id = get_parent_work_item(work_item_id="2002")
# Result: "2001"

# Get parent details to understand overall plan
parent = get_work_item_details(work_item_id="2001")
```

**Observation**: This is Phase 1 of a 3-phase triage plan.

### Step 2: Process Work Item #2010

```python
# Assess: Performance optimization, clear task → implementation
assign_work_item_to_duty("2010", "implementation")
add_work_item_comment(
    "2010",
    "[Copilot-Duty: Triage] 🔄 Triage → Implementation\n\n"
    "Performance optimization with clear metrics. Ready for implementation."
)
```

### Step 3: Process Work Item #2011

```python
# Assess: Refactoring for performance → tech-debt
assign_work_item_to_duty("2011", "tech-debt")
add_work_item_comment(
    "2011",
    "[Copilot-Duty: Triage] 🔄 Triage → Tech Debt\n\n"
    "Performance-related refactoring. Tech debt duty will prioritize."
)
```

### Step 4: Process Work Item #2012

```python
# Assess: Needs benchmarking → research
assign_work_item_to_duty("2012", "research")
add_work_item_comment(
    "2012",
    "[Copilot-Duty: Triage] 🔄 Triage → Research\n\n"
    "Benchmarking needed to compare approaches before deciding."
)
```

### Step 5: Update Parent Work Item

```python
# Mark Phase 1 as complete in parent description
parent_description_updated = """
Triage Q4 backlog in phases:

- [x] Phase 1: Performance-related work items (#2002) - COMPLETE
  - #2010 → Implementation
  - #2011 → Tech Debt
  - #2012 → Research
- [ ] Phase 2: Feature requests (#2003) - OPEN
- [ ] Phase 3: Bug fixes (#2004) - NOT STARTED
"""

update_work_item(
    work_item_id="2001",
    fields={"description": parent_description_updated}
)

add_work_item_comment(
    "2001",
    "[Copilot-Duty: Triage] ✅ Phase 1 Complete\n\n"
    "Performance work items triaged:\n"
    "- #2010 → Implementation\n"
    "- #2011 → Tech Debt\n"
    "- #2012 → Research\n\n"
    "Ready for Phase 2."
)
```

### Step 6: Close Current Sub-Work-Item

```python
update_work_item(
    work_item_id="2002",
    fields={"status": "closed"}
)
```

---

## Expected Outcome

**Work Items #2010, #2011, #2012**:
- Each routed to appropriate duty with rationale

**Work Item #2002** (Current Phase):
- Status changed to `closed`

**Work Item #2001** (Parent):
- Description updated showing Phase 1 complete
- Comment added summarizing Phase 1 results
- Still `open` (Phases 2 and 3 remain)

---

## Success Criteria

- [ ] Multi-phase status checked using semantic operation
- [ ] Parent work item retrieved and understood
- [ ] All referenced work items processed correctly
- [ ] Parent description updated to reflect Phase 1 completion
- [ ] Parent comment added with summary
- [ ] Current phase work item closed
- [ ] Parent remains open for remaining phases
- [ ] No platform-specific code used

---

## Test Result

**Status**: [PENDING]

**Agent Observations**: [To be filled when executing tabletop test]

**Issues Found**: [Any unclear steps, missing info, etc.]

**Notes**: This scenario tests the duty's ability to handle complex multi-phase workflows and parent-child work item relationships.

---

## Semantic Operations Used

- `is_multi_phase(work_item_id)` - Check if work item is part of multi-phase plan
- `get_parent_work_item(work_item_id)` - Get parent work item ID
- `get_work_item_details(work_item_id)` - Retrieve parent and work item details
- `assign_work_item_to_duty(work_item_id, duty)` - Route work items
- `add_work_item_comment(work_item_id, text)` - Add comments
- `update_work_item(work_item_id, fields)` - Update parent description and close sub-work-item
