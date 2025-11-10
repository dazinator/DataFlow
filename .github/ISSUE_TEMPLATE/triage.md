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

**Note**: @copilot will automatically append today's date to the issue title for tracking purposes.

### Bulk Triage Instructions for @copilot:

When you see this bulk triage issue:

1. **Add today's date** to the issue title (if not already present)
2. **Detect unlabeled issues** - find ALL open issues without any workflow label and add `workflow:triage` label
3. **Query the triage queue** to get ALL issues with `workflow:triage` label
4. **Exclude this bulk triage issue** from the list (only process other issues)
5. **For each issue in the queue**:
   - Read and assess the issue
   - Determine appropriate workflow
   - Update workflow label
   - Add handover comment with reasoning
6. **Track your progress** by updating this issue with a summary:
   - Total issues processed (including newly labeled historic issues)
   - Breakdown by destination workflow
   - Any issues needing clarification
7. **Close this issue AND its PR** when all triage work is complete

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

1. Add today's date to issue title (if not present)
2. Detect and label historic issues without workflow labels
3. Query triage queue using MCP tools
4. Process each issue sequentially
5. Update this issue with progress after every 5 issues (or when complete)
6. Close this issue AND its PR when done

**See**: `.team/prompts/TRIAGE_WORKFLOW.md` for complete bulk triage process.
