# Process Modeling Plan

## Current Work

**Issue**: TBD - Improve handover for implementation (Product Backlog System)
**Started**: 2025-11-08
**Status**: Complete ✅

### Workflows Being Updated
- [x] Research Workflow
- [x] Tech Debt Workflow
- [x] Implementation Workflow
- [x] Copilot Instructions
- [x] Issue Templates

### Proposed Changes

Introduced a centralized product backlog system under `/product` folder that:
1. Separates backlog registration from prioritization
2. Provides a unified system for all workflows to handover work to implementation
3. Allows product team to manage prioritization independently
4. Replaces the current `/research/backlog/` tech debt specific backlog

**Key Design Principles:**
- Backlog items are independent files (easier to manage in git)
- Each backlog item can have an associated handover folder for supporting assets
- Prioritization is maintained separately by product team
- Resolved items are archived for future reference
- All team workflows integrate at appropriate points

### Testing Status
- [x] Scenarios created
- [x] Initial tabletop simulation complete
- [x] Refinements based on feedback
- [x] Regression tests passed
- [x] Verbosity/redundancy check complete

### Test Results Summary

**Initial Testing** (Tabletop Simulation):
- Scenario 001 (Research): FAIL → Workflow didn't mention product backlog
- Scenario 002 (Tech Debt): FAIL → Referenced old backlog location
- Scenario 003 (Implementation): FAIL → No guidance on prioritization selection
- Scenario 004 (Product Team): PASS → No changes needed

**After Workflow Updates** (Retest):
- Scenario 001 (Research): PASS ✅
- Scenario 002 (Tech Debt): PASS ✅
- Scenario 003 (Implementation): PASS ✅
- Scenario 004 (Product Team): PASS ✅

All scenarios now pass. Regression test suite created.

### Deliverables

**Product Backlog System**:
- `/product/README.md` - Comprehensive documentation (13KB, covers all teams)
- `/product/prioritization.md` - Prioritization file with examples
- `/product/backlog-item-template.md` - Template for creating items
- `/product/backlog/` - Active backlog (with 2 migrated items)
- `/product/resolved/` - Archive structure

**Workflow Updates**:
- Research Workflow - Added Phase 5 (Create Product Backlog Item)
- Tech Debt Workflow - Updated to use product backlog throughout
- Implementation Workflow - Rewrote Step 2 for backlog integration
- Copilot Instructions - Updated repository structure

**Issue Templates**:
- Implementation template - Now uses backlog items instead of handover paths

**Testing Artifacts**:
- 4 test scenarios created
- All scenarios passed after workflow updates
- Regression test suite archived

**Migration**:
- 2 tech debt backlog items migrated to new system
- Old `/research/backlog/README.md` marked as DEPRECATED

### Archived

This plan has been completed and is ready for archiving.

**Archive Location**: `/research/workflow-modeling/archive/2025-11-08-product-backlog-system.md`

---

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`
