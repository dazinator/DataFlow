# GitHub Driver: Semantic Operation Mappings

**Platform**: GitHub Issues  
**Version**: 1.0  
**Created**: 2025-11-12

This document provides detailed mappings from semantic operations to GitHub MCP tool implementations.

---

## Work Item CRUD Operations

### 1. create_work_item

**Semantic Signature**:
```python
create_work_item(
    type: str,              # Work item type
    title: str,             # Title
    description: str,       # Description
    duty: str,              # Duty assignment
    labels: list[str] = [], # Additional labels
    assignee: str = None    # Optional assignee
) -> str  # Returns work_item_id
```

**GitHub Implementation**:
```python
def create_work_item(type, title, description, duty, labels=[], assignee=None):
    """
    Maps to GitHub issue_write(method="create")
    """
    # Build label list
    github_labels = [f"workflow:{duty}"]  # Duty label
    
    if type:
        github_labels.append(type)  # Type label
    
    github_labels.extend(labels)  # Additional labels
    
    # Create issue
    result = issue_write(
        method="create",
        owner="uniun-technology",
        repo="lib-dataflow",
        title=title,
        body=description,
        labels=github_labels,
        assignees=[assignee] if assignee else []
    )
    
    # Return work item ID (issue number as string)
    return str(result.number)
```

**Notes**:
- Duty mapped to `workflow:{duty}` label
- Type becomes additional label
- Issue number returned as work item ID

---

### 2. get_work_item_details

**Semantic Signature**:
```python
get_work_item_details(
    work_item_id: str
) -> dict
```

**GitHub Implementation**:
```python
def get_work_item_details(work_item_id):
    """
    Maps to GitHub issue_read(method="get")
    """
    issue_number = int(work_item_id)
    
    result = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number
    )
    
    # Extract duty from labels
    duty = None
    other_labels = []
    for label in result.labels:
        if label['name'].startswith('workflow:'):
            duty = label['name'].replace('workflow:', '')
        else:
            other_labels.append(label['name'])
    
    # Return platform-agnostic format
    return {
        "id": str(result.number),
        "title": result.title,
        "description": result.body or "",
        "status": "open" if result.state == "open" else "closed",
        "duty": duty,
        "type": other_labels[0] if other_labels else None,
        "labels": other_labels,
        "assignee": result.assignee.login if result.assignee else None,
        "parent_id": None,  # Would need parent lookup
        "created_at": result.created_at,
        "updated_at": result.updated_at
    }
```

**Notes**:
- Issue number converted from string ID
- Labels parsed to extract duty
- State mapped to status

---

### 3. update_work_item

**Semantic Signature**:
```python
update_work_item(
    work_item_id: str,
    title: str = None,
    description: str = None,
    status: str = None,
    labels: list[str] = None,
    assignee: str = None
) -> None
```

**GitHub Implementation**:
```python
def update_work_item(work_item_id, title=None, description=None, 
                     status=None, labels=None, assignee=None):
    """
    Maps to GitHub issue_write(method="update")
    """
    issue_number = int(work_item_id)
    
    # Build update parameters
    params = {
        "method": "update",
        "owner": "uniun-technology",
        "repo": "lib-dataflow",
        "issue_number": issue_number
    }
    
    if title is not None:
        params["title"] = title
    
    if description is not None:
        params["body"] = description
    
    if status is not None:
        # Map semantic status to GitHub state
        if status == "open":
            params["state"] = "open"
        elif status == "closed":
            params["state"] = "closed"
    
    if labels is not None:
        params["labels"] = labels
    
    if assignee is not None:
        params["assignees"] = [assignee]
    
    issue_write(**params)
```

**Notes**:
- Only provided parameters are updated
- Status mapped to GitHub state
- Labels replace entire label set if provided

---

### 4. add_work_item_comment

**Semantic Signature**:
```python
add_work_item_comment(
    work_item_id: str,
    text: str
) -> None
```

