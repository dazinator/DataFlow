# Scenario 001: Verify Double Verification Prevents Re-Closing

## Context
Testing that the MANDATORY double-verification step prevents re-processing and re-closing of already-closed issues.

## Starting Point
- Bulk process modeling issue created
- Feedback tracker exists with mix of open and closed sub-issues
- Copilot agent starts Step 3: Query Process Modeling Queue

## Steps to Follow

1. **Query feedback tracker parent** (Step 3)
   - Use `search_issues` to find `[Workflow Feedback] Tracker`
   - Use `issue_read(method="get_sub_issues")` to get children

2. **Apply initial filter** (Step 3, line 435)
   - Filter: `issues = [sub for sub in parent_sub_issues if sub.state == "open"]`
   - This should remove closed issues from initial list

3. **MANDATORY double-verification** (Step 3, lines 437-450)
   - For each issue in filtered list
   - Call `issue_read(method="get", issue_number=issue.number)` 
   - Check if `issue_details.get('state') == 'open'`
   - Only keep truly open issues

4. **Verify closed issues excluded**
   - Confirmed: Closed issues are NOT in the final `verified_open_issues` list
   - Workflow continues with only open issues

## Expected Outcome

- Initial filter removes some closed issues (if sub-issue data is current)
- Double-verification catches any closed issues missed by initial filter (if sub-issue data is stale)
- Final `issues` list contains ONLY open issues
- No closed issues are re-processed or re-closed
- Agent proceeds to Step 3.5 or Step 4 with verified open issues only

## Success Criteria

- [x] Instructions clearly state double-verification is MANDATORY (not optional)
- [x] Code example shows individual `issue_read` for each issue
- [x] Comment explains why: "sub-issue queries may return stale data"
- [x] No closed issues make it to the processing loop
- [x] Prevents false progress from re-closing already-closed issues

## Test Result

**Status**: PASS

**Notes**: 
- Step 3 now has clear MANDATORY double-verification (lines 437-450)
- Comment explains the issue: "The sub-issue query may return stale data"
- Each issue verified individually before processing
- This prevents the critical bug where closed issues were re-closed (false progress)
- Instructions are unambiguous - agent MUST perform double verification
