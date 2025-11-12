# Test: create_work_item

**Semantic Operation**: `create_work_item`  
**Category**: Work Item CRUD  
**Version**: 1.0  
**Created**: 2025-11-12

---

## Semantic Operation Specification

**Signature**:
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

**Purpose**: Create a new work item assigned to a specific duty

**See**: [Semantic Language Reference](../../../../docs/design/prompt-engineering/semantic-language.md#create_work_item)

---

## Test Cases

### Test Case 1: Success - Create Research Work Item

**Input**:
```python
create_work_item(
    type="research",
    title="Investigate caching strategy",
    description="Evaluate Redis vs in-memory caching for DataFlow pipelines",
    duty="research",
    labels=["performance"]
)
```

**Expected Output**:
```python
work_item_id = "456"  # Returns work item ID as string
```

**Expected Behavior**:
- Work item created successfully
- Duty assigned to "research"
- Type label added
- Additional labels applied
- Work item is open/active
- Assignee is unassigned (None)

**Platform-Specific Expectations**:

**GitHub**:
```python
# Expected MCP tool call
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="Investigate caching strategy",
    body="Evaluate Redis vs in-memory caching for DataFlow pipelines",
    labels=["workflow:research", "research", "performance"],
    assignees=[]
)
# Returns: issue object with number=456
```

---

### Test Case 2: Success - Create with Assignee

**Input**:
```python
create_work_item(
    type="implementation",
    title="Add caching support",
    description="Implement Redis caching based on research",
    duty="implementation",
    labels=[],
    assignee="Copilot"
)
```

**Expected Output**:
```python
work_item_id = "457"
```

**Expected Behavior**:
- Work item created
- Assigned to "Copilot"
- Duty set to "implementation"

**GitHub**:
```python
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="Add caching support",
    body="Implement Redis caching based on research",
    labels=["workflow:implementation", "implementation"],
    assignees=["Copilot"]
)
```

---

### Test Case 3: Edge Case - No Type

**Input**:
```python
create_work_item(
    type=None,
    title="General task",
    description="General work item",
    duty="triage",
    labels=[]
)
```

**Expected Behavior**:
- Work item created
- Duty label applied
- No type label (since type is None)

**GitHub**:
```python
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="General task",
    body="General work item",
    labels=["workflow:triage"],  # Only duty label, no type
    assignees=[]
)
```

---

### Test Case 4: Edge Case - Empty Description

**Input**:
```python
create_work_item(
    type="bug",
    title="Fix null reference",
    description="",
    duty="implementation"
)
```

**Expected Behavior**:
- Work item created with empty description
- Title and duty still set correctly

---

### Test Case 5: Failure - Invalid Platform Response

**Scenario**: GitHub API returns error

**Expected Behavior**:
- Kernel catches platform error
- Returns error or raises semantic exception
- Does not crash silently

**Error Handling**:
```python
try:
    work_item_id = create_work_item(...)
except PlatformError as e:
    # Kernel should provide meaningful error message
    print(f"Failed to create work item: {e}")
```

---

## Test Execution Procedure

### Setup

1. **Prepare Test Environment**:
   - Access to GitHub repository (or mock)
   - GitHub MCP tools available
   - Test user credentials (if needed)

2. **Create Test Data**:
   - Prepare test descriptions
   - Define expected outcomes

### Execution Steps

**For Each Test Case**:

1. **Execute Operation**:
   ```python
   result = create_work_item(
       type="research",
       title="Test work item",
       description="Test description",
       duty="research"
   )
   ```

2. **Verify Result**:
   - Check returned work_item_id is valid (non-empty string)
   - Check work_item_id is numeric (for GitHub)

3. **Verify Platform State**:
   - Query created work item: `get_work_item_details(result)`
   - Verify title matches input
   - Verify description matches input
   - Verify duty is correct
   - Verify labels are correct

4. **Cleanup** (if needed):
   - Close or delete test work item
   - Mark as test data

### Verification

**For GitHub Implementation**:

```python
# After creating work item
details = get_work_item_details(work_item_id)

assert details['title'] == expected_title
assert details['duty'] == expected_duty
assert details['status'] == 'open'
assert details['description'] == expected_description

# Check labels in GitHub directly
issue = issue_read(method="get", issue_number=int(work_item_id))
labels = [l['name'] for l in issue.labels]

assert f"workflow:{expected_duty}" in labels
if expected_type:
    assert expected_type in labels
```

---

## Pass Criteria

- [ ] **Success Case 1**: Research work item created with correct labels
- [ ] **Success Case 2**: Work item created with assignee
- [ ] **Edge Case 3**: Work item created without type label
- [ ] **Edge Case 4**: Work item created with empty description
- [ ] **Failure Case 5**: Platform errors handled gracefully
- [ ] **GitHub Mapping**: Correct issue_write call with proper parameters
- [ ] **Return Value**: work_item_id is valid string
- [ ] **State Verification**: Created work item queryable and correct

---

## Test Results

**Last Run**: (Not yet executed)  
**Platform**: GitHub  
**Status**: Test scenario documented  

**Results**:
- Test Case 1: PENDING
- Test Case 2: PENDING
- Test Case 3: PENDING
- Test Case 4: PENDING
- Test Case 5: PENDING

**Notes**: Awaiting first test execution

---

## Related Tests

- `test-get-work-item-details.md` - Reading created work items
- `test-update-work-item.md` - Updating work items
- `test-assign-work-item-to-duty.md` - Changing duty

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenario for create_work_item |
