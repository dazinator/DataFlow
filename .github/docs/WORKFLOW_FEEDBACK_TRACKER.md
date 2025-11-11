# Workflow Feedback Tracker

## Overview

The **Workflow Feedback Tracker** is a GitHub issue that consolidates all workflow improvement suggestions and feedback from Copilot agents. It serves as the central hub for continuous workflow improvement.

**Issue Title**: `[Workflow Feedback] Tracker` (exact - workflows search by this title)

**Current Issue**: Search for it using: `"[Workflow Feedback] Tracker" in:title state:open`

## Purpose

The feedback tracker enables:

1. **Continuous Improvement**: Agents provide feedback after completing work, which feeds into process modeling improvements
2. **Centralized Tracking**: All workflow feedback is tracked as comments on this single issue
3. **Simple Processing**: Process Modeling workflow reads comments from this issue to process feedback
4. **Self-Improvement Loop**: Ensures workflows continuously evolve based on real agent experiences

## Key Characteristics

### Fixed Title Convention

- **Title**: `[Workflow Feedback] Tracker` (exact - do not change)
- **Why**: Workflows use title-based search to find this issue dynamically
- **Resilience**: If deleted, recreate with the exact same title - workflows will find it automatically

### Organizational Structure

```
[Workflow Feedback] Tracker (Issue #254 or similar)
└── Comments containing feedback entries
    ├── Feedback Entry #1 (comment)
    ├── Feedback Entry #2 (comment)
    ├── Feedback Entry #3 (comment)
    └── ...
```

- **Parent**: The tracker issue itself
- **Feedback**: Individual feedback entries as comments
- **State**: Comments with ✅ prefix = addressed; Comments without prefix = pending

## How It Works

### For Workflow Agents

When completing work on any issue:

1. **Evaluate**: Reflect on what worked well and what didn't
2. **Find**: Find the tracker issue by title
3. **Comment**: Add a feedback comment to the tracker issue

**Example**:
```python
from datetime import datetime

# Find tracker by title (create if not found)
tracker_results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

if not tracker_results or len(tracker_results) == 0:
    # Create new tracker if none exists
    tracker = issue_write(
        method="create",
        owner="uniun-technology",
        repo="lib-dataflow",
        title="[Workflow Feedback] Tracker",
        labels=["workflow:process-modeling"],
        body="""# Workflow Feedback Tracker

This issue tracks feedback and improvement suggestions for all workflows.

**⚠️ IMPORTANT**: Feedback is submitted as **comments on this issue**, not as sub-issues.

## How to Submit Feedback

When completing work on an issue:

1. **Find this tracker issue**: Search for `[Workflow Feedback] Tracker`
2. **Add a comment** with your feedback using the template below

### Feedback Comment Template

\```markdown
## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #XXX or branch-name
**Workflow**: [Workflow Name]

### What Worked Well
[List specific positives]

### What Didn't Work Well
[List specific issues]

### Suggested Improvement
[Specific, actionable improvements]
\```

## For Process Modeling Workflow

When assigned to address feedback:

1. Read through recent feedback comments on this issue
2. Group related feedback
3. Address improvements using standard Process Modeling workflow
4. Mark feedback as addressed by adding a reply comment
"""
    )
    tracker_number = tracker.number
else:
    tracker_number = tracker_results[0].number

# Add feedback as a comment
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_number,
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
```

### For Process Modeling Workflow

Process Modeling workflow is assigned directly to the tracker issue when feedback needs to be addressed:

**How it works:**

1. **Feedback accumulates** as comments on the tracker issue
2. **Human reviewer** assigns @copilot to the tracker issue when ready to process feedback
3. **Human reviewer** adds a comment requesting processing: "Please review and address the pending feedback comments"
4. **Process Modeling** reads all comments, identifies unaddressed feedback, and processes them

**Processing feedback:**
```python
# Find the feedback tracker issue by title
tracker_results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

if not tracker_results or len(tracker_results) == 0:
    # No tracker found - this shouldn't happen in normal operation
    # but could occur if tracker was closed
    raise ValueError("No open feedback tracker found. Tracker may have been closed.")

tracker_issue = tracker_results[0]

# Get all comments from the tracker issue
comments = issue_read(
    method="get_comments",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_issue.number
)

# Filter to feedback entries (those starting with "## Workflow Feedback Entry")
# and not yet addressed (no subsequent comment starting with "✅")
feedback_comments = [
    c for idx, c in enumerate(comments)
    if "## Workflow Feedback Entry" in c.body
    and not any(
        later_c.body.startswith("✅") for later_c in comments[idx+1:]
    )
]
```

**Marking feedback as addressed:**
After implementing a suggestion, add a reply comment:
```python
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_issue.number,
    body=f"""✅ **Addressed** - Feedback from {original_comment_date}

**What was implemented**: [Description]

**Changes made**:
- [List changes]

**PR**: #{PR_NUMBER}

Thank you for the feedback!
"""
)
```

## If Deleted or Recreated

**Resilience**: The system uses title-based lookup, not issue numbers.

If the tracker issue is deleted or closed:

1. **Agents automatically create new tracker** when adding feedback (if no open tracker found)
2. **Use exact title**: `[Workflow Feedback] Tracker`
3. **Add `workflow:process-modeling` label**
4. **No code changes needed**: Workflows will find it automatically by title

**Why this works**: All workflow code uses `search_issues()` with the title query and creates a new tracker if none found. This allows trackers to be closed when feedback is fully processed, and new ones to be created automatically when new feedback arrives.

**Closing Trackers**: When all feedback in a tracker has been addressed, the tracker can be closed. The next feedback submission will automatically create a new tracker issue.

## Workflow Integration

### Workflows That Create Feedback

All workflows add feedback comments after completing work:

- **Research Workflow**: After completing research and creating handover
- **Implementation Workflow**: After implementing features/fixes
- **Tech Debt Workflow**: After discovering/analyzing tech debt
- **Product Prioritization Workflow**: After prioritizing backlog items
- **Process Modeling Workflow**: After improving workflows
- **Triage Workflow**: After triaging issues

See: [Self-Improvement Loop in copilot-instructions.md](../copilot-instructions.md#self-improvement-loop)

### Workflows That Process Feedback

- **Process Modeling Workflow**: Processes feedback when assigned to tracker issue by human reviewer

## Required Setup

When setting up a new repository:

1. **Create the tracker issue** with exact title `[Workflow Feedback] Tracker`
2. **Add description** explaining the purpose
3. **Leave issue open** permanently to collect feedback

See complete setup guide: [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md#5-create-required-parent-issues)

## References

- **Setup Guide**: [COPILOT_GITHUB_SETUP.md](./COPILOT_GITHUB_SETUP.md)
- **Process Modeling Workflow**: [/.team/prompts/PROCESS_MODELING_WORKFLOW.md](../../.team/prompts/PROCESS_MODELING_WORKFLOW.md)
- **Self-Improvement Loop**: [copilot-instructions.md](../copilot-instructions.md#self-improvement-loop)
- **Migration History**: [WORKFLOW-FEEDBACK-MIGRATION.md](../WORKFLOW-FEEDBACK-MIGRATION.md)

---

**Key Takeaway**: The `[Workflow Feedback] Tracker` is a critical infrastructure issue with a fixed title that enables continuous workflow improvement through comment-based feedback. Always recreate it with the exact same title if deleted.
