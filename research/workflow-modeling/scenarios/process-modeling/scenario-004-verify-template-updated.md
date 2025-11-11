# Scenario 004: Verify Bulk Template Updated

## Context
Testing that the bulk process modeling issue template has been updated to remove line count references and emphasize progress by sub-items closed.

## Starting Point
- User creating a new bulk process modeling issue
- Using template: `.github/ISSUE_TEMPLATE/bulk-process-modeling.md`

## Steps to Follow

1. **Read "What This Does" section** (lines 20-26)
   - Should NOT mention 200/400 line thresholds
   - Should state: "Continues processing until queue is complete"
   - Should state: "workflow files are easy to review, PR size not a concern"
   - Should state: "Tracks progress by sub-items closed"

2. **Check for removed content**
   - Should NOT say: "Continues automatically while PR < 200 lines"
   - Should NOT say: "Requests confirmation at 200-400 lines"
   - Should NOT say: "Requests confirmation at 400+ lines"
   - Should NOT mention: "Multiple bulk sessions expected"

3. **Read "For @copilot" Quick Summary** (lines 34-40)
   - Should include: "Optional triage (for large backlogs 10+ items)"
   - Should include: "Process all items (complete workflow per item)"
   - Should include: "Triage alone is NOT progress"
   - Should include: "Continue until queue is complete"
   - Should NOT include: "Check PR size after each item"
   - Should NOT include: "Request confirmation at thresholds"

4. **Verify removed "Progressive Processing" section**
   - Old version had separate section explaining line thresholds
   - Should be removed or integrated into main instructions
   - No mention of 200 line auto-continue threshold

## Expected Outcome

- Template clearly states workflow files are easy to review
- No line count thresholds mentioned anywhere
- Emphasizes progress by sub-items closed (not lines changed)
- States that triage is NOT progress
- Instructs to continue until queue is complete

## Success Criteria

- [x] "What This Does" does NOT mention 200/400 line thresholds
- [x] States: "workflow files are easy to review, PR size not a concern"
- [x] States: "Tracks progress by sub-items closed"
- [x] Quick Summary includes: "Triage alone is NOT progress"
- [x] Quick Summary includes: "Continue until queue is complete"
- [x] No separate "Progressive Processing" section with line limits

## Test Result

**Status**: PASS

**Notes**:
- "What This Does" section completely revised (lines 20-26)
  * Removed all mentions of 200/400 line thresholds
  * Added: "Continues processing until queue is complete"
  * Added: "workflow files are easy to review, PR size not a concern"
  * Added: "Tracks progress by sub-items closed (triage alone does NOT count)"
  * Clear emphasis on completing work, not managing PR size
  
- "For @copilot" Quick Summary updated (lines 34-40)
  * Added step 3: "Optional triage (for large backlogs 10+ items)"
  * Added step 4: "Process all items (complete workflow per item)"
  * Added step 5: "Triage alone is NOT progress"
  * Added step 6: "Continue until queue is complete - no PR size concerns"
  * Removed: "Check PR size after each item"
  * Removed: "Request confirmation at thresholds or finalize PR"
  
- Old "Progressive Processing" section removed
  * Was explaining 200 line auto-continue and confirmation thresholds
  * Replaced with clear statements about completing the queue
  
- Template now aligns with workflow documentation
- Both emphasize: workflow files are easy to review, PR size not a limiting factor
- This fixes the issue where line count guidance was blocking progress
