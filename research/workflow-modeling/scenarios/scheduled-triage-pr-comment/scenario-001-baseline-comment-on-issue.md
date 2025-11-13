# Scenario 001: Baseline - Comment on Issue (Old Behavior)

## Context

Testing the OLD behavior where the scheduled triage workflow comments on the tracker issue instead of the PR. This documents what DIDN'T work.

## Starting Point

- Old version of `.github/scripts/trigger-bulk-triage.js` (before changes)
- Tracker issue exists: `[Triage] Bulk Triage Tracker`
- Tracker issue has label `workflow:triage`
- Scheduled workflow runs (via cron or manual dispatch)

## Steps to Follow

### 1. Scheduled Workflow Triggers

The GitHub Actions workflow `.github/workflows/scheduled-bulk-triage.yml`:
1. Runs on schedule (daily at 2:00 AM UTC)
2. Checks out repository
3. Runs `.github/scripts/trigger-bulk-triage.js`

### 2. Script Finds or Creates Tracker Issue

Script behavior (old version):
1. Queries for existing tracker issue with title `[Triage] Bulk Triage Tracker`
2. If not found, creates new tracker issue
3. Counts items in triage queue
4. Posts comment directly to the tracker issue

### 3. Comment Posted to Issue

Old script posts:
```markdown
## Bulk Triage Request - YYYY-MM-DD

@copilot Please perform bulk triage following `.team/duties/TRIAGE_DUTY.md` in **BULK MODE**.

**Queue Size**: N issue(s) awaiting triage

**Instructions**: Process all issues in the triage queue and route them to the appropriate workflow duties.
```

## Expected Outcome (Old Behavior)

**Result**: Comment posted to tracker issue #NNN

**Problem**: Copilot is NOT triggered by comments on issues - only on PRs.

## Actual Outcome

**Status**: FAIL ❌ (Expected behavior - this is why the change was needed)

**Notes**:
- Comment successfully posted to issue
- Workflow completed without errors
- BUT: @copilot was NOT triggered
- Triage work items remained unprocessed
- Manual intervention required to start bulk triage

## Observations

This scenario demonstrates why the change was necessary:
- GitHub Copilot agent only triggers on PR comments, not issue comments
- The scheduled workflow was ineffective for automation
- This is the baseline that the improved version should fix
