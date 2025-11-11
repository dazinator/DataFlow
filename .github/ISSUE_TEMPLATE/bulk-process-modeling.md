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

Bulk mode processes multiple workflow improvement issues in a single PR:

- ✅ **Always processes at least 1 item** (minimum progress guarantee - full workflow completion required)
- ✅ **Continues processing until queue is complete** (workflow files are easy to review, PR size not a concern)
- ✅ **Tracks progress by sub-items closed** (triage alone does NOT count as progress)

**Note**: Workflow file changes and copilot instructions are very easy to review. PR size is not a limiting factor for process modeling work.

---

## For @copilot

**Complete instructions**: See `.team/prompts/PROCESS_MODELING_WORKFLOW.md` → **Mode 2: Bulk Processing**

**Quick summary**:
1. Add today's date to this issue title
2. Query process modeling queue (structure-based from feedback tracker)
3. **Optional triage** (for large backlogs 10+ items) - dismiss low-value items
4. **Process all items** (complete workflow per item: scenarios → testing → implementation → documentation)
5. **Triage alone is NOT progress** - must complete at least one full improvement workflow
6. **Continue until queue is complete** - workflow files are easy to review, no PR size concerns
7. Complete self-improvement evaluation
