# Integration Test Scenario 001: Dispatch to Triage Duty

**Purpose**: Verify orchestration correctly dispatches to triage duty  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Agent receives a work item with `workflow:triage` label and must dispatch to the triage duty.

---

## Starting State

**Work Item #999**:
- **Title**: "Assess new feature request for caching"
- **Labels**: `workflow:triage`
- **Description**: "User requests Redis caching support"
- **Status**: Open

---

## Procedure to Follow

1. **Load Orchestration**: Read [copilot-instructions.md](../../../../.github/copilot-instructions.md)
2. **Check Required Context**: Verify kernel and procedures references are present
3. **Apply Duty Assignment**: Use [Duty Assignment Procedure](../../../../.team/procedures/duty-assignment.md)
4. **Dispatch to Duty**: Load [Triage Duty](../../../../.team/duties/TRIAGE_DUTY.md)
5. **Follow Duty Procedure**: Execute triage steps

---

## Expected Behavior

### Step 1: Orchestration Entry
- ✅ Agent starts at orchestration layer
- ✅ Required Context section visible and referenced
- ✅ Navigation shows all 7 duties

### Step 2: Duty Assignment
```python
# Expected semantic operation call
duty = get_work_item_duty(work_item_id=999)
# Returns: "triage"
```

- ✅ Duty assignment procedure referenced
- ✅ Correct duty ("triage") identified from label
- ✅ No ambiguity in determining duty

### Step 3: Duty Dispatch
- ✅ Orchestration dispatches to TRIAGE_DUTY.md
- ✅ Agent loads triage duty procedure
- ✅ No kernel leaks (no direct GitHub MCP tool calls in orchestration)

### Step 4: Duty Execution
- ✅ Agent follows triage duty steps
- ✅ Uses semantic operations (not platform-specific code)
- ✅ Can complete triage assessment

---

## Success Criteria

- [ ] Orchestration layer loaded successfully
- [ ] Required Context section present with kernel/procedures references
- [ ] Duty assignment procedure correctly identified duty as "triage"
- [ ] Dispatch to triage duty successful
- [ ] No kernel leaks in orchestration or duty
- [ ] Agent can complete triage workflow using semantic operations

---

## Test Execution

**Date**: _________  
**Agent**: _________

### Observations

**Orchestration Load**:
- [ ] Required Context section found
- [ ] Kernel reference: [note]
- [ ] Procedures reference: [note]
- [ ] Duty Assignment section: [note]

**Duty Assignment**:
- [ ] Procedure referenced correctly
- [ ] Duty identified as: _________
- [ ] Assignment logic clear: YES / NO

**Dispatch**:
- [ ] Correct duty file loaded
- [ ] Duty procedure accessible
- [ ] Navigation clear: YES / NO

**Execution**:
- [ ] Triage steps clear
- [ ] Semantic operations used
- [ ] No kernel leaks detected

### Issues Found

_List any issues, ambiguities, or problems encountered_

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Pass Criteria Met**: ___/6

**Notes**:

_Detailed notes on test outcome_

---

## Follow-Up Actions

If FAIL:
- [ ] Document specific failure reason
- [ ] Create issue for orchestration/duty fix
- [ ] Re-run after fix

If PASS:
- [ ] Mark scenario as validated
- [ ] Archive for regression testing
