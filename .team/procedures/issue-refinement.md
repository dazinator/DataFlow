# Issue Refinement Procedure

**Purpose**: Detect and refine multi-phase plan issues into properly structured sub-issues

**Layer**: 1 (Global Procedure)

**Used By**: Implementation Duty, Product Prioritization Duty

---

## Overview

This procedure helps detect when a work item represents a multi-phase plan (epic/feature) rather than a single implementable unit, and guides the refinement process to split it into logical sub-issues.

**Key Principle**: Work items selected for implementation should be "ready" - meaning they represent a single, implementable phase that can be completed in one PR. Multi-phase plans need to be broken down before implementation can begin.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction layer
- **[Multi-Phase Work Items](multi-phase-work-items.md)** - Creating and managing multi-phase structures
- **[Work Item Creation](work-item-creation.md)** - Creating work items correctly

**Semantic Operations Used**:
- `get_work_item_details(work_item_id)` - Retrieve work item information
- `list_child_work_items(work_item_id)` - Get existing sub-issues
- `create_child_work_item(parent_id, type, title, description, duty)` - Create sub-issue
- `update_work_item(work_item_id, fields)` - Update work item fields
- `add_work_item_comment(work_item_id, text)` - Add comment

---

## When to Use This Procedure

Use this procedure when:
1. **Before implementation**: Check if work item is ready to implement
2. **During backlog prioritization**: Ensure backlog items are properly structured
3. **When uncertain**: Work item seems too large or contains multiple phases

**Entry Points**:
- Implementation Duty Step 2.5 (pre-flight check)
- Product Prioritization Duty Step 2.5 (housekeeping)
- Manual refinement requests

---

## Procedure

### Step 1: Analyze Work Item for Multi-Phase Indicators

Retrieve the work item and check for multi-phase indicators:

```python
# Get work item details
details = get_work_item_details(work_item_id)
title = details['title']
description = details['description']
```

**Multi-Phase Indicators** (check description for):

1. **Explicit multi-phase plan sections**:
   - "## Multi-Phase Plan"
   - "## Phases"
   - "## Implementation Plan" with multiple phases

2. **Multiple deliverable sections**:
   - Multiple "## Phase N:" sections
   - Numbered phases in checklist
   - Sequential "Part 1", "Part 2", etc.

3. **Large scope indicators**:
   - Contains phrases like "epic", "feature set", "multi-part"
   - Multiple major features listed
   - Dependencies between components

4. **Duration estimates**:
   - Mentions "multiple PRs"
   - Estimated effort > 1 week
   - References "phases" or "iterations"

**Decision**:
- **If 2+ indicators present** → Likely multi-phase, proceed to Step 2
- **If 0-1 indicators** → Single-phase, skip refinement
- **If uncertain** → Ask reviewer in Step 4

### Step 2: Check Existing Sub-Issues

Check if work item already has sub-issues:

```python
# Check for existing children
children = list_child_work_items(work_item_id)

if children:
    # Work item already has sub-issues
    print(f"Found {len(children)} existing sub-issues")
    # Proceed to Step 3 for validation
else:
    # No sub-issues yet
    print("No sub-issues found - needs refinement")
    # Proceed to Step 3 to create structure
```

### Step 3: Extract Phase Structure

Analyze the description to identify phases:

**Extraction Pattern 1: Explicit Phase Sections**
```markdown
## Phase 1: Foundation
**Deliverables**: Core classes
**Dependencies**: None
...

## Phase 2: Integration
**Deliverables**: Integration layer
**Dependencies**: Phase 1 complete
...
```

**Extraction Pattern 2: Checklist Format**
```markdown
## Implementation Plan
- [ ] Phase 1: Foundation - Create core classes
- [ ] Phase 2: Integration - Build integration layer
- [ ] Phase 3: Testing - Add comprehensive tests
```

**Extraction Pattern 3: Multi-Part Format**
```markdown
## Part 1: Core Functionality
[Details...]

## Part 2: Advanced Features
[Details...]
```

