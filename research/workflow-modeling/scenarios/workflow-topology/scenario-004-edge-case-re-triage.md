# Scenario 004: Edge Case - Re-triage

## Context

Testing the workflow where an issue in implementation needs to go back to triage because requirements became unclear.

## Starting Point

- Issue #126 in `workflow:implementation`
- Started implementation
- Discovered requirements are ambiguous
- Multiple interpretation paths exist
- Needs reassessment and clarification

## Steps to Follow

Following `.team/workflows/IMPLEMENTATION_WORKFLOW.md` → "Handover to Triage" section:

1. **During implementation**:
   - Working on feature
   - Requirements state "optimize performance"
   - Unclear what "optimize" means (latency? throughput? memory?)
   - Unclear what target metrics are
   - Can't proceed without clarification

2. **Decision to re-triage**:
   - Stop implementation work
   - Hand back to triage for reassessment
   - Triage can gather more requirements or route elsewhere

3. **Handover back to triage**:
   ```bash
   ./.team/scripts/workflow/handover-issue.sh \
     126 implementation triage "Requirements unclear during implementation. 'Optimize performance' needs specific metrics and priorities. Needs reassessment."
   ```

4. **Verify transition**:
   ```bash
   # Should no longer be in implementation queue
   ./.team/scripts/workflow/query-workflow-queue.sh implementation
   
   # Should be back in triage queue
   ./.team/scripts/workflow/query-workflow-queue.sh triage
   ```

5. **Triage reassesses**:
   Following `.team/workflows/TRIAGE_WORKFLOW.md`:
   - Query triage queue finds issue #126
   - Review comments (see why it came back)
   - Re-assess with new context
   - Either:
     * Route to research (need approach validation)
     * Route back to implementation (clarified requirements)
     * Keep in triage (gather more info)

## Expected Outcome

- Issue successfully returned to triage
- Triage workflow can reassess with context
- Audit trail shows the loop
- Pattern for similar situations is clear
- No stigma about going "backwards"

## Success Criteria

- [x] Re-triage pattern is documented
- [x] Handover worked correctly
- [x] Issue is back in triage queue
- [x] Handover comment explains why
- [x] Triage can pick up from context
- [x] Workflow docs don't discourage re-triage
- [x] Pattern is clear for when to use

## Test Result

**Status**: PASS ✅

**Notes**:
Walked through workflow documentation successfully:

**Re-triage pattern documented**:
- ✅ IMPLEMENTATION_WORKFLOW.md line 826-831 has "Handover to Triage" section
- ✅ Clear "When" criteria: "Requirements were unclear or need re-evaluation"
- ✅ Script command: `./.team/scripts/workflow/handover-issue.sh $ISSUE implementation triage "Requirements unclear..."`

**Triage re-assessment capability**:
- ✅ TRIAGE_WORKFLOW.md supports receiving issues from any workflow
- ✅ Can query triage queue to find returned issues
- ✅ Assessment process same regardless of where issue came from

**Audit trail**:
- ✅ Handover comment explains why returning to triage
- ✅ Comment history shows the loop (triage → impl → triage)
- ✅ Context is preserved for reassessment

**Pattern acceptance**:
- ✅ No stigma about going "backwards"
- ✅ Workflow documentation normalizes this pattern
- ✅ Clear that re-triage is valid and useful

**Documentation Quality**:
- Re-triage pattern is explicit
- Not hidden or discouraged
- Clear when to use
- Both workflows support the pattern
- Audit trail ensures context preservation

**No gaps found** - re-triage loop well supported and documented.

## Observations

This scenario tests an important edge case:
- Workflows aren't strictly linear
- Issues may need to loop back
- Re-triage is a valid and useful pattern
- Audit trail (comments) is critical for loops
