# Scenario: Verify - ADR Location is Logical

## Context
Testing that Architecture Decision Records are in the correct location and not mixed with workflow documentation.

## Starting Point
- Found `.team/workflows/adr/` folder
- ADRs should be in `.team/docs/adr/` or similar
- Workflows folder should contain only workflow documentation

## Steps to Follow
1. Review content of `.team/workflows/adr/2025-11-09-workflow-state-storage.md`
2. Determine if it's truly a workflow-specific ADR or general team ADR
3. Check if `.team/docs/` exists
4. If needed, create `.team/docs/adr/` and move ADR there
5. Update any references to the moved file

## Expected Outcome
- ADRs are in `.team/docs/adr/` (general team decisions)
- OR if workflow-specific, stay in `.team/workflows/adr/`
- Location is documented and consistent
- References updated if moved

## Success Criteria
- [ ] ADR location makes sense (general vs workflow-specific)
- [ ] If moved, all references updated
- [ ] Folder structure is logical and documented
- [ ] No confusion about where to put future ADRs

## Test Result
**Status**: PASS (with recommendation)
**Notes**:
- ✅ `.team/workflows/adr/` contains workflow-specific ADR: "Workflow State Storage Using GitHub Labels"
- ✅ This ADR is specifically about the workflow topology system design
- ✅ Location is appropriate - it's workflow-infrastructure-specific, not a general team decision
- Recommendation: Keep in `.team/workflows/adr/` since it's workflow-system-specific
- Alternative considered: Could move to `.team/docs/adr/` for all team-level decisions
- Decision: Current location is acceptable and logical
- Future: May create `.team/docs/adr/` for non-workflow ADRs when needed
