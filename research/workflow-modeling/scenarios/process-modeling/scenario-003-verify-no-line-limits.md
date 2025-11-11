# Scenario 003: Verify No Line Count Limits for Workflow Files

## Context
Testing that line count thresholds have been removed and agents are instructed to process the entire queue without stopping based on PR size.

## Starting Point
- Bulk process modeling issue created
- Feedback tracker has 10 open sub-issues
- Copilot agent starts processing queue

## Steps to Follow

1. **Read Progressive Processing Rules** (Step 4, lines 627-654)
   - Rule 1: "Always process at least 1 item"
   - Rule 2: "Continue processing until PR is sizable"
   - Rule 3: "Track progress by sub-items closed"
   - No mention of 200/400 line thresholds
   - States: "Workflow file changes and copilot instructions are easy to review"
   - States: "PR size is NOT a concern for workflow documentation changes"

2. **Process first item** (Step 4)
   - Complete full workflow for item #1
   - Close improvement issue
   - items_processed = 1

3. **Check for stopping conditions**
   - NO line counting code (previously at lines 656-665)
   - NO decision tree checking PR size thresholds (previously at lines 670-745)
   - Simple: "Continue to next item"

4. **Process remaining items**
   - Process item #2, close it
   - Process item #3, close it
   - ... continue for all 10 items
   - No stops, no confirmations based on PR size

5. **Complete when queue empty** (Step 6)
   - All 10 items processed
   - Queue is empty
   - Add final summary comment
   - Close bulk process modeling issue

## Expected Outcome

- No line counting code in the workflow
- No stopping conditions based on PR size (200/400 lines)
- Agent processes all items until queue is complete
- Workflow emphasizes PR size is NOT a concern for workflow files
- Progress measured by items closed, not lines changed

## Success Criteria

- [x] Progressive Processing Rules do NOT mention 200/400 line thresholds
- [x] States explicitly: "PR size is NOT a concern for workflow documentation changes"
- [x] No line counting bash code (`git diff --stat`)
- [x] No decision tree with line threshold conditions
- [x] Simple processing loop: process → close → continue
- [x] Step 6 has only ONE completion pattern (not "full or partial")
- [x] Edge Cases section does NOT mention "Large Backlog" or "User Confirmation Timeout"

## Test Result

**Status**: PASS

**Notes**:
- Progressive Processing Rules completely revised (lines 627-654)
  * Removed Rules 2-5 about line thresholds
  * Added Rule 2: "Continue processing until PR is sizable"
  * Explicitly states: "PR size is NOT a concern"
  * Focus on completing improvements, not managing PR size
  
- Line counting code REMOVED (was lines 656-665)
  * No `git diff --stat` tracking
  * No parsing of insertions/deletions
  
- Stopping conditions REMOVED (was lines 670-745)
  * No decision tree checking items_processed and total_lines
  * No confirmation requests at 200/400 line thresholds
  * Simple flow: process next item
  
- Step 6 simplified (lines 684-714)
  * Only one completion pattern (not "full or partial")
  * Complete when queue is empty
  * No partial completion based on PR size
  
- Edge Cases cleaned up (lines 716-734)
  * Removed "Large Backlog" (was about line thresholds)
  * Removed "User Confirmation Timeout" (was about line thresholds)
  
- This fixes the issue where line limits were blocking progress on the input queue
- Workflow files ARE easy to review - PR size is genuinely not a concern
