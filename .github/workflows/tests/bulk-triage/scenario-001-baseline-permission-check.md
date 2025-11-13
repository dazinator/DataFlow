# Scenario 001: Baseline - Permission Check for PR Access

## Type
baseline

## Context
Testing that the bulk triage workflow has the necessary permissions to access pull requests when searching for PRs associated with the tracker issue.

## Starting Point
- Workflow: `scheduled-bulk-triage.yml` is configured with permissions
- Script: `trigger-bulk-triage.js` attempts to list pull requests at line 102
- GitHub Actions workflow is triggered (scheduled or manual)

## Steps to Follow
1. Workflow starts with defined permissions in YAML
2. Script executes `github.rest.pulls.list()` to find PRs
3. GitHub API validates workflow has `pull_requests: read` permission
4. API call succeeds or fails based on permission

## Expected Outcome
**BEFORE FIX**: 
- ❌ FAIL - HTTP 403 error "Resource not accessible by integration"
- Error indicates workflow needs `pull_requests: read` permission
- Script fails at line 102 when calling `github.rest.pulls.list()`

**AFTER FIX**:
- ✅ PASS - API call succeeds with 200 OK
- Script can list pull requests
- Workflow continues to find associated PR and post comment

## Actual Outcome
**BEFORE FIX**: ❌ FAIL
- Confirmed 403 error as reported in issue
- Error message: "Resource not accessible by integration"
- Missing permission: `pull_requests: read`

**AFTER FIX**: To be tested
- Added `pull-requests: read` to workflow permissions
- Expected to resolve the 403 error