**GitHub Implementation**:
```python
def add_work_item_comment(work_item_id, text):
    """
    Maps to GitHub add_issue_comment()
    """
    issue_number = int(work_item_id)
    
    add_issue_comment(
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number,
        body=text
    )
```

**Notes**:
- Direct mapping to add_issue_comment
- Markdown supported in text

---

## Duty Operations

### 5. get_work_item_duty

**Semantic Signature**:
```python
get_work_item_duty(
    work_item_id: str
) -> str  # Duty name or None
```

**GitHub Implementation**:
```python
def get_work_item_duty(work_item_id):
    """
    Extract duty from workflow:* label
    """
    issue_number = int(work_item_id)
    
    result = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number
    )
    
    # Find workflow:* label
    for label in result.labels:
        if label['name'].startswith('workflow:'):
            return label['name'].replace('workflow:', '')
    
    # No duty assigned
    return None
```

**Notes**:
- Returns None if no workflow:* label found
- Returns first match if multiple (shouldn't happen)

---

### 6. assign_work_item_to_duty

**Semantic Signature**:
```python
assign_work_item_to_duty(
    work_item_id: str,
    duty: str
) -> None
```

**GitHub Implementation**:
```python
def assign_work_item_to_duty(work_item_id, duty):
    """
    Update workflow:* label
    """
    issue_number = int(work_item_id)
    
    # Get current labels
    result = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number
    )
    
    # Remove old workflow:* labels, keep others
    new_labels = []
    for label in result.labels:
        if not label['name'].startswith('workflow:'):
            new_labels.append(label['name'])
    
    # Add new duty label
    new_labels.append(f"workflow:{duty}")
    
    # Update issue
    issue_write(
        method="update",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number,
        labels=new_labels
    )
```

**Notes**:
- Removes all workflow:* labels first
- Adds new workflow:{duty} label
- Preserves other labels

---

### 7. query_work_items_by_duty

**Semantic Signature**:
```python
query_work_items_by_duty(
    duty: str,
    status: str = "open",
    limit: int = 100
) -> list[dict]
```

**GitHub Implementation**:
```python
def query_work_items_by_duty(duty, status="open", limit=100):
    """
    Maps to GitHub list_issues() with label filter
    """
    # Map semantic status to GitHub state
    github_state = "OPEN" if status == "open" else "CLOSED"
    
    results = list_issues(
        owner="uniun-technology",
        repo="lib-dataflow",
        labels=[f"workflow:{duty}"],
        state=github_state,
        perPage=min(limit, 100)
    )
    
    # Convert to platform-agnostic format
    work_items = []
    for issue in results:
        work_items.append({
            "id": str(issue.number),
            "title": issue.title,
            "status": "open" if issue.state == "open" else "closed",
            "duty": duty,
            "created_at": issue.created_at
        })
    
    return work_items
```

**Notes**:
- Filters by workflow:{duty} label
- Maps status to GitHub state
- Returns simplified summaries

---

### 8. query_unlabeled_work_items

**Semantic Signature**:
```python
query_unlabeled_work_items(
    status: str = "open",
    limit: int = 100
) -> list[dict]
```

**GitHub Implementation**:
```python
def query_unlabeled_work_items(status="open", limit=100):
    """
    Maps to GitHub list_issues() filtering for issues WITHOUT workflow:* labels
    
    This finds work items that need initial triage - they haven't been
    assigned to any duty yet.
    """
    # Map semantic status to GitHub state
    github_state = "OPEN" if status == "open" else "CLOSED"
    
    # Get all issues without filtering by label first
    all_issues = list_issues(
        owner="uniun-technology",
        repo="lib-dataflow",
        state=github_state,
        perPage=min(limit, 100)
    )
    
    # Filter out issues that have any workflow:* label
    unlabeled_items = []
    for issue in all_issues:
        has_workflow_label = False
        for label in issue.labels:
            if label['name'].startswith('workflow:'):
                has_workflow_label = True
                break
        
        # Only include issues without workflow labels
        if not has_workflow_label:
            unlabeled_items.append({
                "id": str(issue.number),
                "title": issue.title,
                "status": "open" if issue.state == "open" else "closed",
                "duty": None,  # No duty assigned
                "created_at": issue.created_at
            })
    
    return unlabeled_items
```

**Notes**:
- Returns issues WITHOUT any `workflow:*` labels
- These are items needing initial triage
- Returns `duty: None` to indicate no duty assigned
- Complements `query_work_items_by_duty` for complete triage coverage

**Alternative Implementation (More Efficient)**:

For large repositories, use search API with negative label filter:
```python
def query_unlabeled_work_items_efficient(status="open", limit=100):
    """
    More efficient implementation using GitHub search API
    """
    # Build search query for issues without workflow labels
    # Note: GitHub search doesn't support "NOT label:x", so we filter client-side
    # or use repo-specific label enumeration
    
    results = search_issues(
        owner="uniun-technology",
        repo="lib-dataflow",
        query=f"is:issue is:{status} repo:uniun-technology/lib-dataflow",
        perPage=min(limit, 100)
    )
    
    # Filter out any with workflow:* labels
    unlabeled_items = []
    for issue in results['items']:
        has_workflow_label = any(
            label['name'].startswith('workflow:') 
            for label in issue.labels
        )
        
        if not has_workflow_label:
            unlabeled_items.append({
                "id": str(issue.number),
                "title": issue.title,
                "status": "open" if issue.state == "open" else "closed",
                "duty": None,
                "created_at": issue.created_at
            })
    
    return unlabeled_items
```

---

## Multi-Phase Operations

### 9. create_child_work_item

**Semantic Signature**:
```python
create_child_work_item(
    parent_id: str,
    type: str,
    title: str,
    description: str,
    duty: str,
    labels: list[str] = []
) -> str
```

**GitHub Implementation**:
```python
def create_child_work_item(parent_id, type, title, description, duty, labels=[]):
    """
    Maps to GitHub sub_issue_write(method="add")
    """
    parent_number = int(parent_id)
    
    # Build label list
    github_labels = [f"workflow:{duty}"]
    if type:
        github_labels.append(type)
    github_labels.extend(labels)
    
    # Create child issue first
    child_result = issue_write(
        method="create",
        owner="uniun-technology",
        repo="lib-dataflow",
        title=title,
        body=description,
        labels=github_labels
    )
    
    child_number = child_result.number
    
    # Link to parent
    sub_issue_write(
        method="add",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=parent_number,
        sub_issue_id=child_result.id  # Note: uses ID, not number
    )
    
    return str(child_number)
```

**Notes**:
- Creates issue first, then links to parent
- Uses sub-issue ID (not number) for linking
- Returns child issue number

---

### 10. get_parent_work_item

**Semantic Signature**:
```python
get_parent_work_item(
    work_item_id: str
) -> str  # Parent ID or None
```

**GitHub Implementation**:
```python
def get_parent_work_item(work_item_id):
    """
    Query parent from sub-issue relationship
    """
    issue_number = int(work_item_id)
    
    # Note: GitHub doesn't provide direct parent lookup
    # Would need to search issues with get_sub_issues to find parent
    # For now, this is a limitation of GitHub's API
    
    # Workaround: Store parent reference in issue description or labels
    # Or search all issues that might be parents (expensive)
    
    # TODO: Implement parent lookup strategy
    return None
```

**Notes**:
- **Limitation**: GitHub doesn't provide reverse parent lookup
- Possible workarounds:
  - Search pattern in description
  - Custom label with parent ID
  - Cache parent/child relationships
- Current implementation returns None (needs enhancement)

---

### 11. is_multi_phase

**Semantic Signature**:
```python
is_multi_phase(
    work_item_id: str
) -> bool
```

**GitHub Implementation**:
```python
def is_multi_phase(work_item_id):
    """
    Check if issue has parent or children
    """
    issue_number = int(work_item_id)
    
    # Check for children
    children = issue_read(
        method="get_sub_issues",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue_number
    )
    
    if len(children) > 0:
        return True
    
    # Check for parent (using workaround)
    parent = get_parent_work_item(work_item_id)
    if parent is not None:
        return True
    
    return False
```

**Notes**:
- Checks for children first (reliable)
- Parent check limited by get_parent_work_item implementation

---

### 12. list_child_work_items

**Semantic Signature**:
```python
list_child_work_items(
    parent_id: str
) -> list[dict]
```

**GitHub Implementation**:
```python
def list_child_work_items(parent_id):
    """
    Maps to GitHub issue_read(method="get_sub_issues")
    """
    parent_number = int(parent_id)
    
    children = issue_read(
        method="get_sub_issues",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=parent_number
    )
    
    # Convert to platform-agnostic format
    work_items = []
    for child in children:
        # Extract duty from labels
        duty = None
        for label in child.labels:
            if label['name'].startswith('workflow:'):
                duty = label['name'].replace('workflow:', '')
                break
        
        work_items.append({
            "id": str(child.number),
            "title": child.title,
            "status": "open" if child.state == "open" else "closed",
            "duty": duty
        })
    
    return work_items
```

**Notes**:
- Direct mapping to get_sub_issues
- Returns simplified child summaries

---

## Feedback Operations

### 13. submit_feedback

**Semantic Signature**:
```python
submit_feedback(
    work_item_id: str,
    duty: str,
    what_worked: str,
    what_didnt_work: str,
    suggestions: str
) -> None
```

**GitHub Implementation**:
```python
def submit_feedback(work_item_id, duty, what_worked, what_didnt_work, suggestions):
    """
    Add comment to feedback tracker issue
    """
    from datetime import datetime
    
    # Find feedback tracker
    tracker_title = "[Workflow Feedback] Tracker"
    
    search_results = search_issues(
        owner="uniun-technology",
        repo="lib-dataflow",
        query=f'"{tracker_title}" in:title state:open'
    )
    
    if not search_results or len(search_results) == 0:
        # Create tracker if not found
        tracker = issue_write(
            method="create",
            owner="uniun-technology",
            repo="lib-dataflow",
            title=tracker_title,
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
"""
        )
        tracker_number = tracker.number
    else:
        tracker_number = search_results[0].number
    
    # Format feedback comment
    feedback_comment = f"""## Workflow Feedback Entry

**Date**: {datetime.now().strftime("%Y-%m-%d")}
**Issue/PR**: #{work_item_id}
**Workflow**: {duty}

### What Worked Well
{what_worked}

### What Didn't Work Well
{what_didnt_work}

### Suggested Improvement
{suggestions}
"""
    
    # Add comment
    add_issue_comment(
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=tracker_number,
        body=feedback_comment
    )
```

**Notes**:
- Finds or creates feedback tracker issue
- Uses exact title match: `[Workflow Feedback] Tracker`
- Formats feedback as structured comment
- Auto-creates tracker with template if missing

---

## Configuration Reference

All implementations use configuration from `../config.yaml`:

```yaml
github:
  owner: uniun-technology
  repo: lib-dataflow
  duty_label_prefix: "workflow:"
  feedback_tracker_title: "[Workflow Feedback] Tracker"
```

---

## Error Handling

### Common Patterns

**Issue Not Found**:
```python
try:
    result = issue_read(...)
except Exception as e:
    # Handle: log error, return None, or raise semantic error
    return None
```

**Multiple Duty Labels** (data quality issue):
```python
workflow_labels = [l for l in labels if l.startswith('workflow:')]
if len(workflow_labels) > 1:
    # Log warning about multiple duties
    # Return first one, but flag for cleanup
    return workflow_labels[0].replace('workflow:', '')
```

**API Rate Limits**:
```python
# GitHub MCP tools handle rate limiting
# But kernel should be aware and potentially cache results
```

---

## Related Documentation

- [Kernel Overview](../README.md)
- [GitHub Driver README](README.md)
- [Usage Examples](examples.md)
- [Semantic Language Reference](../../../docs/design/prompt-engineering/semantic-language.md)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial operation mappings for all 12 semantic operations |
