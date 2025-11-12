# Integration Test Scenario 011: End-to-End Triage to Research Flow

**Purpose**: Verify complete flow from triage duty through handover to research duty  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Starting State

**Work Item #1011**:
- **Title**: "Evaluate new feature request"
- **Labels**: `workflow:triage`
- **Description**: "User requests background job scheduling feature"

---

## Procedure to Follow

### Phase 1: Triage Duty
1. Load Orchestration
2. Dispatch to Triage Duty
3. Assess work item
4. Determine it requires research
5. Create research handover work item
6. Hand over to research duty

### Phase 2: Research Duty
7. New agent receives research work item
8. Load Orchestration (fresh context)
9. Dispatch to Research Duty
10. Follow research procedure

---

## Expected Behavior

### Triage Phase
```python
# Triage assessment determines research needed
research_work_item_id = create_work_item(
    type="research",
    title="Research background job scheduling approaches",
    description="Investigate options for background jobs. See triage #1011.",
    duty="research"
)

# Update triage work item
add_work_item_comment(
    work_item_id=1011,
    text="[Copilot-Duty: triage] 🔄 Handover to research #[research_work_item_id]"
)

# Close or update triage work item
update_work_item(work_item_id=1011, status="closed")
```

### Research Phase
```python
duty = get_work_item_duty(work_item_id=research_work_item_id)
# Returns: "research"

# Dispatch to research duty
# Can reference original triage work item #1011
# Proceed with research investigation
```

---

## Success Criteria

- [ ] Triage duty completes assessment
- [ ] Handover procedure creates research work item correctly
- [ ] Research work item has correct duty label
- [ ] Research work item references triage work item
- [ ] Research agent can dispatch correctly
- [ ] No kernel leaks in entire flow
- [ ] All semantic operations used correctly

---

## Test Result

**Status**: ☐ PASS ☐ FAIL
