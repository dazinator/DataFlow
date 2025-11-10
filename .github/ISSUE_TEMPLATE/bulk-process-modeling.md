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

### Progressive Processing Approach

Bulk mode uses **progressive processing** to handle large backlogs realistically:

- ✅ **Always processes at least 1 item** (ensures continuous progress)
- ✅ **Continues while PR < 200 lines** (keeps PRs small and reviewable)
- ⚠️ **Requests confirmation at 200-400 lines** (moderate PR size)
- 🛑 **Stops at 400+ lines** (prevents overwhelming PRs)

**For large backlogs (20+ items)**: Expect multiple bulk processing sessions. Each creates a reviewable PR (200-400 lines), then create a new bulk issue for remaining items.

### Bulk Process Modeling Instructions for @copilot:

When you see this bulk process modeling issue:

1. **Add today's date** to the issue title (if not already present)
2. **Query the process modeling queue** from the feedback tracker (structure-based query)
3. **Exclude this bulk process modeling issue** from the list (only process workflow improvements)
4. **Use progressive processing:**
   - Always process at least 1 item (minimum progress guarantee)
   - Check PR size after each item using `git diff --stat`
   - Continue if PR < 200 lines
   - Request confirmation if PR reaches 200-400 lines
   - Stop at 400+ lines for review
5. **For each item processed:**
   - Read and assess the workflow improvement
   - Create test scenarios in `/research/workflow-modeling/scenarios/`
   - Execute tabletop simulations
   - Implement validated improvements
   - Update workflow documentation
   - Archive or revert test scenarios as appropriate
6. **Track your progress** with updates to this issue
7. **Complete (full or partial):**
   - **Full completion**: All items processed → close this issue
   - **Partial completion**: PR size reached 200-400 lines → keep issue open with `partial-completion` label
   - Create feedback issue for self-improvement evaluation
   - Mark PR ready for review

### Expected Processing Approach

Follow the standard Process Modeling workflow for each improvement:
- Create test scenarios
- Run tabletop simulations
- Document results
- Refine based on feedback
- Update workflow files
- Track PR size progressively

### Queue Status

**Created**: [Auto-filled by GitHub]
**Queue Size at Creation**: [Manual: check queue size when creating this issue]

---

## For @copilot: Progressive Processing Plan

1. Add today's date to issue title (if not present)
2. Query process modeling queue using structure-based approach
3. Process items progressively (1 minimum, continue while PR < 200 lines)
4. Request confirmation at 200-400 lines
5. Stop at 400+ lines
6. Update this issue with progress summaries
7. Complete with full or partial completion pattern
8. Create self-improvement feedback at end

**See**: `.team/prompts/PROCESS_MODELING_WORKFLOW.md` - Mode 2: Bulk Processing for complete instructions.
