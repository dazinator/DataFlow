# Scenario 004: Edge Case - Multiple PRs Reference Tracker

## Type
edge-case

## Context
Testing workflow behavior when multiple PRs reference the same tracker issue (e.g., during concurrent work or abandoned PRs).

## Starting Point
- Workflow: `scheduled-bulk-triage.yml` with correct permissions
- Tracker issue exists with label `workflow:triage`
- MULTIPLE PRs reference the tracker issue in body or title
- Triage queue has pending issues

## Steps to Follow
1. Workflow triggered
2. Script finds tracker issue
3. Script lists all open PRs with `pull_requests: read` permission ✅
4. Script searches for PRs referencing tracker issue
5. Script finds first matching PR in iteration order
6. Script posts comment to that PR only

## Expected Outcome
- ✅ Workflow completes successfully
- ✅ PR listing succeeds
- ✅ First matching PR receives comment
- ✅ Other PRs are ignored (no duplicate comments)
- ✅ Console logs show which PR was selected
- ✅ No errors or warnings

## Actual Outcome
To be verified through tabletop simulation:
- Permission check passes ✅
- First PR match logic works ✅
- No duplicate comments ✅

## Notes
Current implementation uses `for...break` pattern which stops at first match.
This is acceptable behavior - the workflow doesn't need to handle multiple PRs simultaneously.

If this becomes an issue in practice, could enhance to:
- Select most recently updated PR
- Select PR with specific label
- Post to all matching PRs (not recommended - spam)
