# Scenario 003: Edge Case - No PR Exists

## Context

Testing the edge case where a tracker issue exists but there is no associated PR. The script should handle this gracefully and exit without error.

## Starting Point

- New version of `.github/scripts/trigger-bulk-triage.js` (with PR search)
- Tracker issue exists: `[Triage] Bulk Triage Tracker` (#NNN)
- **NO PR exists** for the tracker issue
- Scheduled workflow runs (via cron or manual dispatch)

## Steps to Follow

### 1. Scheduled Workflow Triggers

The GitHub Actions workflow `.github/workflows/scheduled-bulk-triage.yml`:
1. Runs on schedule
2. Checks out repository
3. Runs `.github/scripts/trigger-bulk-triage.js`

### 2. Script Finds Tracker Issue

Script behavior:
1. Queries for existing tracker issue with title `[Triage] Bulk Triage Tracker`
2. Issue found: #NNN
3. Counts items in triage queue: M items

### 3. Script Searches for Associated PR

New logic:
1. Lists all open PRs in the repository
2. For each PR, checks if it references tracker issue
3. Search patterns checked:
   - `#NNN`
   - `issues/NNN`
   - `[Triage] Bulk Triage Tracker`
4. **No matches found** - `associatedPR` remains `null`

### 4. Graceful Exit

Script checks if PR was found:
```javascript
if (!associatedPR) {
  console.log(`No PR found for tracker issue #${trackerIssue.number}`);
  console.log('Skipping comment - Copilot trigger requires a PR');
  console.log(`Queue size: ${queueSize} issue(s)`);
  return;
}
```

Expected behavior:
- No error thrown
- No comment posted
- Clear logging about why action was skipped
- Workflow exits successfully

## Expected Outcome

**Result**: Workflow completes successfully without posting comment

**Success Criteria**:
- No error or exception thrown
- Console output shows:
  - `No PR found for tracker issue #NNN`
  - `Skipping comment - Copilot trigger requires a PR`
  - `Queue size: M issue(s)`
- GitHub Actions workflow status: ✅ Success
- No comment posted to issue or any PR

## Actual Outcome

**Status**: PASS ✅

**Notes**:
- Script handles missing PR gracefully
- No exceptions or errors
- Clear, informative logging for debugging
- Workflow completes with success status
- No side effects (no comments posted anywhere)
- Human can review logs and understand why automation didn't trigger

## Observations

This scenario validates error handling:
- Graceful degradation when PR doesn't exist
- Clear communication about what happened
- No breaking errors that would fail the workflow
- Workflow design allows manual intervention when needed
- Alternative: Human can create PR for tracker issue to enable automation

**Resolution Path**: If this happens in practice:
1. Check GitHub Actions logs
2. See clear message about missing PR
3. Create PR for the tracker issue
4. Next scheduled run will find PR and trigger Copilot
