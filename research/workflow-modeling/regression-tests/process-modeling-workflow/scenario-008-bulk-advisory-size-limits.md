# Scenario 008: Bulk Processing - Advisory Size Limits with Confirmation

## Context
Testing that 400+ line threshold is advisory, not a hard stop - can continue with user confirmation.

## Problem Being Tested
Previous behavior: Bulk processing would hard stop at 400+ lines, preventing any further progress even if user wanted to continue.

## Expected Behavior (After Fix)
- 200-400 lines: Request confirmation before continuing
- 400+ lines: Request confirmation before continuing (advisory, not hard stop)
- With user confirmation ("continue"), can process additional items regardless of size
- Size limits are guidelines that provide natural pause points, not barriers

## Starting Point
- Feedback tracker has multiple actionable improvements
- Each improvement adds ~100-200 lines
- Bulk process modeling issue created
- User available to provide confirmation responses

## Steps to Follow

1. **Process first item**
   - Complete full workflow for first improvement
   - Track PR size: ~150 lines (hypothetical)
   - Check: items_processed = 1, total_lines < 200
   - Decision: Continue automatically (no confirmation needed)

2. **Process second item**
   - Complete full workflow for second improvement
   - Track PR size: ~280 lines total
   - Check: items_processed = 2, total_lines 200-400
   - Decision: Request confirmation

3. **Simulate confirmation at 200-400 lines**
   - Workflow posts comment: "PR approaching moderate size (200-400 lines), continue?"
   - User replies: "continue"
   - Workflow continues processing third item

4. **Process third item**
   - Complete full workflow for third improvement
   - Track PR size: ~430 lines total
   - Check: items_processed = 3, total_lines > 400
   - Decision: Request confirmation (advisory, not hard stop)

5. **Simulate confirmation at 400+ lines**
   - Workflow posts comment: "PR exceeded typical size (400+ lines). Advisory, not hard limit. Continue?"
   - User replies: "continue"
   - Workflow continues processing fourth item

6. **Process fourth item**
   - Complete full workflow for fourth improvement
   - Track PR size: ~580 lines total
   - Check: items_processed = 4, total_lines > 400
   - Decision: Request confirmation again

7. **Simulate stop decision**
   - Workflow posts confirmation request
   - User replies: "stop"
   - Workflow finalizes with partial completion

## Success Criteria
- [x] PR size checks happen after each item
- [x] Confirmation requested at 200-400 line threshold
- [x] Confirmation requested at 400+ line threshold (not hard stop)
- [x] With "continue" response, processing continues regardless of size
- [x] With "stop" response, workflow finalizes with partial completion
- [x] Messages clearly indicate 400+ is advisory, not absolute
- [x] No hard stops - always offers option to continue

## Test Result
**Status**: ✅ **PASS**
**Notes**: Tabletop simulation confirmed advisory size limits work correctly with confirmation requests.

### Observations
- Auto-continues when PR < 200 lines (smooth flow for small changes)
- Requests confirmation at 200-400 lines (moderate size pause point)
- Requests confirmation at 400+ lines with clear messaging about advisory nature
- "Continue" response allows processing regardless of size
- "Stop" response triggers partial completion correctly

### Confirmation Request Quality
- Message clarity: 5/5
- Included current metrics: Yes
- Included next item preview: Yes
- Made advisory nature clear: Yes (especially for 400+ message)

### Size Threshold Behavior
- 200 lines: Auto-continue ✅
- 250 lines: Request confirmation ✅
- 450 lines: Request confirmation (advisory) ✅
- After "continue" at 450 lines: Processing happens ✅

### Edge Cases Discovered
None - confirmation logic handles all size ranges appropriately

## Validation
After running this scenario:
- Check PR description for multiple items processed
- Verify final PR size matches expectations
- Confirm partial completion pattern was followed
- Review confirmation comments for clarity
