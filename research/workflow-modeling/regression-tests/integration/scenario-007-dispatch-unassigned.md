# Integration Test Scenario 007: Dispatch to Unassigned Duty

**Purpose**: Verify orchestration correctly dispatches to unassigned duty when duty cannot be determined  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Agent receives a work item with no duty label or unrecognized label.

---

## Starting State

**Work Item #1005**:
- **Title**: "Random feature idea"
- **Labels**: (none)
- **Description**: "Some vague idea about features"
- **Status**: Open

---

## Procedure to Follow

1. Load Orchestration
2. Apply Duty Assignment: [Duty Assignment Procedure](../../../../.team/procedures/duty-assignment.md)
3. Dispatch to [Unassigned Duty](../../../../.team/duties/UNASSIGNED_DUTY.md) (fallback)
4. Follow unassigned duty steps

---

## Expected Behavior

### Duty Assignment
```python
duty = get_work_item_duty(work_item_id=1005)
# Returns: None (no duty label)
```

### Fallback Logic
- ✅ Orchestration detects missing duty
- ✅ Defaults to UNASSIGNED_DUTY.md per procedure
- ✅ No errors or confusion

### Dispatch
- ✅ Unassigned duty loaded
- ✅ Clear guidance on inferring duty
- ✅ Can assign correct duty after assessment

### Execution
- ✅ Unassigned duty helps determine correct duty
- ✅ Can hand over to correct duty
- ✅ Uses semantic operations

---

## Success Criteria

- [ ] Duty assignment detects missing label (None)
- [ ] Fallback to unassigned duty successful
- [ ] Unassigned duty procedure clear
- [ ] Can infer and assign correct duty
- [ ] Handover to correct duty works
- [ ] No kernel leaks detected

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Notes**:
