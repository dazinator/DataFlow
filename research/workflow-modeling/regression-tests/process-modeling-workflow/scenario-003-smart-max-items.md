# Scenario 003: Smart Mode - Max Items Threshold

## Context
Agent receives a process modeling issue with "Backlog-Driven - Smart" mode selected. This tests the max items stopping condition (default: 5 items).

## Starting Point
- Issue template filled with "Backlog-Driven - Smart" mode selected
- `.github/workflow-improvements.md` has 8 unaddressed entries (all small changes <50 lines each)
- Agent starts from copilot-instructions.md navigation

## Steps to Follow

1. Read issue template and identify mode: "Backlog-Driven - Smart"
2. Navigate to `.team/workflows/PROCESS_MODELING_WORKFLOW.md`
3. Find "Mode 2: Backlog-Driven" → "Smart Mode" subsection
4. Read stopping criteria:
   - MAX_ITEMS = 5
   - MAX_LINES_THRESHOLD = 500
   - Stop when any threshold met or backlog exhausted
5. Initialize tracking:
   - items_processed = 0
   - total_lines_changed = 0

**For each iteration:**

6. Check stopping conditions BEFORE processing next item:
   - items_processed >= 5? → STOP
   - total_lines_changed >= 500? → STOP
   - No more unaddressed entries? → STOP
7. If not stopping: Select next unaddressed entry
8. Extract context and process improvement
9. Track changes: `git diff --stat` for this improvement
10. Update counters:
    - items_processed++
    - total_lines_changed += (insertions + deletions)
11. Update history.md
12. Remove entry from workflow-improvements.md
13. Update PR description with running summary
14. Loop back to step 6

**After 5th iteration:**

15. Check stopping conditions: items_processed = 5 → **STOP**
16. Finalize PR description with cumulative metrics
17. Archive plan with summary of all 5 items
18. Note in plan: "Stopped due to max items threshold (5)"

## Expected Outcome

- ✅ Agent processes exactly **5 backlog entries**
- ✅ Stops due to max items threshold, not change volume (total ~200 lines)
- ✅ PR description shows cumulative metrics
- ✅ History.md has 5 new entries
- ✅ 3 entries remain in workflow-improvements.md
- ✅ Archived plan documents why it stopped

## Success Criteria

- [ ] Smart mode algorithm is clearly documented
- [ ] Stopping conditions are checked before each iteration
- [ ] Max items threshold (5) is respected
- [ ] Cumulative tracking is working correctly
- [ ] Clear explanation of why processing stopped
- [ ] Workflow led to expected outcome

## Test Result

**Status**: PASS ✅

**Notes**:
Simulated the scenario by following smart mode workflow:

1. ✅ Identified mode from issue template: "Backlog-Driven - Smart"
2. ✅ Navigated to PROCESS_MODELING_WORKFLOW.md → "Smart Mode" subsection
3. ✅ Found default thresholds clearly documented:
   - MAX_ITEMS: 5 items
   - MAX_LINES_THRESHOLD: 500 lines changed
4. ✅ Initialization instructions clear
5. ✅ Iteration loop with stopping conditions at step 4:
   - "If items_processed >= max_items → STOP (reason: max items threshold)"
6. ✅ After 5th iteration: items_processed = 5
7. ✅ Check at step 4 triggers: "items_processed >= max_items" → STOP
8. ✅ Stopping reason documented: "Stopped after 5 items (max items threshold)"
9. ✅ Archive guidance includes stopping reason (step 17)

**Success Criteria Assessment:**
- ✅ Smart mode algorithm is clearly documented
- ✅ Stopping conditions are checked before each iteration (step 4)
- ✅ Max items threshold (5) is respected and clearly documented
- ✅ Cumulative tracking is working correctly (steps 9-10)
- ✅ Clear explanation of why processing stopped (step 15 and examples)
- ✅ Workflow led to expected outcome

**Conclusion**: Smart mode correctly implements max items stopping condition.
