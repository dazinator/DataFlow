# Scenario 007: Bulk Processing - Minimum Progress Guarantee

## Context
Testing that bulk process modeling always processes at least 1 item completely (full workflow), not just triage.

## Problem Being Tested
Previous behavior: Bulk processing could exit after only doing triage, without processing any improvements through to completion (scenarios, testing, implementation).

## Expected Behavior (After Fix)
- Always process at least 1 item COMPLETELY
- "Processing" means: read issue → create scenarios → tabletop simulation → implement → update docs → archive/revert
- Triage alone is NOT progress
- Must complete full workflow for at least 1 improvement

## Starting Point
- Feedback tracker has multiple open items
- Some items need triage (template placeholders, etc.)
- Some items are actionable improvements
- Bulk process modeling issue created

## Steps to Follow

1. **Query and triage (if needed)**
   - Follow Step 3 to query issues
   - Follow Step 3.5 to triage (if large backlog)
   - Close template placeholders, already-implemented items, etc.

2. **Check items_processed counter**
   - After triage, `items_processed` should still be 0
   - Triage doesn't count as processing

3. **Process first item COMPLETELY**
   - Select oldest actionable improvement (after triage)
   - Read and assess the improvement
   - Create test scenarios in `/research/workflow-modeling/scenarios/[workflow]/`
   - Execute tabletop simulations
   - Document results
   - Implement improvements if validated
   - Update workflow documentation as needed
   - Archive or revert scenarios

4. **Track completion**
   - After completing full workflow: `items_processed = 1`
   - This is when the "minimum guarantee" is satisfied

5. **Check PR size**
   - Even if PR exceeds 400 lines, first item must complete
   - Decision tree: `if items_processed == 0: process_next = True`

## Success Criteria
- [x] Triage (if done) doesn't count as processing
- [x] At least 1 item processed completely (scenarios → testing → implementation)
- [x] First item completes even if it alone exceeds 400 lines
- [x] No exit after triage-only
- [x] Workflow produces actual improvements, not just queue cleanup

## Test Result
**Status**: ✅ **PASS**
**Notes**: Tabletop simulation confirmed triage doesn't count as processing and first item always completes fully.

### Observations
- Triage correctly doesn't increment `items_processed` counter
- Decision tree guarantees first item processes when `items_processed == 0`
- Clear distinction maintained between cleanup (triage) and progress (full processing)
- First item always completes full workflow: scenarios → testing → implementation

### Triage vs Processing Distinction
- Items triaged: [variable - cleanup phase]
- Items fully processed: [minimum 1 guaranteed]
- Clear distinction maintained: Yes

### Edge Cases Discovered
None - minimum guarantee logic is clear and unambiguous

## Validation
After running this scenario:
- Check that at least 1 workflow file was updated (real changes)
- Check that test scenarios were created (full process happened)
- Verify items_processed >= 1 before any stopping decisions
- Confirm triage-only exit is impossible
