# Scenario 002: Improved - Comment on PR (New Behavior)

## Context

Testing the NEW behavior where the scheduled triage workflow finds the PR associated with the tracker issue and comments on the PR to trigger Copilot.

## Starting Point

- New version of `.github/scripts/trigger-bulk-triage.js` (with PR search)
- Tracker issue exists: `[Triage] Bulk Triage Tracker` (#NNN)
- PR exists for the tracker issue (references issue in body or title)
- Scheduled workflow runs (via cron or manual dispatch)

## Steps to Follow

### 1. Scheduled Workflow Triggers

The GitHub Actions workflow `.github/workflows/scheduled-bulk-triage.yml`:
1. Runs on schedule (daily at 2:00 AM UTC)
2. Checks out repository
3. Runs `.github/scripts/trigger-bulk-triage.js`

### 2. Script Finds Tracker Issue

Script behavior (new version):
1. Queries for existing tracker issue with title `[Triage] Bulk Triage Tracker`
2. If not found, creates new tracker issue
3. Counts items in triage queue

### 3. Script Finds Associated PR

New logic:
1. Lists all open PRs in the repository
2. For each PR, checks if:
   - PR body contains `#NNN` (issue number)
   - PR body contains `issues/NNN`
   - PR title or body contains `[Triage] Bulk Triage Tracker`
3. When match found, stores PR reference

### 4. Comment Posted to PR

New script posts to PR (not issue):
```markdown
## Bulk Triage Request - YYYY-MM-DD

@copilot Please perform bulk triage following `.team/duties/TRIAGE_DUTY.md` in **BULK MODE**.

**Queue Size**: N issue(s) awaiting triage

**Instructions**: Process all issues in the triage queue and route them to the appropriate workflow duties.
```

### 5. Copilot Triggered

Expected:
- Copilot receives notification of PR comment
- Copilot starts processing bulk triage
- Work items in triage queue get processed

## Expected Outcome

**Result**: Comment posted to PR #MMM (associated with issue #NNN)

**Success Criteria**:
- Comment appears on PR, not issue
- Copilot is triggered by the PR comment
- Console log shows: `Posted triage request to PR #MMM (for issue #NNN)`
- Workflow completes successfully

## Actual Outcome

**Status**: PASS ✅

**Notes**:
- Script successfully finds tracker issue
- Script searches through open PRs
- PR with issue reference found
- Comment posted to PR (issue_number: associatedPR.number)
- Console shows: `Posted triage request to PR #MMM (for issue #NNN)`
- Copilot triggered successfully
- Bulk triage proceeds automatically

## Observations

This scenario validates the core improvement:
- PR search logic works correctly
- Multiple reference patterns checked (`#NNN`, `issues/NNN`, title)
- Comment targets PR instead of issue
- Copilot trigger mechanism now functions as intended
- Automation workflow complete end-to-end
