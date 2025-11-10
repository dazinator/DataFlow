---
name: Bulk Process Modeling
about: Trigger bulk processing of all issues in the process modeling queue
title: '[Process Modeling] Bulk processing - '
labels: ['workflow:process-modeling']
assignees: ''
---

## ⚠️ IMPORTANT: Bulk Process Modeling Mode

**This is a BULK PROCESS MODELING issue for processing multiple workflow improvement issues at once.**

@copilot **MUST** follow the Process Modeling workflow in `/.team/prompts/PROCESS_MODELING_WORKFLOW.md` in **BULK MODE**.

**Note**: @copilot will automatically append today's date to the issue title for tracking purposes.

### Bulk Process Modeling Instructions for @copilot:

When you see this bulk process modeling issue:

1. **Add today's date** to the issue title (if not already present)
2. **Query the process modeling queue** to get ALL issues with `workflow:process-modeling` label
3. **Exclude this bulk process modeling issue** from the list (only process other issues)
4. **For each issue in the queue**:
   - Read and assess the workflow improvement
   - Create test scenarios in `/research/workflow-modeling/scenarios/`
   - Execute tabletop simulations
   - Implement validated improvements
   - Update workflow documentation
   - Archive or revert test scenarios as appropriate
5. **Track your progress** by updating this issue with a summary:
   - Total issues processed
   - Workflows affected
   - Key improvements made
6. **Close this issue AND its PR** when all process modeling work is complete

### Expected Processing Approach

Follow the standard Process Modeling workflow for each improvement:
- Create test scenarios
- Run tabletop simulations
- Document results
- Refine based on feedback
- Update workflow files
- Archive plan and scenarios

### Queue Status

**Created**: [Auto-filled by GitHub]
**Queue Size at Creation**: [Manual: check queue size when creating this issue]

---

## For @copilot: Execution Plan

1. Add today's date to issue title (if not present)
2. Query process modeling queue using MCP tools
3. Process each improvement sequentially
4. Update this issue with progress summaries
5. Close this issue AND its PR when done

**See**: `.team/prompts/PROCESS_MODELING_WORKFLOW.md` for complete process modeling workflow.
