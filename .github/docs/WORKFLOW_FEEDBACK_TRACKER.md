# Workflow Feedback Tracker

## Overview

The **Workflow Feedback Tracker** is a parent GitHub issue that consolidates all workflow improvement suggestions and feedback from Copilot agents. It serves as the central hub for continuous workflow improvement.

**Issue Title**: `[Workflow Feedback] Tracker` (exact - workflows search by this title)

**Current Issue**: Search for it using: `"[Workflow Feedback] Tracker" in:title state:open`

## Purpose

The feedback tracker enables:

1. **Continuous Improvement**: Agents provide feedback after completing work, which feeds into process modeling improvements
2. **Centralized Tracking**: All workflow feedback is tracked as child issues under this single parent
3. **Bulk Processing**: Process Modeling workflow queries sub-issues from this parent to process feedback in batches
4. **Self-Improvement Loop**: Ensures workflows continuously evolve based on real agent experiences

## Key Characteristics

### Fixed Title Convention

- **Title**: `[Workflow Feedback] Tracker` (exact - do not change)
- **Why**: Workflows use title-based search to find this issue dynamically
- **Resilience**: If deleted, recreate with the exact same title - workflows will find it automatically

### Organizational Structure

```
[Workflow Feedback] Tracker (Parent Issue #254 or similar)
├── Feedback Item #1 (child issue)
├── Feedback Item #2 (child issue)
├── Feedback Item #3 (child issue)
└── ...
```

- **Parent**: The tracker issue itself
- **Children**: Individual feedback items linked as sub-issues
- **State**: Open children = not yet addressed; Closed children = implemented improvements

## How It Works

### For Workflow Agents

When completing work on any issue:

1. **Evaluate**: Reflect on what worked well and what didn't
2. **Create**: Create a child feedback issue
3. **Link**: Link child to tracker parent using `sub_issue_write` MCP tool
4. **Label**: Apply `workflow:process-modeling` label (though structure-based queries will find it regardless)

**Example**:
```python
# Find parent tracker by title
tracker_results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

if not tracker_results or len(tracker_results) == 0:
    raise ValueError("Feedback tracker parent issue not found. Expected issue with title '[Workflow Feedback] Tracker'")

tracker_issue = tracker_results[0]

# Create feedback issue
child = issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="[Brief description of improvement]",
    labels=["workflow:process-modeling"],
    body=f"""## Workflow Feedback Entry

**Date**: {datetime.now().strftime("%Y-%m-%d")}
**Issue/PR**: #XXX
**Workflow**: [Workflow Name]

### What Worked Well
[List specific positives]

### What Didn't Work Well
[List specific issues]

### Suggested Improvement
[Specific, actionable improvements]
"""
)

# Link to parent tracker
sub_issue_write(
    method="add",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_issue.number,
    sub_issue_id=child.id
)
```

### For Process Modeling Workflow

Process Modeling workflow queries sub-issues from the tracker:

```python
# Find the feedback tracker parent issue by title
tracker_results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

if not tracker_results or len(tracker_results) == 0:
    raise ValueError("Feedback tracker parent issue not found")

tracker_issue = tracker_results[0]

# Query sub-issues from the feedback tracker parent
parent_sub_issues = issue_read(
    method="get_sub_issues",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_issue.number
)

# Filter to open sub-issues only
open_feedback = [sub for sub in parent_sub_issues if sub.state == "open"]
```

## If Deleted or Recreated

**Resilience**: The system uses title-based lookup, not issue numbers.

If the tracker issue is deleted:

1. **Create new issue** with exact title: `[Workflow Feedback] Tracker`
2. **Apply label**: `workflow:process-modeling`
3. **Use template**: See [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md) for complete issue template
4. **No code changes needed**: Workflows will find it automatically by title

**Why this works**: All workflow code uses `search_issues()` with the title query, not hardcoded issue numbers.

## Workflow Integration

### Workflows That Create Feedback

All workflows create feedback issues after completing work:

- **Research Workflow**: After completing research and creating handover
- **Implementation Workflow**: After implementing features/fixes
- **Tech Debt Workflow**: After discovering/analyzing tech debt
- **Product Prioritization Workflow**: After prioritizing backlog items
- **Process Modeling Workflow**: After improving workflows
- **Triage Workflow**: After triaging issues

See: [Self-Improvement Loop in copilot-instructions.md](../copilot-instructions.md#self-improvement-loop)

### Workflows That Process Feedback

- **Process Modeling Workflow**: Processes feedback items through bulk processing mode
- **Bulk Triage Workflow**: Validates that feedback items have proper labels

## Required Setup

When setting up a new repository:

1. **Create the tracker issue** with exact title `[Workflow Feedback] Tracker`
2. **Apply label**: `workflow:process-modeling`
3. **Use template** from [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md)

See complete setup guide: [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md#5-create-required-parent-issues)

## References

- **Setup Guide**: [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md)
- **Process Modeling Workflow**: [/.team/prompts/PROCESS_MODELING_WORKFLOW.md](../../.team/prompts/PROCESS_MODELING_WORKFLOW.md)
- **Self-Improvement Loop**: [copilot-instructions.md](../copilot-instructions.md#self-improvement-loop)
- **Migration History**: [WORKFLOW-FEEDBACK-MIGRATION.md](../WORKFLOW-FEEDBACK-MIGRATION.md)

---

**Key Takeaway**: The `[Workflow Feedback] Tracker` is a critical infrastructure issue with a fixed title that enables continuous workflow improvement. Always recreate it with the exact same title if deleted.