**Extract for each phase**:
- Phase number/order
- Phase name/title
- Phase description/deliverables
- Dependencies (what must complete first)
- Success criteria
- Duty assignment (typically same as parent work item, or inferred from phase type)

### Step 4: Present Refinement Plan to Reviewer

Before creating any sub-issues, present the refinement plan for approval:

```python
refinement_comment = f"""[Copilot-Duty: {current_duty}] 🔍 **Multi-Phase Plan Detected**

## Analysis

This work item appears to represent a multi-phase plan rather than a single implementable unit.

**Indicators Found**:
- {indicator_1}
- {indicator_2}
- {indicator_3}

## Detected Phases

{phase_count} phases identified:

### Phase 1: {phase1_name}
**Deliverables**: {phase1_deliverables}
**Dependencies**: {phase1_dependencies}

### Phase 2: {phase2_name}
**Deliverables**: {phase2_deliverables}
**Dependencies**: {phase2_dependencies}

[... list all detected phases ...]

## Proposed Refinement

I will create {phase_count} sub-issues:
- #{work_item_id}-1: [Phase 1] {phase1_title}
- #{work_item_id}-2: [Phase 2] {phase2_title}
[... list all proposed sub-issues ...]

Each sub-issue will:
- Link to this parent issue
- Include phase-specific requirements
- Be assigned to appropriate duty ({proposed_duty})
- Maintain dependencies between phases

## Next Steps

**To proceed with refinement**, reply with:
- `@copilot proceed with refinement` - Create the sub-issues as proposed
- `@copilot adjust phases [instructions]` - Modify the phase breakdown

**To skip refinement**, reply with:
- `@copilot skip refinement` - Treat this as single-phase work item
"""

add_work_item_comment(
    work_item_id=work_item_id,
    text=refinement_comment
)
```

**Wait for reviewer approval before proceeding to Step 5.**

### Step 5: Create Sub-Issues (After Approval)

**Only execute after reviewer approves in Step 4.**

Create child work items for each phase:

```python
# Update parent to mark as multi-phase plan
parent_description = f"""## Multi-Phase Plan

⚠️ **This is a parent issue for a multi-phase plan.**

Work is split into the following sub-issues:

{phase_list_with_checkboxes}

## Original Description

{original_description}
"""

update_work_item(
    work_item_id=work_item_id,
    description=parent_description
)

# Create sub-issue for each phase
sub_issue_ids = []

# Determine duty for each phase
# - Typically inherit from parent work item
# - Can be overridden based on phase content (e.g., research vs implementation)
# - If parent is in product-backlog, sub-issues also go to product-backlog
# - If parent is in implementation, sub-issues also go to implementation

for phase in phases:
    sub_issue_id = create_child_work_item(
        parent_id=work_item_id,
        type=phase['type'],  # Usually same as parent
        title=f"[Phase {phase['number']}] {parent_title} - {phase['name']}",
        description=f"""**Parent Issue**: #{work_item_number}

## Phase {phase['number']}: {phase['name']}

**Deliverables**: {phase['deliverables']}
**Dependencies**: {phase['dependencies']}
**Success Criteria**: {phase['success_criteria']}

## Detailed Requirements

{phase['details']}

---

**Note**: This is Phase {phase['number']} of {total_phases}. 
See parent issue #{work_item_number} for overall plan.
""",
        duty=phase['duty'],  # Determined from parent or phase-specific analysis
        labels=phase.get('labels', [])
    )
    
    sub_issue_ids.append(sub_issue_id)
    
    # Add comment linking to parent
    add_work_item_comment(
        work_item_id=sub_issue_id,
        text=f"[Copilot-Duty: {current_duty}] Created as Phase {phase['number']} of parent #{work_item_number}"
    )
```

### Step 6: Update Parent with Sub-Issue Links

After all sub-issues created, update parent with issue numbers:

```python
# Get sub-issue numbers
sub_issue_numbers = []
for sub_id in sub_issue_ids:
    details = get_work_item_details(sub_id)
    sub_issue_numbers.append(details['number'])

# Update parent description with links
phase_checklist = "\n".join([
    f"- [ ] Phase {i+1}: {phases[i]['name']} (#{sub_issue_numbers[i]})"
    for i in range(len(phases))
])

updated_description = parent_description.replace(
    "{phase_list_with_checkboxes}",
    phase_checklist
)

update_work_item(
    work_item_id=work_item_id,
    description=updated_description
)
```

### Step 7: Provide Next Steps Guidance

Add completion comment with next steps:

```python
completion_comment = f"""[Copilot-Duty: {current_duty}] ✅ **Issue Refinement Complete**

## Refinement Summary

This multi-phase plan has been split into {len(sub_issue_ids)} sub-issues:

{sub_issue_list_with_links}

## Structure

**Parent Issue**: This issue (#{work_item_number})
- Tracks overall multi-phase plan
- Remains open until all phases complete
- Stays in {parent_duty} duty

**Sub-Issues**: Individual phases
- Each represents a single implementable unit
- Each will have its own PR
- Assigned to {phase_duty} duty

## Next Steps for Implementation

{next_steps_guidance}

---

**For Implementation**: Start with Phase 1 (#{first_sub_issue_number}). 
Do not work on this parent issue directly.

See [Multi-Phase Work Items Procedure](../.team/procedures/multi-phase-work-items.md) for complete guidance.
"""

add_work_item_comment(
    work_item_id=work_item_id,
    text=completion_comment
)
```

### Step 8: Return Refinement Status

Return status to calling duty:

```python
return {
    "refined": True,
    "parent_id": work_item_id,
    "sub_issue_ids": sub_issue_ids,
    "first_phase_id": sub_issue_ids[0],
    "next_action": "work_on_first_phase"  # or "close_pr_and_link"
}
```

---

## Duty-Specific Integration

### For Implementation Duty (Pre-Flight Check)

When called from Implementation Duty Step 2.5:

```python
# Check if issue needs refinement
refinement_result = check_issue_refinement(work_item_id)

if refinement_result['refined']:
    # Issue was refined into sub-issues
    first_phase_id = refinement_result['first_phase_id']
    
    # Add comment explaining situation
    add_work_item_comment(
        work_item_id=work_item_id,
        text=f"""[Copilot-Duty: Implementation] 🔄 **Issue Refined**

This issue has been split into sub-issues for each phase.

**First Phase**: #{first_phase_number}

I will close this PR as work should continue in a new PR for Phase 1.
"""
    )
    
    # Stop implementation on parent
    # Close PR (if exists)
    # Suggest creating PR for first sub-issue instead
    
elif refinement_result['needs_approval']:
    # Presented refinement plan, waiting for approval
    # Pause implementation until refinement approved/rejected
    pass
    
else:
    # No refinement needed, proceed with implementation
    pass
```

### For Product Prioritization Duty (Housekeeping)

When called from Product Prioritization Duty Step 2.5:

```python
# Refine all backlog items before prioritization
backlog_items = query_work_items_by_duty(duty="product-backlog")

refined_items = []
for item in backlog_items:
    refinement_result = check_issue_refinement(item['id'])
    
    if refinement_result['refined']:
        refined_items.append(refinement_result)

# After refinement, re-query backlog to include new sub-issues
backlog_items = query_work_items_by_duty(duty="product-backlog")

# Now prioritize including the refined sub-issues
```

---

## Examples

### Example 1: Multi-Phase Plan with Explicit Phases

**Input Work Item**:
```markdown
Title: Add User Authentication System

## Multi-Phase Plan

### Phase 1: Core Authentication
**Deliverables**: JWT generation, login/logout
**Dependencies**: None
**Success Criteria**: Users can log in

### Phase 2: Session Management
**Deliverables**: Session storage, refresh tokens
**Dependencies**: Phase 1 complete
**Success Criteria**: Sessions persist correctly

### Phase 3: Security Hardening
**Deliverables**: Rate limiting, audit logging
**Dependencies**: Phases 1-2 complete
**Success Criteria**: System resistant to attacks
```

