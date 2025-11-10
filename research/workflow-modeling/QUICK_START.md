# Workflow Feedback Migration - Quick Start

[Copilot-Workflow: Process Modeling]

## What Changed?

Workflow feedback moved from `.github/workflow-improvements.md` file to GitHub Issues for better tracking.

## New System (For Workflow Agents)

When completing work, instead of editing a markdown file:

### 1. Find Parent Feedback Issue

Search for: `[Workflow Feedback] Tracker`

Example:
```python
parent = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query="[Workflow Feedback] Tracker in:title state:open"
)[0]
```

### 2. Create Feedback Issue

```python
child = issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="Brief description of improvement",
    labels=["workflow:process-modeling"],
    body="""
## Workflow Feedback Entry

**Date**: 2025-11-10
**Issue/PR**: #245
**Workflow**: Research Workflow (or "Multiple" if spans workflows)

### What Worked Well
- Research phases were clear
- Documentation structure worked well

### What Didn't Work Well
- Unclear when to create prototype vs full implementation

### Suggested Improvement
Add prototyping scope guidance with decision tree:
- Minimal POC for feasibility
- Working prototype for performance
- Production-ready for adoption
"""
)
```

### 3. Link to Parent

```python
sub_issue_write(
    method="add",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent.number,
    sub_issue_id=child.id
)
```

## New System (For Process Modeling)

Instead of reading markdown file:

```python
# Query open feedback
parent = search_issues(
    query="[Workflow Feedback] Tracker in:title state:open"
)[0]

# Get open children
parent_data = issue_read(
    method="get_sub_issues",
    issue_number=parent.number
)

open_feedback = [c for c in parent_data.children if c.state == "open"]

# Process feedback (standard process modeling)
# ...

# When done, close the issue
issue_write(
    method="update",
    issue_number=feedback.number,
    state="closed"
)

add_issue_comment(
    issue_number=feedback.number,
    body="✅ Implemented in PR #XXX"
)
```

## Benefits

- **82% faster** (2 min vs 11 min)
- **More visible** (issue tracker vs hidden file)
- **Better tracking** (open/closed vs ✅ markers)
- **Searchable** (GitHub search)
- **No conflicts** (concurrent work possible)

## Implementation Status

- ✅ Design validated (all scenarios PASS)
- ⏳ Parent issue creation (pending)
- ⏳ Migration of historical entries (pending)
- ⏳ Workflow documentation updates (pending)

See `/research/workflow-modeling/IMPLEMENTATION_GUIDE.md` for complete details.
