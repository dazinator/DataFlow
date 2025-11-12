# Integration Test Scenario 010: End-to-End Research to Implementation Flow

**Purpose**: Verify complete flow from research duty through handover to implementation duty  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Complete workflow from research investigation through handover to implementation.

---

## Starting State

**Work Item #1010**:
- **Title**: "Research Redis caching approach"
- **Labels**: `workflow:research`
- **Description**: "Investigate Redis integration for DataFlow pipelines"
- **Status**: Open

---

## Procedure to Follow

### Phase 1: Research Duty
1. Load Orchestration
2. Dispatch to Research Duty
3. Follow research procedure:
   - Create research folder
   - Document findings
   - Create implementation handover work item
   - Hand over to implementation duty

### Phase 2: Handover
4. Use [Handover Procedure](../../../../.team/procedures/handover.md)
5. Use [Work Item Creation Procedure](../../../../.team/procedures/work-item-creation.md)
6. Create implementation work item with correct duty

### Phase 3: Implementation Duty
7. New agent receives implementation work item
8. Load Orchestration (fresh context)
9. Dispatch to Implementation Duty
10. Follow implementation procedure

---

## Expected Behavior

### Research Phase
```python
# Create research folder (semantic operation)
# Document findings in /research/redis-caching/

# Create implementation handover work item
impl_work_item_id = create_work_item(
    type="implementation",
    title="[Implementation] Add Redis caching support",
    description="Implement based on research #1010. See /research/redis-caching/ for design.",
    duty="implementation"
)

# Close research work item
update_work_item(
    work_item_id=1010,
    status="closed"
)

# Add handover comment
add_work_item_comment(
    work_item_id=1010,
    text="[Copilot-Duty: research] ✅ Research complete. Handover to implementation #[impl_work_item_id]"
)
```

### Handover Transition
- ✅ Research work item closed
- ✅ Implementation work item created with correct duty label
- ✅ Implementation work item references research folder
- ✅ Handover comment added

### Implementation Phase
```python
# New agent starts
duty = get_work_item_duty(work_item_id=impl_work_item_id)
# Returns: "implementation"

# Load implementation duty
# Access research folder /research/redis-caching/
# Implement based on design
```

- ✅ Implementation agent can access research findings
- ✅ Clear linkage between research and implementation
- ✅ No context lost in transition

---

## Success Criteria

- [ ] Research duty completes successfully
- [ ] Handover procedure creates implementation work item correctly
- [ ] Implementation work item has correct duty label
- [ ] Implementation work item references research folder
- [ ] Implementation agent can dispatch correctly
- [ ] Implementation agent can access research findings
- [ ] No kernel leaks in entire flow
- [ ] All semantic operations used correctly

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Notes**:
