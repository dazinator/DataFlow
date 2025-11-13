# Scenario 002: Improved - Successful Bulk Triage Trigger

## Type
improved

## Context
Testing the complete bulk triage workflow after permission fix, ensuring it can find PRs, post comments, and trigger Copilot.

## Starting Point
- Workflow: `scheduled-bulk-triage.yml` with correct permissions
- Tracker issue exists with label `workflow:triage`
- PR exists that references the tracker issue
- Multiple issues in triage queue

## Steps to Follow
1. Workflow triggered (scheduled or manual)
2. Script finds or creates bulk triage tracker issue
3. Script queries triage queue to count pending issues
4. Script lists all open PRs with `pull_requests: read` permission
5. Script finds PR associated with tracker issue by:
   - Checking PR body for issue number reference
   - Checking PR title for issue number reference
   - Checking for tracker title mention
6. Script posts comment to PR to trigger @copilot
7. Comment includes queue size and bulk triage instructions

## Expected Outcome
- ✅ Workflow completes successfully
- ✅ PR listing succeeds with 200 OK (not 403)
- ✅ Associated PR is found
- ✅ Comment posted to PR with:
  - Date stamp
  - @copilot mention
  - Reference to `.team/duties/TRIAGE_DUTY.md`
  - Queue size count
  - Bulk mode instruction
- ✅ Console logs show:
  - "Posted triage request to PR #XXX (for issue #YYY)"
  - "Queue size: N issue(s)"

## Actual Outcome
To be verified through tabletop simulation:
- Permission check passes ✅
- PR listing logic executes ✅
- Comment posting succeeds ✅
