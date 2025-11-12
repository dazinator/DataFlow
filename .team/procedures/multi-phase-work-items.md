# Multi-Phase Work Item Management Procedure

**Purpose**: Manage parent-child work item relationships for multi-phase plans

**Layer**: 1 (Global Procedure)

**Used By**: All duties (Layer 2), Orchestration (Layer 0)

---

## Overview

Multi-phase work items involve a parent work item tracking an overall plan with child work items representing individual phases. This procedure defines how to create, manage, and complete multi-phase structures.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `is_multi_phase(work_item_id)` - Check if work item is part of multi-phase plan
- `get_parent_work_item(work_item_id)` - Get parent work item ID
- `get_work_item_details(work_item_id)` - Get work item information
- `list_child_work_items(work_item_id)` - Get all child work items
- `create_child_work_item(parent_id, type, title, description, duty)` - Create child linked to parent
- `update_work_item(work_item_id, ...)` - Update work item fields
- `add_work_item_comment(work_item_id, text)` - Add comment

**Work Item Fields Required**:
- `parent_id` - Parent work item ID (if child)
- `children` - List of child work items (if parent)
- `title` - Work item title
- `description` - Work item description
- `status` - Work item status ("open", "closed")

---

## When to Use Multi-Phase

Create a multi-phase structure when:
- Work naturally divides into 3+ sequential phases
- Each phase has clear deliverables and can be reviewed independently
- Phases build on each other (not independent tasks)
- Total effort exceeds what's reasonable for a single PR

**Don't use for**:
- Independent tasks that happen to relate to the same feature (use labels instead)
- Work that could reasonably fit in a single PR
- Phases that don't have clear boundaries

---

## Procedure: Creating Multi-Phase Structure

### Step 1: Create Parent Work Item

Create the parent work item with the overall plan:

```python
parent_id = create_work_item(
    type="plan",
    title="[Feature/Project Name]",
    description="""
## Overview
[Brief description of the overall goal]

## Multi-Phase Plan

- [ ] Phase 1: [Name] - [Brief description]
- [ ] Phase 2: [Name] - [Brief description]  
- [ ] Phase 3: [Name] - [Brief description]

## Phase Descriptions

### Phase 1: [Name]
**Deliverables**: [What this phase produces]
**Dependencies**: [What must exist before starting]
**Success Criteria**: [How to know phase is complete]

### Phase 2: [Name]
**Deliverables**: [What this phase produces]
**Dependencies**: Phase 1 complete
**Success Criteria**: [How to know phase is complete]

### Phase 3: [Name]
**Deliverables**: [What this phase produces]
**Dependencies**: Phase 2 complete
**Success Criteria**: [How to know phase is complete]

## Overall Success Criteria
[What success looks like when all phases complete]
""",
    duty="product-backlog"  # Parent typically stays in backlog
)
```

### Step 2: Create Child Work Items for Each Phase

For each phase, create a child work item:

```python
phase1_id = create_child_work_item(
    parent_id=parent_id,
    type="implementation",  # or "research", etc.
    title="[Phase 1] [Feature Name] - [Phase Name]",
    description="""
**Parent Issue**: #{parent_number}

## Phase 1: [Phase Name]

**Deliverables**: [What this phase produces]
**Dependencies**: [What must exist before starting]
**Success Criteria**: [How to know phase is complete]

## Detailed Requirements
[Phase-specific requirements]
""",
    duty="implementation"  # Appropriate duty for this phase
)

# Repeat for each phase
phase2_id = create_child_work_item(...)
phase3_id = create_child_work_item(...)
```

### Step 3: Update Parent with Child Links

After creating all children, update parent description with issue numbers:

```python
# Get child work item numbers
child1_number = get_work_item_details(phase1_id)['number']
child2_number = get_work_item_details(phase2_id)['number']
child3_number = get_work_item_details(phase3_id)['number']

# Update parent description to include child issue numbers
updated_description = f"""
## Multi-Phase Plan

- [ ] Phase 1: [Name] (#{child1_number}) - [Brief description]
- [ ] Phase 2: [Name] (#{child2_number}) - [Brief description]  
- [ ] Phase 3: [Name] (#{child3_number}) - [Brief description]

[... rest of description ...]
"""

update_work_item(
    work_item_id=parent_id,
    description=updated_description
)
```

---

## Procedure: Working on Child Work Items

### Step 1: Check for Parent (ALWAYS at workflow start)

At the beginning of work on ANY work item, check if it's a child:

```python
# Check if this is a multi-phase work item
is_child = is_multi_phase(work_item_id)

if is_child:
    parent_id = get_parent_work_item(work_item_id)
    # This is a child - proceed to Step 2
else:
    # Standalone work item - proceed with normal workflow
    parent_id = None
```

**When to check**: At the very beginning of duty execution, right after duty assignment verification.

### Step 2: Read Parent Context

If parent exists, read it to understand the overall plan:

```python
parent = get_work_item_details(parent_id)

# Review parent description for:
# - Overall goal
# - Which phase current work item represents
# - What phases came before (context)
# - What phases come after (roadmap)
```

**Use this context to**:
- Understand how current work fits into bigger picture
- Align implementation with overall plan
- Make informed decisions about scope and design
- Avoid duplicating work from other phases
- Prepare handover for next phase

### Step 3: Update Parent During Work

When reporting progress on the child work item, also update parent:

```python
# After calling report_progress for current work item

# Get current parent description
parent = get_work_item_details(parent_id)
parent_description = parent['description']

# Find and update the checklist item for current phase
# Example: From: - [ ] Phase 2: Core Features (#122)
#          To:   - [x] Phase 2: Core Features (#122) ✅

# Update phase status
updated_description = parent_description.replace(
    f"- [ ] Phase N: [Name] (#{current_number})",
    f"- [x] Phase N: [Name] (#{current_number}) ✅"
)

update_work_item(
    work_item_id=parent_id,
    description=updated_description
)

add_work_item_comment(
    work_item_id=parent_id,
    text=f"Phase N (#{current_number}) progress: [brief update]"
)
```

**When to update parent**:
- After significant milestones within the phase
- When reporting progress on the child work item
- Before marking child work item complete

**What to update**:
- Phase checklist item status ([ ] → [x])
- Brief progress notes in comment (keep concise)
- Any blockers or dependencies discovered

### Step 4: Close Parent When Completing Last Child

Before completing work, determine if this is the last child:

```python
# Get all children of parent
children = list_child_work_items(parent_id)

# Count open children (excluding current one)
current_number = get_work_item_details(work_item_id)['number']
other_open = [c for c in children 
              if c['status'] == 'open' and c['number'] != current_number]

is_last_child = len(other_open) == 0
```

**If this IS the last child**:

1. Update parent description with final status:
   ```python
   # Mark all phases complete in parent description
   # Add completion summary
   add_work_item_comment(
       work_item_id=parent_id,
       text="🎉 All phases complete! This multi-phase plan is finished."
   )
   ```

2. Close parent work item:
   ```python
   update_work_item(
       work_item_id=parent_id,
       status="closed"
   )
   ```

3. Add comment to current work item:
   ```python
   add_work_item_comment(
       work_item_id=work_item_id,
       text=f"This was the last phase. Parent work item #{parent_number} closed."
   )
   ```

**If this is NOT the last child**:
- Parent remains open for remaining phases
- Only close current child work item
- Update parent checklist but don't close parent

---

## Examples

### Example 1: Creating Multi-Phase Structure

```python
# Create parent for feature implementation
parent_id = create_work_item(
    type="plan",
    title="Add User Authentication System",
    description="""
## Overview
Implement complete user authentication with JWT tokens and session management.

## Multi-Phase Plan

- [ ] Phase 1: Core Authentication
- [ ] Phase 2: Session Management
- [ ] Phase 3: Security Hardening

## Phase Descriptions

### Phase 1: Core Authentication
**Deliverables**: JWT generation/validation, user login/logout
**Dependencies**: None
**Success Criteria**: Users can log in and receive valid JWT tokens

### Phase 2: Session Management  
**Deliverables**: Session storage, refresh tokens, timeout handling
**Dependencies**: Phase 1 complete
**Success Criteria**: Sessions persist and refresh correctly

### Phase 3: Security Hardening
**Deliverables**: Rate limiting, brute force protection, audit logging
**Dependencies**: Phases 1-2 complete
**Success Criteria**: System resistant to common attacks
""",
    duty="product-backlog"
)

# Create phase 1
phase1_id = create_child_work_item(
    parent_id=parent_id,
    type="implementation",
    title="[Phase 1] User Authentication - Core Authentication",
    description="[Phase-specific details]",
    duty="implementation"
)
```

### Example 2: Working on Child Work Item

```python
# At workflow start
work_item_id = "789"

# Check for parent
is_child = is_multi_phase(work_item_id)
# Result: True

parent_id = get_parent_work_item(work_item_id)
parent = get_work_item_details(parent_id)

# Review parent context
print(f"Parent: {parent['title']}")
print(f"Overall goal: [from parent description]")
print(f"My phase: Phase 2 - Session Management")
print(f"Previous phase: Phase 1 (Core Auth) - completed")
print(f"Next phase: Phase 3 (Security Hardening)")

# Use context to guide implementation
# - Build on Phase 1 authentication
# - Prepare for Phase 3 security features
```

### Example 3: Completing Last Child

```python
# Completing Phase 3
work_item_id = "791"
parent_id = get_parent_work_item(work_item_id)

# Check if last child
children = list_child_work_items(parent_id)
other_open = [c for c in children if c['status'] == 'open' and c['id'] != work_item_id]
is_last = len(other_open) == 0
# Result: True - this is the last phase

# Update parent
add_work_item_comment(
    work_item_id=parent_id,
    text="🎉 All authentication phases complete! User auth system fully implemented."
)

update_work_item(
    work_item_id=parent_id,
    status="closed"
)

# Note in current work item
add_work_item_comment(
    work_item_id=work_item_id,
    text=f"Completed final phase. Parent #{parent_number} closed."
)
```

---

## Edge Cases

### Case 1: Parent Created But Children Not Yet Created

**Scenario**: Parent exists but children haven't been created

**Resolution**:
1. If you're assigned the parent, create the children
2. If someone else is handling it, wait or ask for clarification
3. Don't proceed until structure is clear

### Case 2: Phase Order Violated

**Scenario**: Phase 3 started before Phase 2 complete

**Resolution**:
1. Check parent to verify phase dependencies
2. If truly dependent, wait for previous phase
3. If actually independent, consider restructuring as independent work items

### Case 3: Phases Need Reordering Mid-Plan

**Scenario**: Discover Phase 2 should come before Phase 1

**Resolution**:
1. Update parent description to reflect new order
2. Add comment explaining the change
3. Adjust dependencies in child work items
4. Continue with new order

---

## Anti-Patterns

### ❌ Don't: Skip Parent Context Check

```python
# Wrong - starting work without checking parent
# No check for is_multi_phase()
# Missing context from overall plan
```

✅ **Correct**: Always check for parent at workflow start

### ❌ Don't: Forget to Update Parent Progress

```python
# Wrong - completing child without updating parent
update_work_item(work_item_id, status="closed")
# Parent still shows [ ] for this phase
```

✅ **Correct**: Update parent checklist and add progress comment

### ❌ Don't: Close Parent Before All Children Complete

```python
# Wrong - closing parent while children still open
is_last = False  # Other children still open
update_work_item(parent_id, status="closed")  # WRONG
```

✅ **Correct**: Only close parent when completing last child

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong - using GitHub-specific sub-issue API
sub_issue_write(  # ❌ Don't use platform-specific operations
    method="add",
    issue_number=parent_number,
    sub_issue_id=child_id
)
```

✅ **Correct**: Use semantic operation `create_child_work_item()`

---

## Success Criteria

- [ ] Parent work item has clear multi-phase plan
- [ ] All child work items created and linked to parent
- [ ] Parent context checked at start of child work
- [ ] Parent updated as child work progresses
- [ ] Parent closed when last child completes
- [ ] Semantic operations used exclusively (no platform code)

---

## Related Procedures

- [Work Item Creation](work-item-creation.md) - Creating parent and child work items
- [Duty Assignment](duty-assignment.md) - Assigning duties to phases
- [Comment Patterns](comment-patterns.md) - Standard comment formats

---

## Testing

**Test Scenarios**: `.team/procedures/tests/multi-phase-work-items/`

**Key Scenarios**:
1. Creating multi-phase structure (happy path)
2. Working on middle child (context usage)
3. Completing last child (parent closure)
4. Phase without parent (standalone)
5. Parent with no children yet (edge case)