**Refinement Output**:
- **Parent**: Issue #100 (updated with sub-issue links)
- **Sub-Issue 1**: #101 - [Phase 1] User Authentication - Core Authentication
- **Sub-Issue 2**: #102 - [Phase 2] User Authentication - Session Management
- **Sub-Issue 3**: #103 - [Phase 3] User Authentication - Security Hardening

### Example 2: Already Refined Issue

**Input Work Item**:
```markdown
Title: Add User Authentication System

## Multi-Phase Plan
- [ ] Phase 1: Core Authentication (#101)
- [ ] Phase 2: Session Management (#102)
- [ ] Phase 3: Security Hardening (#103)
```

**Refinement Output**:
```python
{
    "refined": False,  # Already refined
    "has_sub_issues": True,
    "sub_issue_count": 3,
    "needs_validation": True  # Validate sub-issues match phases
}
```

### Example 3: Single-Phase Issue (No Refinement)

**Input Work Item**:
```markdown
Title: Fix null reference in TransformBlock

## Description
TransformBlock throws NullReferenceException when input is null.

## Steps to Reproduce
1. Create TransformBlock
2. Pass null input
3. Observe exception

## Expected Behavior
Should handle null gracefully
```

**Refinement Output**:
```python
{
    "refined": False,
    "multi_phase": False,
    "reason": "Single bug fix - no phase indicators"
}
```

---

## Edge Cases

### Case 1: Sub-Issues Exist But Don't Match Phases

**Scenario**: Work item has sub-issues but they don't align with phases in description

**Resolution**:
1. Compare existing sub-issues with detected phases
2. Present comparison to reviewer
3. Suggest creating missing sub-issues or updating existing ones
4. Ask for approval before making changes

### Case 2: Ambiguous Phase Boundaries

**Scenario**: Description mentions phases but boundaries are unclear

**Resolution**:
1. Extract best guess at phase structure
2. Flag uncertainty in refinement plan
3. Ask reviewer to clarify phase breakdown
4. Proceed only after clarification

### Case 3: Single Large Task (Not Multi-Phase)

**Scenario**: Large work item but logically a single unit

**Resolution**:
1. Check for true phase indicators (sequential dependencies)
2. If no sequential dependencies → single phase
3. If just large scope → consider breaking by feature area instead
4. Present analysis to reviewer

---

## Anti-Patterns

### ❌ Don't: Create Sub-Issues Without Reviewer Approval

```python
# Wrong - creating sub-issues immediately
phases = extract_phases(description)
for phase in phases:
    create_child_work_item(...)  # WRONG - no approval yet
```

✅ **Correct**: Present refinement plan and wait for approval

### ❌ Don't: Over-Refine Single-Phase Work

```python
# Wrong - treating every work item as multi-phase
if len(description) > 500:  # Arbitrary size check
    # Don't auto-refine based on size alone
```

✅ **Correct**: Look for actual phase indicators, not just size

### ❌ Don't: Use Platform-Specific Operations

```python
# Wrong - using GitHub sub-issue API
sub_issue_write(method="add", ...)  # ❌ Platform-specific
```

✅ **Correct**: Use semantic operation `create_child_work_item()`

---

## Success Criteria

- [ ] Multi-phase indicators correctly identified
- [ ] Existing sub-issues validated
- [ ] Phase structure accurately extracted
- [ ] Refinement plan presented to reviewer
- [ ] Sub-issues created only after approval
- [ ] Parent updated with sub-issue links
- [ ] Next steps guidance provided
- [ ] Semantic operations used exclusively

---

## Related Procedures

- [Multi-Phase Work Items](multi-phase-work-items.md) - Managing parent-child relationships
- [Work Item Creation](work-item-creation.md) - Creating work items correctly
- [Duty Assignment](duty-assignment.md) - Assigning duties to phases
- [Comment Patterns](comment-patterns.md) - Standard comment formats

---

## Testing

**Test Scenarios**: `.team/procedures/tests/issue-refinement/`

**Key Scenarios**:
1. Multi-phase plan needing refinement (explicit phases)
2. Already refined multi-phase plan (validation)
3. Single-phase issue (no refinement needed)
4. Ambiguous phase boundaries (edge case)
5. Sub-issues exist but incomplete (partial refinement)
