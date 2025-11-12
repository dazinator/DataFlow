# Semantic Language Reference

**Purpose**: Define all semantic operations that procedures use to interact with work trackers

**Related**: [Main Design Document](README.md) | [Core Concepts](concepts.md)

---

## Overview

The **semantic language** provides a platform-agnostic interface for work item operations. Procedures use these semantic operations instead of calling platform APIs directly.

**Key Principle**: Semantic operations define **what** to do, kernel implementations define **how** on each platform.

---

## Operation Categories

### Work Item CRUD Operations

#### create_work_item

**Purpose**: Create a new work item

**Signature**:
```python
create_work_item(
    type: str,              # Work item type: "research", "implementation", "bug", etc.
    title: str,             # Work item title
    description: str,       # Work item description/body
    duty: str,              # Duty assignment: "research", "implementation", etc.
    labels: list[str] = [], # Additional platform-agnostic labels
    assignee: str = None    # Optional assignee username
) -> str  # Returns work_item_id
```

**Platform Implementations**:
- **GitHub**: Maps to `issue_write(method="create", labels=[f"workflow:{duty}"])`
- **Azure DevOps**: Maps to `POST /wit/workitems` with `tags=[f"duty:{duty}"]`

**Example Usage** (in procedure):
```python
# Create research work item
work_item_id = create_work_item(
    type="research",
    title="Investigate caching strategy",
    description="Evaluate Redis vs in-memory caching...",
    duty="research",
    labels=["performance"]
)
```

---

#### get_work_item_details

**Purpose**: Retrieve full details of a work item

**Signature**:
```python
get_work_item_details(
    work_item_id: str  # Work item identifier
) -> dict  # Work item details including title, description, status, duty, etc.
```

**Platform Implementations**:
- **GitHub**: Maps to `issue_read(method="get")`
- **Azure DevOps**: Maps to `GET /wit/workitems/{id}`

**Returned Fields** (platform-agnostic):
```python
{
    "id": str,              # Work item ID
    "title": str,           # Title
    "description": str,     # Description/body
    "status": str,          # "open", "closed", "in_progress", etc.
    "duty": str,            # Current duty assignment
    "type": str,            # Work item type
    "labels": list[str],    # Additional labels/tags
    "assignee": str,        # Assigned username (if any)
    "parent_id": str,       # Parent work item ID (if any)
    "created_at": str,      # ISO 8601 timestamp
    "updated_at": str       # ISO 8601 timestamp
}
```

**Example Usage**:
```python
details = get_work_item_details(work_item_id="123")
print(f"Working on: {details['title']}")
print(f"Assigned to duty: {details['duty']}")
```

---

#### update_work_item

**Purpose**: Update work item fields

**Signature**:
```python
update_work_item(
    work_item_id: str,      # Work item to update
    title: str = None,      # New title (optional)
    description: str = None, # New description (optional)
    status: str = None,     # New status: "open", "closed", etc. (optional)
    labels: list[str] = None, # Replace labels (optional)
    assignee: str = None    # New assignee (optional)
) -> None
```

**Platform Implementations**:
- **GitHub**: Maps to `issue_write(method="update")`
- **Azure DevOps**: Maps to `PATCH /wit/workitems/{id}`

**Example Usage**:
```python
# Update work item title and status
update_work_item(
    work_item_id="123",
    title="[Updated] Investigate caching strategy",
    status="in_progress"
)
```

---

#### add_work_item_comment

**Purpose**: Add a comment to a work item

**Signature**:
```python
add_work_item_comment(
    work_item_id: str,  # Work item to comment on
    text: str            # Comment text (markdown supported)
) -> None
```

**Platform Implementations**:
- **GitHub**: Maps to `add_issue_comment()`
- **Azure DevOps**: Maps to `POST /wit/workitems/{id}/comments`

**Example Usage**:
```python
add_work_item_comment(
    work_item_id="123",
    text="Research findings documented in `/research/caching/`"
)
```

---

### Duty Operations

#### get_work_item_duty

**Purpose**: Extract duty designation from work item

**Signature**:
```python
get_work_item_duty(
    work_item_id: str  # Work item ID
) -> str  # Duty name: "research", "implementation", etc., or None if unassigned
```

**Platform Implementations**:
- **GitHub**: Extracts from labels matching `workflow:*` pattern
- **Azure DevOps**: Extracts from tags matching `duty:*` pattern

**Example Usage**:
```python
duty = get_work_item_duty(work_item_id="123")
if duty == "research":
    # Execute research duty
elif duty is None:
    # Execute unassigned duty
```

