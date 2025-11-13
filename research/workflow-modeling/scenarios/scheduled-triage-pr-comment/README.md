# Scheduled Triage PR Comment - Test Scenarios

## Overview

Test scenarios for the scheduled triage workflow improvement that changes comment target from issue to PR.

## Issue Context

**Problem**: The scheduled bulk triage workflow commented on the tracker issue to trigger Copilot, but this didn't work because Copilot only triggers on PR comments, not issue comments.

**Solution**: 
1. Update script to find PR associated with tracker issue
2. Comment on PR instead of issue
3. Handle gracefully when no PR exists

**Secondary**: Simplify the process modeling issue template to remove references to old directory structure.

## Test Scenarios

### Scenario 001: Baseline - Comment on Issue (Old Behavior)
- **Type**: baseline
- **Purpose**: Document the old behavior that didn't work
- **Result**: FAIL ❌ (expected - shows why change was needed)
- **File**: `scenario-001-baseline-comment-on-issue.md`

### Scenario 002: Improved - Comment on PR (New Behavior)
- **Type**: improved
- **Purpose**: Validate new PR comment mechanism works
- **Result**: PASS ✅
- **File**: `scenario-002-improved-comment-on-pr.md`

### Scenario 003: Edge Case - No PR Exists
- **Type**: edge-case
- **Purpose**: Ensure graceful handling when PR doesn't exist
- **Result**: PASS ✅
- **File**: `scenario-003-edge-case-no-pr.md`

### Scenario 004: Verify Template References
- **Type**: verify
- **Purpose**: Validate issue template has correct path references
- **Result**: PASS ✅
- **File**: `scenario-004-verify-template-references.md`

## Test Results Summary

**Overall**: 3/3 improved scenarios PASSED ✅ (100% pass rate)

**Baseline**: 1 scenario documented (FAIL ❌ - expected, shows problem)

### Key Findings

1. **PR Search Works**: Script successfully finds PRs associated with tracker issue
2. **Comment Targeting Works**: Comments posted to PR instead of issue
3. **Copilot Triggering**: PR comments successfully trigger Copilot
4. **Error Handling**: Graceful degradation when no PR exists
5. **Template Accuracy**: All file references updated and verified

### Critical Validation

- Old behavior (commenting on issue) did NOT trigger Copilot
- New behavior (commenting on PR) DOES trigger Copilot
- Edge case (no PR) handled without errors
- Template references all valid and accurate

## Files Changed

1. `.github/scripts/trigger-bulk-triage.js` - PR search and comment logic
2. `.github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md` - Path updates

## Manual Testing Notes

To manually test the workflow:

```bash
# Trigger workflow manually
gh workflow run scheduled-bulk-triage.yml

# Check workflow run status
gh run list --workflow=scheduled-bulk-triage.yml

# View workflow logs
gh run view <run-id> --log
```

Expected in logs:
- "Posted triage request to PR #NNN (for issue #MMM)"
OR
- "No PR found for tracker issue #NNN"
- "Skipping comment - Copilot trigger requires a PR"

## Success Criteria

- [x] Old behavior documented (baseline)
- [x] New behavior validated (improved)
- [x] Edge case handled (no PR)
- [x] Template verified (correct references)
- [x] All scenarios pass (except expected baseline failure)
- [x] No regressions introduced
- [x] Clear error messages for debugging
