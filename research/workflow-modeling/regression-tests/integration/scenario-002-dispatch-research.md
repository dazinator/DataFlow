# Integration Test Scenario 002: Dispatch to Research Duty

**Purpose**: Verify orchestration correctly dispatches to research duty  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Agent receives a work item with `workflow:research` label and must dispatch to the research duty.

---

## Starting State

**Work Item #1000**:
- **Title**: "Research Redis caching approach for DataFlow"
- **Labels**: `workflow:research`
- **Description**: "Investigate Redis integration options, evaluate performance"
- **Status**: Open

---

## Procedure to Follow

1. Load Orchestration: [copilot-instructions.md](../../../../.github/copilot-instructions.md)
2. Apply Duty Assignment: [Duty Assignment Procedure](../../../../.team/procedures/duty-assignment.md)
3. Dispatch to Duty: [Research Duty](../../../../.team/duties/RESEARCH_DUTY.md)
4. Follow Duty Procedure: Execute research steps

---

## Expected Behavior

### Duty Assignment
```python
duty = get_work_item_duty(work_item_id=1000)
# Returns: "research"
```

### Dispatch
- ✅ Orchestration dispatches to RESEARCH_DUTY.md
- ✅ Research duty procedure loaded
- ✅ No kernel leaks

### Execution
- ✅ Research steps clear
- ✅ Semantic operations used for creating research folder, handover work items
- ✅ Handover procedure referenced

---

## Success Criteria

- [ ] Duty assignment identifies "research"
- [ ] Dispatch to research duty successful
- [ ] Research procedure clear and executable
- [ ] No kernel leaks detected
- [ ] Can create handover work item to implementation duty

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Notes**:
