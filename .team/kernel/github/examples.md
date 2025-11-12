# GitHub Driver: Usage Examples

**Platform**: GitHub Issues  
**Version**: 1.0  
**Created**: 2025-11-12

This document provides practical examples of using semantic operations with the GitHub driver.

---

## Example 1: Create Research Work Item

**Scenario**: Create a new research work item assigned to the research duty

```python
# Semantic operation (platform-agnostic)
work_item_id = create_work_item(
    type="research",
    title="Investigate caching strategy for DataFlow pipelines",
    description="""## Objective
Evaluate caching strategies for improving DataFlow pipeline performance.

## Scope
- Compare in-memory vs distributed caching
- Benchmark performance impact
- Document trade-offs

## Deliverables
- Analysis document in `/research/caching-strategy/`
- Performance benchmarks
- Recommendations for implementation
""",
    duty="research",
    labels=["performance", "investigation"]
)

print(f"Created work item: #{work_item_id}")
# Output: Created work item: #456
```

**GitHub Result**:
- Issue #456 created
- Labels: `workflow:research`, `research`, `performance`, `investigation`
- State: open

---

## Example 2: Query Research Queue

**Scenario**: Get all open research work items

```python
# Semantic operation
research_items = query_work_items_by_duty(
    duty="research",
    status="open"
)

print(f"Found {len(research_items)} open research items:")
for item in research_items:
    print(f"  #{item['id']}: {item['title']}")
```

**Output**:
```
Found 3 open research items:
  #456: Investigate caching strategy for DataFlow pipelines
  #423: Research async pipeline patterns
  #401: Evaluate alternative concurrency models
```

**GitHub Query**: `list_issues(labels=["workflow:research"], state="OPEN")`

---

## Example 2.5: Query Unlabeled Work Items (For Bulk Triage)

**Scenario**: Find all work items without workflow labels that need initial triage

```python
# Semantic operation
unlabeled_items = query_unlabeled_work_items(status="open")

print(f"Found {len(unlabeled_items)} work items needing initial triage:")
for item in unlabeled_items:
    print(f"  #{item['id']}: {item['title']}")
    print(f"    Duty: {item['duty']}")  # Will be None
```

**Output**:
```
Found 2 work items needing initial triage:
  #377: Add validation for pipeline configuration
    Duty: None
  #380: Update documentation for custom blocks
    Duty: None
```

**GitHub Implementation**: 
```python
# Gets all issues, filters out those with workflow:* labels
all_issues = list_issues(state="OPEN")
# Filter client-side to exclude issues with workflow:* labels
unlabeled = [issue for issue in all_issues 
             if not any(label.startswith('workflow:') for label in issue.labels)]
```

**Use Case**: 
- Bulk triage operations combine `query_work_items_by_duty("triage")` and `query_unlabeled_work_items()` 
- Ensures all work items are processed, including newly created issues without labels

---

## Example 3: Handover to Implementation

**Scenario**: Research complete, hand over to implementation duty

```python
# Research work item #456 is complete
work_item_id = "456"

# Add handover comment
add_work_item_comment(
    work_item_id=work_item_id,
    text="""## Research Complete ✅

**Findings**: Documented in `/research/caching-strategy/`

**Recommendation**: Implement distributed caching with Redis

**Next Steps**: See implementation specification in handover issue
"""
)

# Create implementation work item
impl_id = create_work_item(
    type="implementation",
    title="Implement Redis caching for DataFlow pipelines",
    description="""## Implementation Specification

**Research**: #456

**Design**: See `/research/caching-strategy/design.md`

## Tasks
- [ ] Add Redis client dependency
- [ ] Create CacheBlock propagator
- [ ] Add cache key strategy
- [ ] Write integration tests
- [ ] Update documentation
""",
    duty="implementation"
)

# Transition research work item to closed
update_work_item(
    work_item_id=work_item_id,
    status="closed"
)

# Assign original work item to implementation (alternative approach)
# assign_work_item_to_duty(
#     work_item_id=work_item_id,
#     duty="implementation"
# )

print(f"Handed over to implementation: #{impl_id}")
```

