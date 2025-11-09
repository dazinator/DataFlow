# Scenario 003: Implementation Reveals Tech Debt

## Context

Testing the workflow where implementation work uncovers technical debt that should be addressed, requiring a handover to the tech debt workflow.

## Starting Point

- Issue #125 in `workflow:implementation`
- Working on feature implementation
- Discovered legacy code patterns that need modernization
- Implementation can proceed but tech debt should be tracked

## Steps to Follow

Following `.team/workflows/IMPLEMENTATION_WORKFLOW.md` → "Handover to Tech Debt" section:

1. **Current work status**:
   - Implementing new feature
   - Found old namespace style in related code
   - Found CS0436 warnings in affected area
   - Could work around but should modernize

2. **Decide to hand over to tech debt**:
   - Implementation can continue
   - But tech debt should be documented for later
   - Create new issue or use current issue for tech debt

3. **Handover using script**:
   ```bash
   ./.team/scripts/workflow/handover-issue.sh \
     125 implementation tech-debt "Implementation revealed legacy code in data access layer that needs modernization"
   ```

4. **Verify transition**:
   ```bash
   # Should no longer be in implementation queue
   ./.team/scripts/workflow/query-workflow-queue.sh implementation
   
   # Should now be in tech-debt queue
   ./.team/scripts/workflow/query-workflow-queue.sh tech-debt
   ```

5. **Tech debt workflow picks up**:
   Following `.team/workflows/TECH_DEBT_WORKFLOW.md`:
   - Query tech-debt queue finds issue #125
   - Can now do systematic analysis
   - Create findings report
   - Create product backlog items

## Expected Outcome

- Issue transitioned to tech debt workflow
- Tech debt team can discover and analyze the issues
- Handover comment explains what was found
- Clear separation between implementation and debt analysis

## Success Criteria

- [x] Handover decision was clear in workflow doc
- [x] Handover script worked
- [x] Issue moved to tech-debt workflow
- [x] Handover comment explained what was found
- [x] Tech debt workflow can query and find issue
- [x] Pattern is clear for similar situations
- [x] No confusion about when to use this handover

## Test Result

**Status**: PASS ✅

**Notes**:
Walked through workflow documentation successfully:

**Handover to tech debt pattern**:
- ✅ IMPLEMENTATION_WORKFLOW.md line 794-811 has "Handover to Tech Debt" section
- ✅ Clear "When" criteria: "Implementation reveals technical debt"
- ✅ Both manual and script methods documented
- ✅ Comment template explains what tech debt was found

**Script command**:
- ✅ `./.team/scripts/workflow/handover-issue.sh $ISSUE implementation tech-debt "Implementation revealed technical debt in [area]"`
- ✅ Placeholder for area description

**Tech debt workflow reception**:
- ✅ TECH_DEBT_WORKFLOW.md line 42-60 has "Workflow Queue" section
- ✅ Entry points include "From Implementation workflow (debt discovered during work)"
- ✅ Can query: `./.team/scripts/workflow/query-workflow-queue.sh tech-debt`

**Use case clarity**:
- ✅ Clear when to use this handover
- ✅ Distinguishes between "must fix" vs "should track"
- ✅ Tech debt analysis can proceed systematically

**Documentation Quality**:
- Handover criteria are clear
- Both workflows reference each other
- Pattern makes sense for the use case
- No confusion about when to use

**No gaps found** - transition pattern well documented.
