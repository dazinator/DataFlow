# Process Modeling Archived Plan - Workflow File Ownership Clarification

## Summary

Added explicit guidance to prevent workflow file updates from being handled outside the process modeling workflow, and clarified the supported set of workflow labels to prevent label invention.

## Problem Statement

Issue #252 (Workflow Feedback Migration implementation) was created by process modeling as part of a multi-phase plan, but:

1. It was designated to the implementation workflow
2. It included tasks to update workflow documentation files
3. Workflow file updates should ALWAYS be handled by process modeling (requires tabletop simulation, validation)
4. The issue also mentioned invented label formats like "workflow-implementation" instead of "workflow:implementation"

This revealed two gaps:
1. No clear documentation that workflow files are OWNED by process modeling
2. No prominent list of supported workflow labels to prevent invention

## Before/After Impact

**Before** (baseline state):
- No explicit ownership statement for workflow files
- Implementation workflow had no checklist for workflow file detection
- No prominent list of supported workflow labels
- Room for agents to update workflow files directly
- Room for agents to invent new label formats

**After** (improved state):
- Explicit ownership in 3 locations (copilot-instructions, process modeling workflow, implementation workflow)
- Implementation workflow has checklist item: "Workflow File Check"
- Clear list of 6 supported labels with examples of incorrect formats
- Handover patterns provided for redirecting workflow file updates
- Process modeling clearly claims exclusive ownership

**Measured Impact**:
- 4/4 improved scenarios PASS
- Comprehensive coverage (implementation redirect, label validation, ownership clarity)
- ~250 lines of documentation added across 3 files

## Improvements Addressed

### 1. Workflow File Ownership Documentation

**copilot-instructions.md**:
- Added "Workflow Labels and File Ownership" section
- Lists files owned by process modeling
- Provides handover pattern for other workflows
- Explains why (tabletop testing, validation, consistency)

**PROCESS_MODELING_WORKFLOW.md**:
- Added "Exclusive Ownership of Workflow Files" section
- Lists owned files explicitly
- Explains unique requirements
- Shows handover pattern for other workflows
- Allows exception for minor typo fixes

**IMPLEMENTATION_WORKFLOW.md**:
- Added "Workflow File Check" to critical review checklist
- Added "Workflow File Updates - Separate to Process Modeling" section
- Provides complete handover pattern example
- Emphasizes completing implementation WITHOUT workflow file changes

### 2. Supported Workflow Labels

**copilot-instructions.md**:
- Added "Supported Workflow Labels" section
- Lists all 6 supported labels explicitly
- Shows examples of INCORRECT formats to avoid
- Emphasizes the `workflow:` format requirement
- Links to Workflow Topology Guide for complete schema

## Test Results

**4 scenarios created in** `/research/workflow-modeling/scenarios/workflow-file-ownership/`:

1. **Scenario 001 (Baseline)**: Implementation with workflow files
   - Status: FAIL (expected)
   - Documents the problem state
   - Shows agents would update workflow files directly

2. **Scenario 002 (Improved)**: Implementation redirects workflow files
   - Status: PASS ✅
   - Agent finds ownership guidance in copilot-instructions.md
   - Agent uses checklist in Implementation Workflow
   - Agent creates separate process modeling issue
   - Completes implementation without touching workflow files

3. **Scenario 003 (Improved)**: Workflow label validation
   - Status: PASS ✅
   - Agent finds comprehensive list of 6 supported labels
   - Agent sees examples of incorrect formats
   - Agent uses correct `workflow:` format

4. **Scenario 004 (Improved)**: Process modeling ownership
   - Status: PASS ✅
   - Process modeling workflow has clear ownership section
   - Lists all owned files explicitly
   - Explains why workflow files need special handling

## Scenario Statistics

**Created**: 4 scenarios
**Retained**: 0 scenarios (will be reverted - temporary validation only)
**Reverted**: 4 scenarios (after archiving this plan)
**Regression Tests Ran**: 0 (new improvement, no pre-existing scenarios)

**Retention Decision**: Revert All
**Rationale**: These scenarios validated a one-time improvement (adding ownership guidance). Once the documentation is in place and validated, the scenarios serve no ongoing regression testing purpose. The guidance itself is now the source of truth.

## Files Modified

- `.github/copilot-instructions.md` - Added "Workflow Labels and File Ownership" section (~90 lines)
- `.team/prompts/PROCESS_MODELING_WORKFLOW.md` - Added "Exclusive Ownership" section (~100 lines)
- `.team/prompts/IMPLEMENTATION_WORKFLOW.md` - Added workflow file checks and redirect guidance (~60 lines)
- `research/workflow-modeling/plan.md` - Tracked work
- `research/workflow-modeling/history.md` - Added history entry

**Total**: ~250 lines of documentation added (excluding temporary test scenarios)

## Lessons Learned

**What Worked Well**:
- Creating baseline scenario to document problem state
- Testing from multiple perspectives (implementation, process modeling, label usage)
- Positioning ownership guidance prominently (right after multi-phase issues in copilot-instructions)
- Providing complete handover pattern examples
- Using checklist format for workflow file detection

**What Could Improve**:
- Consider adding similar ownership statements for other critical file patterns (if identified in future)
- Could potentially add pre-commit hook to detect workflow file changes outside process modeling PRs (future enhancement)

**Key Insight**: Explicit ownership with actionable guidance is more effective than implicit expectations. The combination of:
1. Ownership statement (WHAT is owned)
2. Reasoning (WHY it's owned)
3. Detection checklist (HOW to identify)
4. Handover pattern (WHAT TO DO instead)

...creates a complete system that prevents the problem rather than just documenting it.
