# Scenario 006: Smart Mode - Minimum One Item (Edge Case)

## Context
Agent receives a process modeling issue with "Backlog-Driven - Smart" mode selected. This tests the edge case where the FIRST item alone exceeds the line threshold.

## Starting Point
- Issue template filled with "Backlog-Driven - Smart" mode selected
- `.github/workflow-improvements.md` has 3 unaddressed entries
- Entry 1: ~600 lines changed (exceeds 500 line threshold by itself)
- Entry 2: ~100 lines changed
- Entry 3: ~80 lines changed
- Agent starts from copilot-instructions.md navigation

## Steps to Follow

1. Read issue template and identify mode: "Backlog-Driven - Smart"
2. Navigate to `.team/workflows/PROCESS_MODELING_WORKFLOW.md`
3. Find "Smart Mode" subsection
4. Read edge case guidance: "Process at least 1 item, even if it exceeds thresholds"
5. Initialize tracking:
   - items_processed = 0
   - total_lines_changed = 0

**Iteration 1:**

6. Check: items=0, lines=0 → Continue (must process at least one)
7. Process entry 1 (large change)
8. Track: `git diff --stat` shows +350/-250 = 600 lines
9. Update: items_processed=1, total_lines_changed=600

**Pre-Iteration 2:**

10. Check stopping conditions:
    - items_processed = 1 (< 5) ✓
    - total_lines_changed = 600 (> 500) → **STOP**
    - But: Already processed 1 item → OK to stop
11. Finalize PR: "Processed 1 item, ~600 lines changed (exceeded threshold)"
12. Archive plan: "Stopped after 1 item due to change volume (600 > 500)"

## Expected Outcome

- ✅ Agent processes **1 backlog entry** (the large one)
- ✅ Doesn't refuse to start because entry exceeds threshold
- ✅ Stops after 1st item since threshold now exceeded
- ✅ PR description explains the situation
- ✅ History.md has 1 new entry
- ✅ 2 entries remain in workflow-improvements.md
- ✅ Archived plan documents edge case handling

## Success Criteria

- [ ] Agent processes at least 1 item regardless of size
- [ ] Clear guidance on "minimum one item" rule
- [ ] Threshold check happens AFTER first item completes
- [ ] Doesn't get stuck refusing to start
- [ ] Clear explanation in archived plan
- [ ] Workflow led to expected outcome

## Test Result

**Status**: PASS ✅

**Notes**:
Simulated the scenario by following smart mode workflow:

1. ✅ Identified mode: "Backlog-Driven - Smart"
2. ✅ Found edge case handling at step 4:
   - "**Exception**: Always process at least 1 item, even if it exceeds thresholds"
3. ✅ Also documented at bottom of Smart Mode section:
   - "**Edge Case**: If first item alone exceeds line threshold, still process it (minimum 1 item rule). Then stop before processing 2nd item."
4. ✅ Iteration tracking:
   - Before item 1: items=0, lines=0
     - Check acknowledges: "must process at least one" → Continue
   - After item 1: items=1, lines=600
     - Processed successfully even though 600 > 500
5. ✅ Before item 2: Check at step 4
   - items=1 (< 5) ✓
   - lines=600 (> 500) → STOP
   - Exception only applies to first item
6. ✅ Stopping reason: "Stopped after 1 item due to change volume (600 > 500)"

**Success Criteria Assessment:**
- ✅ Agent processes at least 1 item regardless of size
- ✅ Clear guidance on "minimum one item" rule (step 4 exception + edge case note)
- ✅ Threshold check happens AFTER first item completes
- ✅ Doesn't get stuck refusing to start
- ✅ Clear explanation in archived plan (step 17 guidance)
- ✅ Workflow led to expected outcome

**Conclusion**: Smart mode correctly handles edge case where first item exceeds threshold.
