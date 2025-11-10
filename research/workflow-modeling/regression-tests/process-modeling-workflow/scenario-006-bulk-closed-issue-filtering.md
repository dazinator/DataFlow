# Scenario 006: Bulk Processing - Closed Issue Filtering

## Context
Testing that bulk process modeling correctly filters out closed issues and never re-closes them.

## Problem Being Tested
Previous behavior: Bulk processing would query closed issues and re-close them with different reasons ("not planned").

## Expected Behavior (After Fix)
- Query should filter to OPEN sub-issues only
- Closed issues should never appear in processing queue
- No re-closing of already-closed issues

## Starting Point
- Feedback tracker exists with mix of open and closed sub-issues
- Some closed issues were previously marked as "implemented" or "not planned"
- Bulk process modeling issue created

## Steps to Follow

1. **Query feedback tracker**
   - Follow Step 3 in PROCESS_MODELING_WORKFLOW.md
   - Find feedback tracker parent issue
   - Query sub-issues
   - Apply filter: `[sub for sub in parent_sub_issues if sub.state == "open"]`
   - Apply double verification (check each issue individually)

2. **Verify filtering worked**
   - Count issues in queue
   - Manually check a few issue numbers to confirm they're all open
   - Confirm no closed issues in the list

3. **Execute triage (if applicable)**
   - Follow Step 3.5 triage rules
   - Guard clause should skip any closed issues: `if issue_data.get('state') == 'closed': continue`
   - No attempts to close already-closed issues

4. **Process items**
   - Continue with normal bulk processing
   - Never encounter a closed issue

## Success Criteria
- [x] Query produces only OPEN issues
- [x] Double verification catches any closed issues that slipped through
- [x] Triage guard clause prevents processing closed issues
- [x] No closed issues are re-closed with different reasons
- [x] Workflow completes without errors related to closed issues

## Test Result
**Status**: ✅ **PASS**
**Notes**: Tabletop simulation confirmed all three layers of protection work correctly.

### Observations
- Initial filter `[sub for sub in parent_sub_issues if sub.state == "open"]` correctly removes closed issues
- Double verification provides belt-and-suspenders protection  
- Guard clause in triage acts as final safety net (should never trigger if filtering works)
- Three layers ensure closed issues never processed or re-closed

### Edge Cases Discovered
None - filtering approach is robust with multiple failsafes

## Validation
After running this scenario, check GitHub issue history:
- No closed issues should have state changes
- No new comments on closed issues from bulk processing
- Only open issues should have been processed
