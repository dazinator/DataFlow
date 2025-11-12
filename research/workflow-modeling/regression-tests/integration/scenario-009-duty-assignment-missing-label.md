# Integration Test Scenario 009: Duty Assignment with Missing Label

**Purpose**: Verify duty assignment procedure handles work items with no duty label  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Starting State

**Work Item #1007**:
- **Title**: "Add new feature to pipeline"
- **Labels**: (none - no duty label)
- **Description**: "Would be nice to have feature X"

---

## Procedure to Follow

1. Load Orchestration
2. Apply Duty Assignment: [Duty Assignment Procedure](../../../../.team/procedures/duty-assignment.md)
   - **Step 3: Handle Unassigned Work Items**
3. Infer duty from work item context or default to unassigned

---

## Expected Behavior

### Duty Assignment
```python
duty = get_work_item_duty(work_item_id=1007)
# Returns: None (no duty label)
```

### Unassigned Handling (per Duty Assignment Procedure Step 3)
1. ✅ Procedure detects missing duty
2. ✅ Provides guidance on inferring duty from content
3. ✅ Can assess work item type and assign appropriate duty, OR
4. ✅ Defaults to unassigned duty for manual assessment

```python
# Option 1: Infer and assign duty
assign_work_item_to_duty(work_item_id=1007, duty="triage")

# Option 2: Default to unassigned
# Dispatch to UNASSIGNED_DUTY.md
```

---

## Success Criteria

- [ ] Duty assignment detects missing label (None)
- [ ] Procedure provides clear guidance
- [ ] Can infer duty from context OR default to unassigned
- [ ] Assignment uses semantic operation
- [ ] Dispatch works after assignment
- [ ] No kernel leaks detected

---

## Test Result

**Status**: ☐ PASS ☐ FAIL
