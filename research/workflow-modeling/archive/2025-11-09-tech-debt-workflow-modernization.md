# Process Modeling Archived Plan - Tech Debt Workflow Modernization

## Summary

Updated the Tech Debt workflow to integrate with the product backlog system and removed the manual reviewer selection phase. Added verification check requirements to ensure implementation teams can confirm tech debt still exists before starting work.

## Selected Entry Details
- **Issue**: Tech debt discovery workflow improvements
- **Date**: 2025-11-09
- **Area**: Tech Debt Workflow (primary), Implementation Workflow (verification check)

## Before/After Impact

**Purpose**: Show concrete improvement to help future readers understand the value

**Before** (baseline state):
- Tech debt workflow referenced legacy `/research/backlog/` path (deprecated)
- Manual reviewer selection phase blocked workflow completion
- Findings split between "selected" (handover) and "non-selected" (backlog)
- Implementation teams sometimes found tech debt already fixed by others
- No verification check mechanism

**After** (improved state):
- Tech debt workflow uses `/product/backlog/` exclusively
- All findings go directly to product backlog (no blocking selection phase)
- Product Prioritization workflow handles selection separately
- Implementation workflow requires verification check at Step 0
- Clear separation: Discovery → Backlog → Prioritization → Implementation

**Measured Impact**:
- Eliminated reviewer selection bottleneck in tech debt workflow
- Tech debt discovery can complete without waiting for reviewer decisions
- Product team has full visibility of all findings for prioritization
- Verification checks prevent wasted implementation effort on already-fixed issues

## Improvements Addressed

1. **Updated Tech Debt workflow to use product backlog system**:
   - Changed all `/research/backlog/` references to `/product/backlog/`
   - Updated examples and guidance throughout
   - Integrated with Product Prioritization workflow

2. **Removed manual reviewer selection phase**:
   - Deleted entire Phase 4 (Reviewer Evaluation and Selection)
   - Removed "Reviewer Decision" section from findings report template
   - Removed split between "selected" and "non-selected" findings
   - All findings go to product backlog

3. **Added verification check requirement**:
   - Updated findings report template to require verification checks
   - Updated backlog item template with CRITICAL verification section
   - Added verification guidance to Implementation workflow Step 0
   - Included example commands and handling for both scenarios

## Test Results

All scenarios PASS:

- **scenario-001-baseline-current-workflow.md**: Identified all issues in current workflow
  - Found 6 `/research/backlog/` references
  - Found reviewer selection phase (lines 450-464)
  - Found findings report decision checkboxes (lines 426-433)
  - PASS

- **scenario-002-improved-direct-to-backlog.md**: Validated improved workflow
  - All findings to product backlog (no selection)
  - Clear handoff to Product Prioritization workflow
  - Simpler, faster workflow
  - PASS

- **scenario-001-verify-techdebt-exists.md** (implementation-workflow): Validated verification check
  - Verification at Step 0 (earliest point)
  - Clear handling for both outcomes
  - Natural fit in Handover Critical Review
  - PASS

- **scenario-003-regression-complete-flow.md**: Validated complete integration
  - Tech debt discovery → Product backlog → Prioritization → Implementation
  - All three workflows integrate cleanly
  - Verification check prevents wasted effort
  - PASS

## Scenario Statistics

**Created**: 4 scenarios
**Retained**: 1 scenario (archived to `/research/workflow-modeling/regression-tests/tech-debt-workflow/`)
**Reverted**: 3 scenarios (temporary validation only)
**Regression Tests Ran**: 0 pre-existing scenarios (first implementation of these workflow changes)

**Retention Decision**: Archive one comprehensive regression test
**Rationale**: 
- Scenario 001-003 validated specific aspects and were reverted after confirmation
- Scenario "regression-complete-flow" retained as comprehensive regression test
- Future changes to Tech Debt, Implementation, or Product Prioritization workflows can run this regression test
- Validates end-to-end integration across all three workflows
- Includes verification points for key requirements

## Files Modified

### Tech Debt Workflow (`.team/prompts/TECH_DEBT_WORKFLOW.md`)

**Updated Sections:**
1. DO/DON'T sections (lines 12-29)
2. OUTCOME statement (line 36)
3. Comparison table (lines 80-87)
4. Phase 1: Review existing backlog reference (line 164)
5. Phase 3: Cross-reference check (line 328)
6. Phase 3: Findings report template (lines 372-441)
7. Deleted Phase 4: Reviewer Evaluation and Selection (old lines 450-464)
8. Updated Phase 4 (was Phase 5): Create Product Backlog Items (lines 468-564)
9. Renamed Phase 6 to Phase 5: Code Reversion and Finalization (lines 575-618)
10. Updated "Using Product Backlog Items" section (lines 620-660)
11. Updated example (lines 763-783)
12. Updated Summary (lines 808-817)

**Key Changes:**
- 10 sections updated
- All `/research/backlog/` changed to `/product/backlog/`
- Manual reviewer selection phase completely removed
- Verification check requirement added throughout
- Integration with Product Prioritization workflow

### Implementation Workflow (`.team/prompts/IMPLEMENTATION_WORKFLOW.md`)

**Updated Sections:**
1. Step 0: Handover Critical Review checklist (lines 38-45)
2. Added "Tech Debt Verification Example" section (lines 73-106)

**Key Changes:**
- Added "Tech Debt Verification" to checklist
- Added comprehensive example with commands
- Clear guidance for both scenarios (tech debt exists / already fixed)
- Integration with backlog item verification check format

## Lessons Learned

**What worked well:**
- Tabletop simulation identified all necessary changes precisely
- Test scenarios covered baseline, improved, and regression cases
- Changes simplified workflow (removed blocking phase)
- Clear separation of concerns between discovery and prioritization
- Verification check fits naturally at existing Step 0

**What could be improved for future process modeling:**
- Could have created design document first (though changes were straightforward)
- Regression scenario tested integration early, catching potential issues

**Recommendations for future work:**
- When removing workflow phases, check for references throughout document
- When integrating workflows, create regression scenario testing complete flow
- Verification checks should be concrete (commands, expected results)
- Backlog item templates should include all requirements for implementation team

## Scenario Lifecycle Decision

**Decision**: Archive one comprehensive regression test, revert others

**Rationale**:
- Scenarios 1-3 validated specific aspects during development and were reverted after confirmation
- **Scenario "regression-complete-flow"** retained as comprehensive regression test
- This provides value for future workflow changes:
  - Can be run when modifying Tech Debt workflow
  - Can be run when modifying Implementation workflow  
  - Can be run when modifying Product Prioritization workflow
- Validates end-to-end integration across all three workflows
- Includes clear verification points for key requirements
- Balances repository cleanliness with regression testing value

**Scenarios reverted** (temporary validation only):
- `scenario-001-baseline-current-workflow.md` - Identified current state issues
- `scenario-002-improved-direct-to-backlog.md` - Validated new approach
- `scenario-001-verify-techdebt-exists.md` - Validated verification check integration

**Scenario archived** (regression testing):
- `scenario-001-regression-complete-flow.md` → `/research/workflow-modeling/regression-tests/tech-debt-workflow/`
