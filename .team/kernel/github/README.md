# GitHub Issues Driver

**Platform**: GitHub Issues  
**Version**: 1.0  
**Created**: 2025-11-12  
**Status**: ✅ Implemented

---

## Overview

This driver provides platform-specific implementations of all semantic operations for GitHub Issues using the GitHub MCP (Model Context Protocol) tools.

**Platform Features Used**:
- GitHub Issues API
- Issue labels for duty assignment (`workflow:*` pattern)
- Issue parent/child relationships for multi-phase work
- Issue comments for feedback and communication

---

## Semantic Operations Implementation

This driver implements all 12 semantic operations:

### Work Item CRUD
- `create_work_item` → `issue_write(method="create")`
- `get_work_item_details` → `issue_read(method="get")`
- `update_work_item` → `issue_write(method="update")`
- `add_work_item_comment` → `add_issue_comment()`

### Duty Management
- `get_work_item_duty` → Extract from `workflow:*` labels
- `assign_work_item_to_duty` → Update labels
- `query_work_items_by_duty` → `list_issues()` with label filter

### Multi-Phase Support
- `create_child_work_item` → `sub_issue_write(method="add")`
- `get_parent_work_item` → `issue_read(method="get")` parent lookup
- `is_multi_phase` → Check for parent/children
- `list_child_work_items` → `issue_read(method="get_sub_issues")`

### Feedback
- `submit_feedback` → `add_issue_comment()` on tracker issue

**Complete Mappings**: See [operations.md](operations.md)

---

## Platform-Specific Considerations

### Duty Labels

**Format**: `workflow:{duty_name}`

**Examples**:
- `workflow:research` - Research duty
- `workflow:implementation` - Implementation duty
- `workflow:triage` - Triage duty
- etc.

**Important**: GitHub Issues allows multiple labels, but our semantic model requires exactly ONE duty label per work item.

### Multi-Phase Relationships

GitHub supports parent/child issue relationships via the sub-issues feature:
- Parent issues can have multiple sub-issues
- Sub-issues are linked to exactly one parent
- Sub-issues can be queried via `issue_read(method="get_sub_issues")`

### Feedback Tracker

Feedback is submitted as comments on a special tracker issue:
- **Title**: `[Workflow Feedback] Tracker` (exact match required)
- **Search**: Use `search_issues()` to find tracker
- **Create**: Auto-create if not found
- **Format**: Structured comment with feedback template

### Authentication

GitHub MCP tools handle authentication automatically using the repository context.

**Configuration**:
```yaml
github:
  owner: uniun-technology
  repo: lib-dataflow
```

---

## Error Handling

### Common Errors

**Work Item Not Found**:
```python
# MCP tool will raise error if issue doesn't exist
# Kernel should catch and return None or appropriate error
```

**Multiple Duty Labels**:
```python
# If work item has multiple workflow:* labels, kernel should:
# 1. Log warning
# 2. Return first matching label
# 3. Note in documentation that cleanup is needed
```

**Feedback Tracker Not Found**:
```python
# If tracker issue doesn't exist:
# 1. Auto-create using issue_write()
# 2. Use standard template
# 3. Add initial feedback comment
```

---

## Performance Considerations

### Label Queries

GitHub's label filtering is efficient:
```python
list_issues(labels=["workflow:research"], state="OPEN")
# Fast: GitHub indexes labels
```

### Parent/Child Queries

Querying sub-issues requires additional API calls:
```python
# First get parent issue
parent = issue_read(issue_number=100, method="get")

# Then get sub-issues
children = issue_read(issue_number=100, method="get_sub_issues")
# Requires 2 API calls
```

**Optimization**: Cache parent/child relationships when processing multi-phase work.

---

## Usage Examples

**See**: [examples.md](examples.md) for complete usage examples of all operations.

---

## Testing

**Test Location**: `../tests/github/`

**Test Approach**: Semantic contract tests verify that GitHub implementations correctly map to semantic operations.

---

## Migration Notes

### From Direct MCP Tool Usage

**Before** (in workflows - platform-specific):
```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:research"],
    state="OPEN"
)
```

**After** (in procedures/duties - platform-agnostic):
```python
query_work_items_by_duty(
    duty="research",
    status="open"
)
```

The kernel handles the translation transparently.

---

## Related Documentation

- [Kernel Overview](../README.md)
- [Semantic Language Specification](../../../docs/design/prompt-engineering/semantic-language.md)
- [Operation Mappings](operations.md)
- [Usage Examples](examples.md)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial GitHub driver documentation |