---

#### assign_work_item_to_duty

**Purpose**: Change duty assignment for a work item

**Signature**:
```python
assign_work_item_to_duty(
    work_item_id: str,  # Work item to reassign
    duty: str            # New duty: "research", "implementation", etc.
) -> None
```

**Platform Implementations**:
- **GitHub**: Updates labels, removes old `workflow:*`, adds new `workflow:{duty}`
- **Azure DevOps**: Updates tags, removes old `duty:*`, adds new `duty:{duty}`

**Example Usage**:
```python
# Handover from research to implementation
assign_work_item_to_duty(
    work_item_id="123",
    duty="implementation"
)
```

---

#### query_work_items_by_duty

**Purpose**: Get all work items assigned to a specific duty

**Signature**:
```python
query_work_items_by_duty(
    duty: str,                  # Duty to query: "research", "implementation", etc.
    status: str = "open",       # Filter by status (optional)
    limit: int = 100            # Maximum results (optional)
) -> list[dict]  # List of work item summaries
```

**Platform Implementations**:
- **GitHub**: Maps to `list_issues(labels=[f"workflow:{duty}"], state=status)`
- **Azure DevOps**: Query work items by `duty:{duty}` tag

**Returned Format**:
```python
[
    {
        "id": str,
        "title": str,
        "status": str,
        "duty": str,
        "created_at": str
    },
    ...
]
```

**Example Usage**:
```python
# Get all open research work items
research_items = query_work_items_by_duty(
    duty="research",
    status="open"
)

for item in research_items:
    print(f"- {item['title']} (#{item['id']})")
```

---

#### query_unlabeled_work_items

**Purpose**: Get all work items without any workflow duty labels (need initial triage)

**Signature**:
```python
query_unlabeled_work_items(
    status: str = "open",       # Filter by status (optional)
    limit: int = 100            # Maximum results (optional)
) -> list[dict]  # List of work item summaries
```

**Platform Implementations**:
- **GitHub**: Maps to `list_issues()` filtering for issues WITHOUT any `workflow:*` labels
- **Azure DevOps**: Query work items WITHOUT any `duty:*` tags

**Returned Format**:
```python
[
    {
        "id": str,
        "title": str,
        "status": str,
        "duty": None,  # Always None for unlabeled items
        "created_at": str
    },
    ...
]
```

**Example Usage**:
```python
# Get all open work items without workflow labels
unlabeled_items = query_unlabeled_work_items(status="open")

for item in unlabeled_items:
    print(f"Unlabeled: {item['title']} (#{item['id']})")
```

**Use Case**: Bulk triage operations need to find both:
1. Work items explicitly assigned to triage (`workflow:triage` label)
2. New work items without any workflow labels yet (need initial triage)

---

### Multi-Phase Operations

#### create_child_work_item

**Purpose**: Create a child work item linked to a parent

**Signature**:
```python
create_child_work_item(
    parent_id: str,         # Parent work item ID
    type: str,              # Child work item type
    title: str,             # Child title
    description: str,       # Child description
    duty: str,              # Duty assignment for child
    labels: list[str] = []  # Additional labels
) -> str  # Returns child work_item_id
```

**Platform Implementations**:
- **GitHub**: Creates issue with parent relationship
- **Azure DevOps**: Creates work item with parent link

**Example Usage**:
```python
# Create sub-task for multi-phase work
child_id = create_child_work_item(
    parent_id="100",
    type="task",
    title="Phase 1: Foundation",
    description="Create kernel structure...",
    duty="implementation"
)
```

---

#### get_parent_work_item

**Purpose**: Get parent work item if one exists

**Signature**:
```python
get_parent_work_item(
    work_item_id: str  # Child work item ID
) -> str  # Parent work item ID, or None if no parent
```

**Platform Implementations**:
- **GitHub**: Queries parent relationship
- **Azure DevOps**: Queries parent link

**Example Usage**:
```python
parent_id = get_parent_work_item(work_item_id="123")
if parent_id:
    parent_details = get_work_item_details(parent_id)
    print(f"Part of: {parent_details['title']}")
```

---

#### is_multi_phase

**Purpose**: Check if work item is part of a multi-phase plan

**Signature**:
```python
is_multi_phase(
    work_item_id: str  # Work item ID
) -> bool  # True if has parent or children
```

**Platform Implementations**:
- **GitHub**: Checks for parent/child relationships
- **Azure DevOps**: Checks for parent/child links

