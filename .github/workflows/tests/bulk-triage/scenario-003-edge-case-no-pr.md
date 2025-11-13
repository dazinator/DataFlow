# Scenario 003: Edge Case - No PR Exists

## Type
edge-case

## Context
Testing the workflow's graceful handling when the tracker issue exists but no associated PR has been created yet.

## Starting Point
- Workflow: `scheduled-bulk-triage.yml` with correct permissions
- Tracker issue exists with label `workflow:triage`
- NO PR exists that references the tracker issue
- Multiple issues in triage queue

## Steps to Follow
1. Workflow triggered (scheduled or manual)
2. Script finds existing bulk triage tracker issue
3. Script queries triage queue successfully
4. Script lists all open PRs with `pull_requests: read` permission ✅
5. Script searches for PR referencing tracker issue
6. Script finds NO matching PR (associatedPR = null)
7. Script exits gracefully without posting comment

## Expected Outcome
- ✅ Workflow completes successfully (not a failure)
- ✅ PR listing succeeds with 200 OK
- ✅ No PR found, script exits gracefully
- ✅ Console logs show:
  - "No PR found for tracker issue #XXX"
  - "Skipping comment - Copilot trigger requires a PR"
  - "Queue size: N issue(s)"
- ✅ No comment posted (correct behavior)
- ✅ No errors thrown

## Actual Outcome
To be verified through tabletop simulation:
- Permission allows PR listing ✅
- Graceful exit when no PR exists ✅
- Appropriate logging ✅
- No false errors ✅

## Why This Matters
This is the expected state when:
- Tracker issue is newly created
- PR hasn't been opened yet for current work
- System is waiting for Copilot to create PR

The workflow should handle this gracefully, not fail.
