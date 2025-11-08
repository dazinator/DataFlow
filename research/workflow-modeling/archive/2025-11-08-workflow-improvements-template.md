# Process Modeling Plan

## Current Work

**Issue**: Better handover organisation to implementation team
**Started**: 2025-11-08
**Status**: In Progress

### Workflows Being Updated
- [x] Workflow Improvements Issue Template (`.github/ISSUE_TEMPLATE/workflow-improvements.md`)

### Proposed Changes
Simplify the workflow-improvements.md issue template by:
- Removing verbose file path listings (should be discoverable)
- Focusing on essential problem/solution information
- Keeping critical process modeling guidance
- Maintaining effectiveness while reducing friction

### Testing Status
- [x] Baseline scenarios created (current template)
- [x] Baseline tabletop simulation complete
- [x] Simplified template designed
- [x] Improved scenarios created
- [x] Improved tabletop simulation complete
- [x] Refinements based on feedback
- [x] Regression tests passed (none existed)
- [x] Verbosity/redundancy check complete
- [x] Template implemented

### Test Results Summary

**Baseline Testing (Scenarios 001, 002):**
- Simple improvement: Works but verbose, 10-15 min to complete
- Complex improvement: Painful, 20-25 min, high barrier to entry
- Key issue: User does discovery work that workflow should do

**Simplified Template Testing (Scenarios 003, 004, 005):**
- ✅ Simple improvement: PASS - 3-5 min (67% reduction)
- ✅ Complex improvement: PASS - 5-8 min (75% reduction)
- ✅ Minimal info edge case: PASS - Handles through clarification workflow
- Better focus: User describes problem/solution, copilot does discovery
- Scales gracefully: Similar effort for simple and complex changes

**Verdict:** Simplified template is significantly better:
- Lower barrier to entry (encourages more proposals)
- Better division of labor (user describes, agent discovers)
- Scales well (doesn't punish complex improvements)
- Natural collaboration workflow (copilot can request clarification)

## How to Continue

**Next Steps:**
1. Archive test scenarios as regression tests
2. Complete self-improvement evaluation
3. Archive this plan to `/research/workflow-modeling/archive/`
4. Clear plan.md for next work

---

## How to Start New Work (After Current Work Complete)

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in `/scenarios/[workflow-name]/`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`