**Example Usage**:
```python
if is_multi_phase(work_item_id="123"):
    # Execute multi-phase procedure
    parent_id = get_parent_work_item(work_item_id="123")
    # Update parent progress...
```

---

#### list_child_work_items

**Purpose**: Get all children of a work item

**Signature**:
```python
list_child_work_items(
    parent_id: str  # Parent work item ID
) -> list[dict]  # List of child work item summaries
```

**Platform Implementations**:
- **GitHub**: Queries child relationships
- **Azure DevOps**: Queries child links

**Returned Format**:
```python
[
    {
        "id": str,
        "title": str,
        "status": str,
        "duty": str
    },
    ...
]
```

**Example Usage**:
```python
children = list_child_work_items(parent_id="100")
completed = [c for c in children if c['status'] == 'closed']
print(f"Progress: {len(completed)}/{len(children)} phases complete")
```

---

### Feedback Operations

#### submit_feedback

**Purpose**: Submit self-improvement feedback

**Signature**:
```python
submit_feedback(
    work_item_id: str,      # Current work item being completed
    duty: str,              # Duty that was executed
    what_worked: str,       # What worked well
    what_didnt_work: str,   # What didn't work well
    suggestions: str        # Improvement suggestions
) -> None
```

**Platform Implementations**:
- **GitHub**: Adds comment to Workflow Feedback Tracker issue
- **Azure DevOps**: Creates feedback work item or comment

**Example Usage**:
```python
submit_feedback(
    work_item_id="123",
    duty="research",
    what_worked="Research workflow provided clear structure",
    what_didnt_work="Unclear when to create ADR vs design doc",
    suggestions="Add decision tree for documentation type selection"
)
```

---

## Platform Configuration

### config.yaml Format

The kernel layer includes a configuration file that specifies the active platform:

```yaml
# .team/kernel/config.yaml

# Active platform
platform: github  # Options: "github", "azuredevops"

# GitHub-specific configuration
github:
  owner: uniun-technology
  repo: lib-dataflow
  duty_label_prefix: "workflow:"
  feedback_tracker_title: "[Workflow Feedback] Tracker"

# Azure DevOps configuration (when active)
azuredevops:
  organization: uniun-technology
  project: lib-dataflow
  duty_tag_prefix: "duty:"
  feedback_work_item_type: "Feedback"
```

---

## Implementation Notes for Kernel Developers

### Adding a New Semantic Operation

1. **Define signature** in this document
2. **Document purpose** and return values
3. **Implement for GitHub** in `.team/kernel/github/operations.md`
4. **Implement for Azure DevOps** in `.team/kernel/azuredevops/operations.md`
5. **Add examples** showing usage in procedures

### Backward Compatibility

When changing semantic operations:
- **Additive changes only** (new parameters with defaults)
- **Deprecation period** (mark old signature, provide migration path)
- **Version in config** (allow procedures to specify required version)

Example deprecation:
```python
# DEPRECATED: Use create_work_item instead
def create_issue(title, body, labels):
    """
    Deprecated in v2.0. Use create_work_item() instead.
    
    Migration:
        create_issue("title", "body", ["label"])
    becomes:
        create_work_item(type="task", title="title", description="body", 
                        duty="unassigned", labels=["label"])
    """
    warnings.warn("create_issue is deprecated, use create_work_item")
    return create_work_item(type="task", title=title, description=body,
                           duty="unassigned", labels=labels)
```

---

## Operation Summary Table

| Operation | Category | Purpose |
|-----------|----------|---------|
| `create_work_item` | CRUD | Create new work item |
| `get_work_item_details` | CRUD | Read work item |
| `update_work_item` | CRUD | Update work item fields |
| `add_work_item_comment` | CRUD | Add comment |
| `get_work_item_duty` | Duty | Extract duty designation |
| `assign_work_item_to_duty` | Duty | Change duty assignment |
| `query_work_items_by_duty` | Duty | Query work items by duty |
| `create_child_work_item` | Multi-Phase | Create child |
| `get_parent_work_item` | Multi-Phase | Get parent |
| `is_multi_phase` | Multi-Phase | Check if multi-phase |
| `list_child_work_items` | Multi-Phase | List children |
| `submit_feedback` | Feedback | Submit self-improvement feedback |

---

## Further Reading

- [Core Concepts](concepts.md) - Understanding layers and abstractions
- [Main Design Document](README.md) - Complete design
- [Kernel Implementation Guide](kernel-implementation.md) - How to implement a new platform driver
