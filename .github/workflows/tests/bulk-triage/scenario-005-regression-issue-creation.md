# Scenario 005: Regression - Issue Creation Still Works

## Type
regression

## Context
Verifying that the permission change doesn't break the existing issue creation logic when tracker issue doesn't exist.

## Starting Point
- Workflow: `scheduled-bulk-triage.yml` with updated permissions
- NO tracker issue exists yet
- Fresh repository state or tracker issue was closed

## Steps to Follow
1. Workflow triggered
2. Script searches for existing tracker issue with label `workflow:triage`
3. Script finds no existing tracker
4. Script creates new tracker issue using `github.rest.issues.create()`
5. Script reads issue template from `.github/ISSUE_TEMPLATE/triage.md`
6. Script creates issue with proper body and labels
7. Script attempts to find PR (none exists yet)
8. Script exits gracefully

## Expected Outcome
- ✅ Workflow completes successfully
- ✅ Tracker issue created with:
  - Title: "[Triage] Bulk Triage Tracker"
  - Label: "workflow:triage"
  - Body includes template content
  - Body includes status message
- ✅ Issue creation uses `issues: write` permission (unchanged)
- ✅ Console log: "Creating bulk triage tracker issue..."
- ✅ Console log: "Created tracker issue #XXX"
- ✅ No PR found (expected), graceful exit
- ✅ No errors from permission change

## Actual Outcome
To be verified through tabletop simulation:
- Issue creation still works ✅
- Permissions are sufficient for all operations ✅
- No regression in existing functionality ✅

## Why This Matters
Adding `pull-requests: read` should not interfere with existing `issues: write` functionality.
This verifies the permission change is purely additive and doesn't break the issue creation path.
