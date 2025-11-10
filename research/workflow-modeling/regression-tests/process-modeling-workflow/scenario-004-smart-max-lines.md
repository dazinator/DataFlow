# Scenario 004: Smart Mode - Max Lines Threshold

## Context
Agent receives a process modeling issue with "Backlog-Driven - Smart" mode selected. This tests the change volume stopping condition (default: 500 lines).

## Starting Point
- Issue template filled with "Backlog-Driven - Smart" mode selected
- `.github/workflow-improvements.md` has 10 unaddressed entries
- Entry 1: ~100 lines changed
- Entry 2: ~150 lines changed
- Entry 3: ~200 lines changed
- Entry 4: ~120 lines changed (would exceed threshold)
- Entries 5-10: Various sizes
- Agent starts from copilot-instructions.md navigation

## Steps to Follow

1. Read issue template and identify mode: "Backlog-Driven - Smart"
2. Navigate to `.team/prompts/PROCESS_MODELING_WORKFLOW.md`
3. Find "Smart Mode" subsection with stopping criteria
4. Initialize tracking:
   - items_processed = 0
   - total_lines_changed = 0
   - MAX_LINES_THRESHOLD = 500

**Iteration 1:**

5. Check: items=0, lines=0 → Continue
6. Process entry 1
7. Track: `git diff --stat` shows +60/-40 = 100 lines
8. Update: items_processed=1, total_lines_changed=100

**Iteration 2:**

9. Check: items=1, lines=100 → Continue
10. Process entry 2
11. Track: `git diff --stat` shows +90/-60 = 150 lines
12. Update: items_processed=2, total_lines_changed=250

**Iteration 3:**

13. Check: items=2, lines=250 → Continue
14. Process entry 3
15. Track: `git diff --stat` shows +120/-80 = 200 lines
16. Update: items_processed=3, total_lines_changed=450

**Iteration 4 (would exceed):**

17. Check: items=3, lines=450 → Continue (still under 500)
18. Process entry 4
19. Track: `git diff --stat` shows +70/-50 = 120 lines
20. Update: items_processed=4, total_lines_changed=570

**Pre-Iteration 5:**

21. Check: items=4, lines=570 → **STOP** (exceeds MAX_LINES_THRESHOLD)
22. Finalize PR description: "Processed 4 items, ~570 lines changed"
23. Archive plan: "Stopped due to change volume threshold (>500 lines)"

## Expected Outcome

- ✅ Agent processes **4 backlog entries** (not 5)
- ✅ Stops due to change volume threshold (~570 lines > 500)
- ✅ Stopping condition checked BEFORE processing 5th item
- ✅ PR description shows cumulative metrics
- ✅ History.md has 4 new entries
- ✅ 6 entries remain in workflow-improvements.md
- ✅ Archived plan documents stopping reason

## Success Criteria

- [ ] Change volume tracked accurately via git diff --stat
- [ ] Threshold checked before each iteration
- [ ] Stops at correct point (after 4th item)
- [ ] Cumulative line count calculation is correct
- [ ] Clear explanation of stopping reason
- [ ] Workflow led to expected outcome

## Test Result

**Status**: PASS ✅

**Notes**:
Simulated the scenario by following smart mode workflow:

1. ✅ Identified mode: "Backlog-Driven - Smart"
2. ✅ Found stopping criteria in workflow (step 4):
   - Check: "If total_lines_changed >= max_lines → STOP"
3. ✅ Tracking mechanism documented (steps 9-10):
   - `git diff --stat | tail -1`
   - Extract insertions + deletions
   - Update: `total_lines_changed += (insertions + deletions)`
4. ✅ Iteration tracking:
   - After item 1: total = 100 lines (< 500) → Continue
   - After item 2: total = 250 lines (< 500) → Continue
   - After item 3: total = 450 lines (< 500) → Continue
   - After item 4: total = 570 lines (> 500) → Check triggers
5. ✅ Before item 5: Check at step 4 detects total_lines_changed = 570 >= 500 → STOP
6. ✅ Stopping reason documented: "Stopped after 4 items with ~570 lines changed (change volume threshold)"

**Success Criteria Assessment:**
- ✅ Change volume tracked accurately via git diff --stat (documented in step 9)
- ✅ Threshold checked before each iteration (step 4)
- ✅ Stops at correct point (after 4th item, before 5th)
- ✅ Cumulative line count calculation is correct (step 10)
- ✅ Clear explanation of stopping reason (step 15, examples provided)
- ✅ Workflow led to expected outcome

**Conclusion**: Smart mode correctly implements change volume threshold.
