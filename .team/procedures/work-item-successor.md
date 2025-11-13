# Work Item Successor Procedure

**Purpose**: Handle scope splitting during implementation by creating successor work items

**Layer**: 1 (Global Procedure)

**Used By**: All duties (Layer 2), especially Implementation and Research

---

## Overview

The work item successor pattern handles situations where, during active work on a PR, it becomes clear that part of the scope should be deferred to a follow-up PR. This creates a predecessor→successor relationship where the successor continues the work from where the predecessor left off.

**Key Scenario**: You're implementing a feature and realize it's best to merge the current progress and complete the remaining work in a separate PR.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `create_child_work_item(parent_id, type, title, description, duty)` - Create sibling work item
- `get_work_item_details(work_item_id)` - Get work item information
- `get_parent_work_item(work_item_id)` - Get parent work item ID
- `is_multi_phase(work_item_id)` - Check if part of multi-phase plan
- `update_work_item(work_item_id, ...)` - Update work item fields
- `add_work_item_comment(work_item_id, text)` - Add comment
- `list_child_work_items(work_item_id)` - Get all child work items

**Work Item Fields Required**:
- `parent_id` - Parent work item ID (if child)
- `children` - List of child work items (if parent)
- `title` - Work item title
- `description` - Work item description
- `status` - Work item status ("open", "closed")
- `number` - Work item number for references

---

## When to Use Successor Pattern

Create a successor work item when:
- Actively working on implementation/research with an open PR
- Discover scope is larger than initially estimated
- Current progress represents a valuable, mergeable subset
- Remaining work can be clearly separated
- PR will close current issue (can't keep current issue open)

**Don't use for**:
- Planning phases BEFORE implementation starts (use multi-phase pattern instead)
- Independent work not logically connected to current work
- Work that could reasonably be completed in current PR
- Situations where multi-phase planning would be more appropriate

---

## Successor vs Multi-Phase Pattern

| Aspect | Multi-Phase Pattern | Successor Pattern |
|--------|---------------------|-------------------|
| **When** | BEFORE implementation starts | DURING active implementation |
| **Planning** | Eager - all phases known upfront | Lazy - scope discovered iteratively |
| **Structure** | Parent plan + child phases | Sibling work items under same parent |
| **Current Issue** | Becomes child of new parent | WILL close (PR target) |
| **Creation** | Creates parent + all children | Creates one successor at a time |
| **Predictability** | Known number of phases | Unknown number of successors |
| **Use Case** | Structured, planned work division | Adaptive scope boundary discovery |

**Example**:
- **Multi-Phase**: "Build auth system" → Plan 3 phases (core, sessions, security) before starting
- **Successor**: Implementing auth, realize sessions need more work → Merge core auth, defer sessions to successor

---

## Procedure: Creating Successor Work Item

### Step 1: Check for Parent Work Item

Before creating successor, determine if current work item has a parent:

```python
# Check if current work item is part of multi-phase plan
is_child = is_multi_phase(current_work_item_id)

if is_child:
    parent_id = get_parent_work_item(current_work_item_id)
else:
    # Standalone work item - no parent exists
    parent_id = None
```

**Why this matters**: 
- If parent exists, successor must be sibling (same parent)
- If no parent, successor is standalone work item
- Parent needs to be updated to show both predecessor and successor

### Step 2: Define Scope Boundary

Clearly identify what's in current PR vs successor:

**Document in current work item**:
```python
add_work_item_comment(
    work_item_id=current_work_item_id,
    text="""
## Scope Split Decision

**Completed in this PR**:
- [x] Feature/component 1
- [x] Feature/component 2
- [x] Basic implementation

**Deferred to successor** (#{successor_number}):
- [ ] Feature/component 3
- [ ] Advanced features
- [ ] Performance optimizations

**Rationale**: [Why this split makes sense - e.g., current work is substantial and mergeable, remaining work requires separate design decisions]
"""
)
```

### Step 3: Create Successor Work Item

**If parent exists** (current work item is child of multi-phase plan):

```python
# Get parent details
parent = get_work_item_details(parent_id)
current = get_work_item_details(current_work_item_id)

# Create successor as sibling
successor_id = create_child_work_item(
    parent_id=parent_id,
    type=current['type'],  # Same type as predecessor
    title=f"{current['title']} - Continuation",
    description=f"""
**Predecessor**: #{current['number']} (read for full context)
**Parent**: #{parent['number']}

## Continuation from Predecessor

This work item continues from #{current['number']}, which completed:
- [Summary of what predecessor accomplished]

## Remaining Scope

[Complete scope for THIS work item, extracted from predecessor]

### Requirements
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

## Context from Predecessor

**Key Decisions Made**:
- [Decision 1]
- [Decision 2]

**Constraints**:
- [Constraint 1]
- [Constraint 2]

## Success Criteria
- [ ] All remaining scope implemented
- [ ] Tests passing
- [ ] Documentation updated
- [ ] Builds on predecessor work

## References
- Predecessor: #{current['number']}
- Parent: #{parent['number']}
""",
    duty="implementation"  # Or appropriate duty
)
```

**If no parent exists** (standalone work item):

```python
current = get_work_item_details(current_work_item_id)

# Create successor as standalone work item
successor_id = create_work_item(
    type=current['type'],
    title=f"{current['title']} - Continuation",
    description=f"""
**Predecessor**: #{current['number']} (read for full context)

## Continuation from Predecessor

This work item continues from #{current['number']}, which completed:
- [Summary of what predecessor accomplished]

## Remaining Scope

[Complete scope for THIS work item, extracted from predecessor]

### Requirements
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

## Context from Predecessor

**Key Decisions Made**:
- [Decision 1]
- [Decision 2]

**Constraints**:
- [Constraint 1]
- [Constraint 2]

## Success Criteria
- [ ] All remaining scope implemented
- [ ] Tests passing
- [ ] Documentation updated
- [ ] Builds on predecessor work

## References
- Predecessor: #{current['number']}
""",
    duty="implementation",  # Or appropriate duty - can be "triage" if unsure
    labels=["continuation"]
)
```

### Step 4: Update Predecessor Work Item

Add reference to successor in current work item:

```python
# Add successor reference
add_work_item_comment(
    work_item_id=current_work_item_id,
    text=f"""
✅ **Successor Created**

Remaining work moved to successor: #{successor_number}

This PR will merge the completed scope. The successor will continue with remaining features.
"""
)
```

### Step 5: Update Parent Work Item (If Exists)

If current work item has parent, update parent to show both predecessor and successor:

```python
if is_child:
    parent = get_work_item_details(parent_id)
    parent_description = parent['description']
    current_number = current['number']
    successor_number = get_work_item_details(successor_id)['number']
    
    # Update parent description to include both work items
    # Example: Adding successor reference next to predecessor
    # Original: "- [ ] Phase 1: Feature X (#123)"
    # Updated:  "- [ ] Phase 1: Feature X (#123, successor: #124)"
    
    add_work_item_comment(
        work_item_id=parent_id,
        text=f"""
📋 **Work Item Successor Created**

Work item #{current_number} split into predecessor→successor:
- **Predecessor**: #{current_number} (closing with current PR)
- **Successor**: #{successor_number} (continuing remaining work)

**Scope Split**:
- Predecessor completed: [Brief summary]
- Successor will complete: [Brief summary]
"""
    )
```

### Step 6: Assign Duty to Successor

Determine appropriate duty for successor:

**If clear what duty needed**:
- Same type of work → Same duty as predecessor
- Different type → Appropriate duty (research, implementation, etc.)

**If uncertain**:
- Assign to `triage` duty
- Let triage determine correct duty

```python
# Already specified in create_work_item() or create_child_work_item()
# Duty can be: "implementation", "research", "triage", etc.
```

---

## Procedure: Working on Successor Work Item

### Step 1: Read Predecessor Context

**ALWAYS read predecessor work item first**:

```python
successor = get_work_item_details(successor_work_item_id)
# Extract predecessor number from description
predecessor_number = # Parse from "**Predecessor**: #NNN"

predecessor = get_work_item_details(predecessor_work_item_id)

# Review:
# - What was completed
# - Key decisions made
# - Constraints identified
# - Design patterns used
# - Test patterns established
```

**If predecessor has its own predecessor (chain), read entire chain**:

```python
# Check if predecessor also has a predecessor
predecessor_description = predecessor['description']

if "**Predecessor**:" in predecessor_description:
    # This is a chain - read all predecessors
    # Extract earlier predecessor number
    earlier_predecessor_number = # Parse from predecessor description
    
    earlier_predecessor = get_work_item_details(earlier_predecessor_number)
    
    # Continue recursively if more predecessors exist
    # Build complete context from entire chain
```

**Why this matters**: 
- Predecessor contains critical context and decisions that inform successor implementation
- Chains accumulate decisions across multiple work items
- Must honor ALL decisions from entire chain, not just immediate predecessor

### Step 2: Align with Predecessor Approach

Build on what predecessor established:

- Use same design patterns
- Follow same coding conventions
- Maintain consistency with predecessor
- Honor constraints from predecessor
- Extend existing test patterns

### Step 3: Update Parent (If Exists)

If successor has parent, update parent progress:

```python
is_child = is_multi_phase(successor_work_item_id)

if is_child:
    parent_id = get_parent_work_item(successor_work_item_id)
    
    # Update parent with progress
    add_work_item_comment(
        work_item_id=parent_id,
        text=f"Successor #{successor_number} progress: [brief update]"
    )
```

### Step 4: Create New Successor (If Needed)

If scope needs further splitting, repeat the successor pattern:

```python
# Current successor becomes predecessor
# Create new successor following Step 3 procedure
new_successor_id = create_child_work_item(...)  # Or create_work_item()

# Chain continues: Original → Successor1 → Successor2 → ...
```

**Pattern can continue**: Each successor can spawn its own successor as scope boundaries are discovered.

---

## Examples

### Example 1: Implementation Successor (With Parent)

```python
# Context: Implementing Phase 2 of auth system
# Discovered session management is larger than expected
# Current PR has core session storage working
# Need to defer refresh tokens and expiration to successor

# Step 1: Check for parent
current_work_item_id = "456"
is_child = is_multi_phase(current_work_item_id)
# Result: True

parent_id = get_parent_work_item(current_work_item_id)
# Result: "455" (parent: "Implement User Authentication System")

# Step 2: Document scope split
add_work_item_comment(
    work_item_id="456",
    text="""
## Scope Split Decision

**Completed in this PR**:
- [x] Session storage infrastructure
- [x] Basic session creation/retrieval
- [x] Session cleanup

**Deferred to successor**:
- [ ] Refresh token mechanism
- [ ] Token expiration handling
- [ ] Session renewal API

**Rationale**: Core storage is complete and testable. Refresh tokens require additional security analysis.
"""
)

# Step 3: Create successor as sibling
successor_id = create_child_work_item(
    parent_id="455",
    type="implementation",
    title="[Phase 2] User Authentication - Session Refresh & Expiration",
    description="""
**Predecessor**: #456 (read for full context)
**Parent**: #455

## Continuation from Predecessor

This work item continues from #456, which completed:
- Session storage infrastructure
- Basic session CRUD operations
- Session cleanup mechanism

## Remaining Scope

### Requirements
- [ ] Implement refresh token generation
- [ ] Implement token expiration logic
- [ ] Add session renewal API endpoint
- [ ] Handle expired session scenarios
- [ ] Add tests for refresh and expiration

## Context from Predecessor

**Key Decisions Made**:
- Sessions stored in Redis with 24-hour TTL
- Session IDs use GUID format
- Cleanup runs every 15 minutes

**Constraints**:
- Must maintain backward compatibility with existing session API
- Refresh tokens must use JWT format

## Success Criteria
- [ ] Refresh token mechanism working
- [ ] Expiration handled correctly
- [ ] All tests passing
- [ ] Documentation updated

## References
- Predecessor: #456
- Parent: #455
""",
    duty="implementation"
)

# Step 4: Update predecessor
successor_number = get_work_item_details(successor_id)['number']

add_work_item_comment(
    work_item_id="456",
    text=f"""
✅ **Successor Created**

Remaining work moved to successor: #{successor_number}

This PR merges core session storage. Refresh tokens and expiration deferred to successor.
"""
)

# Step 5: Update parent
add_work_item_comment(
    work_item_id="455",
    text=f"""
📋 **Work Item Successor Created**

Phase 2 (#456) split into predecessor→successor:
- **Predecessor**: #456 (core session storage - merging)
- **Successor**: #{successor_number} (refresh & expiration - next)

**Scope Split**:
- Predecessor completed: Core session infrastructure
- Successor will complete: Refresh tokens and expiration handling
"""
)
```

### Example 2: Research Successor (Standalone)

```python
# Context: Researching caching strategies
# Validated Redis approach, but discovered need for cache invalidation research
# Current research PR covers Redis basics
# Need successor for invalidation patterns

# Step 1: Check for parent
current_work_item_id = "789"
is_child = is_multi_phase(current_work_item_id)
# Result: False (standalone research)

# Step 3: Create successor as standalone
successor_id = create_work_item(
    type="research",
    title="Investigate cache invalidation patterns for distributed pipeline",
    description="""
**Predecessor**: #789 (read for full context)

## Continuation from Predecessor

This research continues from #789, which validated:
- Redis as distributed cache solution
- StackExchange.Redis client selection
- Basic get/set patterns

## Remaining Research

### Research Questions
- [ ] What invalidation patterns work for pipeline state?
- [ ] How to handle stale cache during pipeline updates?
- [ ] Should we use TTL, explicit invalidation, or both?
- [ ] How to coordinate invalidation across multiple processes?

## Context from Predecessor

**Key Decisions Made**:
- Redis selected over Memcached
- Using JSON serialization for cache values
- Cache keys use pipeline-id:block-id format

**Constraints**:
- Must work with existing Redis setup
- Can't require external coordination service

## Success Criteria
- [ ] Invalidation patterns evaluated
- [ ] Trade-offs documented
- [ ] Recommendation made
- [ ] Implementation handover created (if approved)

## References
- Predecessor: #789
- Predecessor folder: /research/distributed-caching/
""",
    duty="research",
    labels=["investigation", "continuation"]
)

# Step 4: Update predecessor
successor_number = get_work_item_details(successor_id)['number']

add_work_item_comment(
    work_item_id="789",
    text=f"""
✅ **Successor Created**

Cache invalidation research moved to successor: #{successor_number}

This PR completes basic Redis validation. Invalidation patterns researched separately.
"""
)
```

### Example 3: Successor Chain

```python
# Original work item: #100
# Successor 1: #101 (created from #100)
# Successor 2: #102 (created from #101)

# When working on #102:
# Read both #100 AND #101 for complete context

# Get all predecessor context
successor = get_work_item_details("102")
# Description shows: "**Predecessor**: #101"

predecessor1 = get_work_item_details("101")
# Description shows: "**Predecessor**: #100"

predecessor_original = get_work_item_details("100")

# Review context from entire chain:
# - What #100 accomplished
# - What #101 accomplished  
# - What remains for #102
```

---

## Edge Cases

### Case 1: Multiple Successors from One Predecessor

**Scenario**: Predecessor work reveals multiple independent follow-up tasks

**Resolution**:
1. Create separate work items, not as successors (they're independent)
2. All reference the predecessor for context
3. Use labels to group related work
4. Don't force linear successor chain for parallel work

**Example**:
```python
# Wrong: Creating successor chain for independent work
predecessor → successor1 → successor2  # If successor2 doesn't depend on successor1

# Right: Creating independent work items
predecessor → follow-up-A  # Independent task A
predecessor → follow-up-B  # Independent task B
```

### Case 2: Successor Needs Different Duty

**Scenario**: Predecessor in implementation, but successor needs research

**Resolution**:
1. Create successor with appropriate duty (research)
2. Explain in description why duty changed
3. Reference predecessor for context
4. Can use triage if uncertain about duty

**Example**:
```python
successor_id = create_work_item(
    type="research",
    title="Research advanced features for [Feature] (from #456)",
    description="""
**Predecessor**: #456 (implementation)

**Why Research**: Implementation (#456) revealed need to validate advanced 
patterns before proceeding. Research needed before continuing implementation.

[Rest of research description]
""",
    duty="research"
)
```

### Case 3: Successor Not Needed After All

**Scenario**: Created successor, but later realize it's not needed

**Resolution**:
1. Close successor work item
2. Add comment explaining why not needed
3. Update predecessor if it mentioned successor
4. Update parent (if exists) to show successor not needed

```python
add_work_item_comment(
    work_item_id=successor_id,
    text="""
Closing this successor work item.

**Reason**: After further analysis, remaining scope from predecessor #456 
is no longer needed due to [reason - e.g., requirements changed, 
alternative approach found, etc.]
"""
)

update_work_item(
    work_item_id=successor_id,
    status="closed"
)
```

### Case 4: Successor Needs Its Own Parent

**Scenario**: Successor scope is large enough to need multi-phase planning

**Resolution**:
1. Close original successor work item
2. Create multi-phase parent plan
3. Reference predecessor in parent description
4. Create phases as children of new parent

**Example**:
```python
# Original: predecessor #100 → successor #101
# Discovered: Successor #101 needs multi-phase

# Create parent for successor scope
parent_id = create_work_item(
    type="plan",
    title="[Feature] Advanced Implementation Plan",
    description="""
**Origin**: Successor #101 from predecessor #100

## Multi-Phase Plan
[Phases for what was going to be in #101]
""",
    duty="product-backlog"
)

# Close original successor
update_work_item(work_item_id="101", status="closed")

# Create phases as children
phase1_id = create_child_work_item(parent_id=parent_id, ...)
```

---

## Anti-Patterns

### ❌ Don't: Use Successor When Multi-Phase Planning Appropriate

```python
# Wrong: Creating successors when you know all phases upfront
# Before implementation, you know need 3 phases
predecessor_id = create_work_item(...)  # Phase 1
# Later create successor for Phase 2
# Later create successor for Phase 3

# Right: Use multi-phase pattern from the start
parent_id = create_work_item(type="plan", ...)
phase1_id = create_child_work_item(parent_id, ...)
phase2_id = create_child_work_item(parent_id, ...)
phase3_id = create_child_work_item(parent_id, ...)
```

✅ **Correct**: Use multi-phase for known phases, successor for discovered scope boundaries

### ❌ Don't: Create Successor Without Clear Scope Split

```python
# Wrong: Vague scope split
add_work_item_comment(
    work_item_id=current_id,
    text="Some stuff done, rest goes to successor"  # Too vague
)
```

✅ **Correct**: Document clear scope boundary with specifics

### ❌ Don't: Forget to Update Parent

```python
# Wrong: Creating sibling but not updating parent
successor_id = create_child_work_item(parent_id=parent_id, ...)
# Missing: parent update showing new successor

# Right: Always update parent when creating sibling
successor_id = create_child_work_item(parent_id=parent_id, ...)
add_work_item_comment(work_item_id=parent_id, text="...")
```

✅ **Correct**: Always update parent when creating successor sibling

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong: Using platform-specific operations
sub_issue_write(  # ❌ Don't use platform-specific operations
    method="add",
    issue_number=parent_number,
    sub_issue_id=successor_id
)
```

✅ **Correct**: Use semantic operation `create_child_work_item()`

---

## Success Criteria

- [ ] Successor work item created with complete scope description
- [ ] Predecessor updated with scope split and successor reference
- [ ] Parent updated (if exists) to show both predecessor and successor
- [ ] Successor references predecessor for context
- [ ] Clear rationale provided for scope split
- [ ] Appropriate duty assigned to successor
- [ ] Semantic operations used exclusively (no platform code)

---

## Related Procedures

- [Multi-Phase Work Items](multi-phase-work-items.md) - For eager phase planning (compare with successor)
- [Work Item Creation](work-item-creation.md) - Creating successor work items
- [Handover](handover.md) - Transitioning between duties
- [Comment Patterns](comment-patterns.md) - Standard comment formats

---

## Testing

**Test Scenarios**: `.team/procedures/tests/work-item-successor/`

**Key Scenarios**:
1. Creating successor with parent (happy path)
2. Creating successor without parent (standalone)
3. Working on successor (reading predecessor context)
4. Successor chain (multiple successors)
5. Successor with different duty type
