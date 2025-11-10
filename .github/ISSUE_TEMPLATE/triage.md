---
name: Bulk Triage
about: Trigger bulk triage of all issues in the triage queue
title: '[Triage] Bulk triage - '
labels: ['workflow:triage']
assignees: ''
---

## ⚠️ IMPORTANT: Bulk Triage Mode

**This is a BULK TRIAGE issue for processing multiple issues at once.**

@copilot **MUST** follow the Triage workflow in `/.team/prompts/TRIAGE_WORKFLOW.md` in **BULK MODE**.

### Bulk Triage Instructions for @copilot:

When you see this bulk triage issue:

1. **Query the triage queue** to get ALL issues with `workflow:triage` label
2. **Exclude this bulk triage issue** from the list (only process other issues)
3. **For each issue in the queue**:
   - Read and assess the issue
   - Determine appropriate workflow
   - Update workflow label
   - Add handover comment with reasoning
4. **Track your progress** by updating this issue with a summary:
   - Total issues processed
   - Breakdown by destination workflow
   - Any issues needing clarification
5. **Close this issue** when all triage work is complete

### Expected Workflow Distribution

After bulk triage, issues should be distributed to:
- `workflow:research` - Issues with unknowns or needing validation
- `workflow:implementation` - Issues with clear requirements
- `workflow:tech-debt` - Code quality/refactoring issues
- `workflow:product-backlog` - Items needing prioritization
- `workflow:process-modeling` - Workflow/process improvements
- Closed - Duplicates, invalid, or out-of-scope issues

### Triage Queue Status

**Created**: [Auto-filled by GitHub]
**Queue Size at Creation**: [Manual: check queue size when creating this issue]

---

## For @copilot: Execution Plan

1. Query triage queue using MCP tools
2. Process each issue sequentially
3. Update this issue with progress after every 5 issues (or when complete)
4. Close this issue when done

**See**: `.team/prompts/TRIAGE_WORKFLOW.md` for complete triage process.