**GitHub Result**:
- Issue #456 closed with handover comment
- New issue created (e.g., #457) with `workflow:implementation` label
- Research complete, implementation ready to start

---

## Example 4: Multi-Phase Work - Create Parent and Children

**Scenario**: Create a multi-phase implementation with sub-tasks

```python
# Create parent work item
parent_id = create_work_item(
    type="implementation",
    title="[Multi-Phase] Layered Prompt Architecture Migration",
    description="""## Overview
Multi-phase implementation of layered, platform-agnostic prompt architecture.

## Phases
This work is divided into 6 phases (tracked as sub-issues):
- Phase 0: Process Modeling Alignment
- Phase 1: Foundation (Kernel, Graph, Tooling)
- Phase 2: Global Procedures Migration
- Phase 3: Duty Migration
- Phase 4: Orchestration Update
- Phase 5: Cleanup & Validation

## Success Criteria
- All phases complete
- Zero kernel leaks
- All tests passing
""",
    duty="implementation"
)

print(f"Created parent: #{parent_id}")

# Create Phase 1 sub-task
phase1_id = create_child_work_item(
    parent_id=parent_id,
    type="implementation",
    title="[Phase 1] Foundation - Kernel, Semantic Operations, Graph, Tooling",
    description="""## Phase 1: Foundation

Create kernel layer and tooling.

## Tasks
- [ ] Create kernel structure
- [ ] Implement semantic operations
- [ ] Create graph tooling
- [ ] Implement leak detection
""",
    duty="implementation",
    labels=["phase-1"]
)

print(f"Created Phase 1: #{phase1_id}")

# Create Phase 2 sub-task
phase2_id = create_child_work_item(
    parent_id=parent_id,
    type="implementation",
    title="[Phase 2] Global Procedures Migration",
    description="""## Phase 2: Global Procedures

Extract platform-agnostic procedures.

## Dependencies
- Phase 1 must be complete

## Tasks
- [ ] Extract duty assignment procedure
- [ ] Extract multi-phase procedure
- [ ] Extract self-improvement procedure
""",
    duty="implementation",
    labels=["phase-2"]
)

print(f"Created Phase 2: #{phase2_id}")
```

**GitHub Result**:
- Parent issue created (e.g., #363)
- Child issues created (e.g., #365, #366)
- Sub-issue relationships established
- Can query children later

---

## Example 5: Check and Update Multi-Phase Progress

**Scenario**: Working on a child issue, update parent progress

```python
# Currently working on Phase 1 (#365)
current_id = "365"

# Check if multi-phase
if is_multi_phase(current_id):
    print("This is part of a multi-phase plan")
    
    # Get parent
    parent_id = get_parent_work_item(current_id)
    
    if parent_id:
        print(f"Parent work item: #{parent_id}")
        
        # Get all children to check progress
        children = list_child_work_items(parent_id)
        
        completed = sum(1 for c in children if c['status'] == 'closed')
        total = len(children)
        
        print(f"Overall progress: {completed}/{total} phases complete")
        
        # Update parent with progress comment
        add_work_item_comment(
            work_item_id=parent_id,
            text=f"""## Progress Update

**Phase 1 Complete** ✅

**Overall Progress**: {completed + 1}/{total} phases complete

**Next**: Phase 2 - Global Procedures Migration
"""
        )
        
        # If this is the last phase, close parent
        if completed + 1 == total:
            update_work_item(
                work_item_id=parent_id,
                status="closed"
            )
            print("All phases complete! Closed parent work item.")
```

**Output**:
```
This is part of a multi-phase plan
Parent work item: #363
Overall progress: 1/6 phases complete
```

---

## Example 6: Submit Self-Improvement Feedback

**Scenario**: After completing work, submit feedback on the workflow

```python
# Just completed work item #365
work_item_id = "365"

submit_feedback(
    work_item_id=work_item_id,
    duty="implementation",
    what_worked="""
- Clear task breakdown in issue description
- Kernel design documents were comprehensive
- Examples in semantic language spec were helpful
""",
    what_didnt_work="""
- Unclear if should create all scripts at once or incrementally
- No guidance on script testing methodology
- Graph tooling examples would have been helpful
""",
    suggestions="""
1. Add section on incremental development approach
2. Create script testing procedure in testing framework
3. Include graph tooling examples in kernel README
"""
)

print("Feedback submitted to tracker")
```

**GitHub Result**:
- Finds `[Workflow Feedback] Tracker` issue
- Adds formatted comment with feedback
- Available for Process Modeling workflow to review

---

## Example 7: Get Work Item Details

**Scenario**: Read complete work item information

```python
work_item_id = "365"

details = get_work_item_details(work_item_id)

print(f"Title: {details['title']}")
print(f"Status: {details['status']}")
print(f"Duty: {details['duty']}")
print(f"Type: {details['type']}")
print(f"Created: {details['created_at']}")
print(f"Assignee: {details['assignee']}")

print(f"\nDescription:\n{details['description']}")
```

**Output**:
```
Title: [Phase 1] Foundation - Kernel, Semantic Operations, Graph, Tooling
Status: open
Duty: implementation
Type: implementation
Created: 2025-11-12T00:35:12Z
Assignee: Copilot

Description:
## Phase 1: Foundation
...
```

---

## Example 8: Update Work Item

**Scenario**: Update work item title and add assignee

```python
work_item_id = "365"

update_work_item(
    work_item_id=work_item_id,
    title="[Phase 1] Foundation - Kernel, Semantic Operations, Graph, Tooling ✅",
    assignee="Copilot"
)

print("Work item updated")
```

**GitHub Result**:
- Issue #365 title updated with checkmark
- Assignee set to Copilot

---

## Example 9: Cleanup - Remove Invalid Duty Labels

**Scenario**: Fix work item with multiple duty labels (data quality issue)

```python
work_item_id = "456"

# Get current state
details = get_work_item_details(work_item_id)

# User has determined correct duty should be "implementation"
correct_duty = "implementation"

# Reassign (this removes old workflow:* labels)
assign_work_item_to_duty(
    work_item_id=work_item_id,
    duty=correct_duty
)

# Add comment explaining the fix
add_work_item_comment(
    work_item_id=work_item_id,
    text=f"""## Duty Label Cleanup

Fixed multiple duty labels. This work item is now assigned to: **{correct_duty}**

Previous state had conflicting duty designations.
"""
)

print(f"Work item #{work_item_id} cleaned up and assigned to {correct_duty}")
```

---

## Example 10: Error Handling

**Scenario**: Handle common errors gracefully

```python
work_item_id = "999999"  # Non-existent issue

try:
    details = get_work_item_details(work_item_id)
    print(f"Found: {details['title']}")
except Exception as e:
    print(f"Error: Work item #{work_item_id} not found")
    print(f"Details: {e}")
    # Handle appropriately (log, return None, raise semantic error, etc.)
```

---

## Example 11: Batch Query and Process

**Scenario**: Process all open implementation work items

```python
# Get all open implementation items
impl_items = query_work_items_by_duty(
    duty="implementation",
    status="open"
)

print(f"Processing {len(impl_items)} implementation items...")

for item in impl_items:
    work_item_id = item['id']
    
    # Get full details
    details = get_work_item_details(work_item_id)
    
    # Check if multi-phase
    if is_multi_phase(work_item_id):
        print(f"  #{work_item_id}: {item['title']} (multi-phase)")
        
        # Could update parent progress here
    else:
        print(f"  #{work_item_id}: {item['title']} (standalone)")
```

---

## Example 12: Complete Workflow - Research to Implementation

**Scenario**: Full lifecycle from research to implementation

```python
# Step 1: Create research work item
research_id = create_work_item(
    type="research",
    title="Research feature X implementation approach",
    description="Validate technical feasibility...",
    duty="research"
)

print(f"Step 1: Created research #{research_id}")

# ... research work happens ...

# Step 2: Complete research, add findings
add_work_item_comment(
    work_item_id=research_id,
    text="Research complete. Findings in `/research/feature-x/`"
)

update_work_item(
    work_item_id=research_id,
    status="closed"
)

print(f"Step 2: Research #{research_id} closed")

# Step 3: Create implementation work item
impl_id = create_work_item(
    type="implementation",
    title="Implement feature X",
    description=f"Based on research #{research_id}...",
    duty="implementation"
)

print(f"Step 3: Created implementation #{impl_id}")

# Step 4: Link research to implementation
add_work_item_comment(
    work_item_id=impl_id,
    text=f"**Research**: #{research_id}"
)

print("Step 4: Linked research to implementation")

# ... implementation work happens ...

# Step 5: Submit feedback
submit_feedback(
    work_item_id=impl_id,
    duty="implementation",
    what_worked="Research spec was thorough",
    what_didnt_work="Could use more examples",
    suggestions="Add implementation examples to research template"
)

print("Step 5: Feedback submitted")

# Step 6: Close implementation
update_work_item(
    work_item_id=impl_id,
    status="closed"
)

print(f"Step 6: Implementation #{impl_id} complete ✅")
```

**Output**:
```
Step 1: Created research #500
Step 2: Research #500 closed
Step 3: Created implementation #501
Step 4: Linked research to implementation
Step 5: Feedback submitted
Step 6: Implementation #501 complete ✅
```

---

## Platform-Agnostic Benefits

All these examples use semantic operations. If we switch to Azure DevOps:

1. **No code changes needed** in procedures/duties
2. **Kernel handles translation** to Azure DevOps APIs
3. **Same semantic operations work** on different platform

**Example** - Same code, different platform:
```python
# This code works on BOTH GitHub and Azure DevOps
work_item_id = create_work_item(
    type="research",
    title="Feature investigation",
    description="...",
    duty="research"
)

# GitHub: Creates issue with workflow:research label
# Azure DevOps: Creates work item with duty:research tag
# Procedures don't know or care which platform is active
```

---

## Related Documentation

- [Operation Mappings](operations.md) - Detailed implementation of each operation
- [GitHub Driver README](README.md) - Platform-specific considerations
- [Kernel Overview](../README.md) - Architecture and concepts
- [Semantic Language Reference](../../../docs/design/prompt-engineering/semantic-language.md) - Complete operation specifications

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial examples for all 12 semantic operations |
