# Integration Test Scenario 008: Duty Assignment with Multiple Labels

**Purpose**: Verify duty assignment procedure handles multiple conflicting duty labels correctly  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Agent receives a work item with multiple duty labels (label pollution) and must resolve the conflict.

---

## Starting State

**Work Item #1006**:
- **Title**: "Fix caching implementation"
- **Labels**: `workflow:research`, `workflow:implementation` (conflicting!)
- **Description**: "Research shows we need to fix implementation"
- **Status**: Open

---

## Procedure to Follow

1. Load Orchestration
2. Apply Duty Assignment: [Duty Assignment Procedure](../../../../.team/procedures/duty-assignment.md)
   - **Step 2: Handle Multiple Duty Designations**
3. Clean up labels using semantic operation
4. Dispatch to correct duty

---

## Expected Behavior

### Duty Assignment
```python
duty = get_work_item_duty(work_item_id=1006)
# Returns: Error indicating multiple duties
```

### Conflict Resolution (per Duty Assignment Procedure Step 2)
1. ✅ Procedure detects multiple duty labels
2. ✅ Provides clear guidance on resolution
3. ✅ Examines work item context to determine correct duty
4. ✅ Removes conflicting labels using semantic operation
5. ✅ Adds cleanup comment

```python
# Expected cleanup
assign_work_item_to_duty(work_item_id=1006, duty="implementation")

add_work_item_comment(
    work_item_id=1006,
    text="[Copilot-Duty: implementation] 🏷️ Label cleanup: Removed conflicting duty labels..."
)
```

### Dispatch
- ✅ After cleanup, dispatches to correct duty
- ✅ No confusion or errors

---

## Success Criteria

- [ ] Duty assignment detects multiple labels
- [ ] Procedure provides clear conflict resolution steps
- [ ] Correct duty determined from context
- [ ] Conflicting labels removed using semantic operation
- [ ] Cleanup comment added
- [ ] Dispatch to correct duty successful
- [ ] No kernel leaks detected

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Notes**:
