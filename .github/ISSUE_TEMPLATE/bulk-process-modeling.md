---
name: Bulk Process Modeling
about: Trigger progressive processing of the process modeling queue
title: '[Process Modeling] Bulk processing - '
labels: ['workflow:process-modeling']
assignees: ''
---

## ⚠️ IMPORTANT: Bulk Process Modeling Mode

**This is a BULK PROCESS MODELING issue for progressive processing of workflow improvement issues.**

@copilot **MUST** follow the Process Modeling workflow in `/.team/prompts/PROCESS_MODELING_WORKFLOW.md` in **BULK MODE**.

**Note**: @copilot will automatically append today's date to the issue title for tracking purposes.

---

## What This Does

Bulk mode processes multiple workflow improvement issues in a single PR using **progressive processing**:

- ✅ **Always processes at least 1 item** (minimum progress guarantee)
- ✅ **Continues automatically while PR < 200 lines** (keeps PRs reviewable)
- ⚠️ **Requests confirmation at 200-400 lines** (moderate PR size)
- ⚠️ **Requests confirmation at 400+ lines** (advisory - can continue with approval)

**For large backlogs (20+ items)**: Multiple bulk sessions expected. Each creates a reviewable PR, then a new bulk issue continues with remaining items.

---

## For @copilot

**Complete instructions**: See `.team/prompts/PROCESS_MODELING_WORKFLOW.md` → **Mode 2: Bulk Processing**

**Quick summary**:
1. Add today's date to this issue title
2. Query process modeling queue (structure-based from feedback tracker)
3. Process items progressively (complete workflow per item)
4. Check PR size after each item
5. Request confirmation at thresholds or finalize PR
6. Complete self-improvement evaluation

**Progressive Processing**:
- 1 item minimum (full workflow: scenarios → testing → implementation → documentation)
- Continue automatically if PR < 200 lines
- Request confirmation at 200+ lines (you decide when to stop for review)
