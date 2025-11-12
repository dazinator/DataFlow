# Multi-Phase Issue Management Procedures

This document provides standardized procedures for managing multi-phase issues across all workflows.

## Overview

Multi-phase issues involve a parent issue that tracks an overall plan with multiple sub-issues representing individual phases. This pattern is common for:
- Large implementations split into incremental phases
- Research projects with multiple exploration stages
- Process improvements with staged rollout

**Key Problems Solved:**
1. Parent issues going out of date as sub-issues complete
2. Parent issues remaining open after all sub-issues complete
3. Loss of context when working on individual sub-issues
4. Manual toil cleaning up stale parent issues

## When to Use Multi-Phase Issues

Create a multi-phase structure when:
- Work naturally divides into 3+ sequential phases
- Each phase has clear deliverables and can be reviewed independently
- Phases build on each other (not independent tasks)
- Total effort exceeds what's reasonable for a single PR

**Don't use for:**
- Independent tasks that happen to relate to the same feature (use labels instead)
- Work that could reasonably fit in a single PR
- Phases that don't have clear boundaries

## Creating Multi-Phase Issues

### 1. Create Parent Issue

The parent issue describes the overall plan and tracks all phases:

```markdown
# [Feature/Project Name]

## Overview
[Brief description of the overall goal]

## Multi-Phase Plan

- [ ] Phase 1: [Name] (#issue-number) - [Brief description]
- [ ] Phase 2: [Name] (#issue-number) - [Brief description]  
- [ ] Phase 3: [Name] (#issue-number) - [Brief description]

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
```

### 2. Create Sub-Issues for Each Phase

For each phase, create a separate issue with:
- Clear title: "[Phase N] [Feature Name] - [Phase Name]"
- Link to parent issue in description
- Specific deliverables for this phase only
- Appropriate workflow label

### 3. Link Sub-Issues to Parent

Use GitHub's sub-issue feature:

```python
# For each sub-issue
sub_issue_write(
    method="add",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=PARENT_ISSUE_NUMBER,
    sub_issue_id=SUB_ISSUE_ID  # ID, not number
)
```

## Working on Sub-Issues

### Step 1: Check for Parent Issue (ALWAYS)

At the start of work on ANY issue, check if it's a sub-issue:

```python
# Get current issue details
issue = issue_read(
    method="get",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=CURRENT_ISSUE_NUMBER
)

# Check if parent exists
if issue.parent:
    parent_number = issue.parent.number
    # This is a sub-issue - proceed to Step 2
else:
    # Standalone issue - proceed with normal workflow
```

**When to check:** At the very beginning of workflow execution, right after workflow label verification.

### Step 2: Read Parent Context

If parent exists, read it to understand the overall plan:

```python
parent = issue_read(
    method="get",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number
)

# Review parent description for:
# - Overall goal
# - Which phase current issue represents
# - What phases came before (context)
# - What phases come after (roadmap)
```

**Use this context to:**
- Understand how current work fits into bigger picture
- Align implementation with overall plan
- Make informed decisions about scope and design

### Step 3: Update Parent as Work Progresses

When reporting progress on the sub-issue, also update parent:

```python
# After calling report_progress for current issue
# Update parent issue description to reflect progress

# Example: Update phase status in parent description
parent_body = parent.description

# Find and update the line for current phase
# From: - [ ] Phase 2: Core Features (#122) - NOT STARTED
# To:   - [ ] Phase 2: Core Features (#122) - Foundation complete, tests passing

issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number,
    body=updated_parent_body
)
```

**When to update parent:**
- After significant milestones within the phase
- When reporting progress on the sub-issue
- Before marking sub-issue complete

**What to update:**
- Phase checklist item status
- Brief progress notes (keep concise)
- Any blockers or dependencies discovered

### Step 4: Close Parent When Completing Last Sub-Issue

Before completing work, determine if this is the last sub-issue:

```python
# Get all sub-issues of parent
parent_data = issue_read(
    method="get_sub_issues",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number
)

# Count open sub-issues (excluding current one)
other_open = [s for s in parent_data.children 
              if s.state == "open" and s.number != CURRENT_ISSUE_NUMBER]

is_last_subissue = len(other_open) == 0
```

**If this IS the last sub-issue:**

1. Update parent description with final status:
   ```python
   # Mark all phases complete in parent description
   # Add completion note
   ```

2. Include parent closure in PR description:
   ```markdown
   Fixes #CURRENT_ISSUE_NUMBER
   Fixes #PARENT_ISSUE_NUMBER
   ```

3. When PR merges:
   - Sub-issue closes automatically
   - Parent issue closes automatically
   - No manual cleanup needed

**If this is NOT the last sub-issue:**
- Only include current issue in PR: `Fixes #CURRENT_ISSUE_NUMBER`
- Parent remains open for remaining phases

## Workflow-Specific Guidance

### Implementation Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup on Entry" section

**When:** Before reading handover or requirements

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Note which phase you're implementing
4. Proceed with normal implementation workflow

### Research Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup on Entry" section

**When:** Before creating research folder

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Understand how this research fits into overall plan
4. Create research handover that acknowledges multi-phase context

### Process Modeling Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup on Entry" section

**When:** Before updating plan.md

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Update plan.md to note multi-phase context
4. Update parent as scenarios are validated

### Tech Debt Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup on Entry" section

**When:** Before discovery work

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Note if debt analysis is part of larger investigation
4. Update parent with findings

### Product Prioritization Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup on Entry" section

**When:** Before prioritization analysis

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Understand if prioritization is part of larger backlog initiative
4. Update parent with prioritization decisions

### Triage Workflow

Insert multi-phase check at workflow start:

**Location:** Right after "Label Cleanup" section

**When:** Before triage assessment

**Action:**
1. Check for parent issue
2. Read parent context if exists
3. Note if triage is part of larger assessment plan (e.g., bulk triage)
4. Update parent with triage progress

## Examples

### Example 1: Three-Phase Implementation

**Parent Issue #200: "Add Metrics System"**
```markdown
## Multi-Phase Plan

- [x] Phase 1: Metrics Infrastructure (#201) - COMPLETE
- [x] Phase 2: Core Metrics (#202) - COMPLETE
- [ ] Phase 3: Advanced Metrics (#203) - IN PROGRESS

[Phase descriptions...]
```

**Agent assigned to #203:**
1. Checks for parent: Finds #200
2. Reads parent: Understands Phases 1-2 are complete, this is Phase 3
3. Works on Phase 3
4. Updates parent: "Phase 3: Advanced Metrics (#203) - Timer metrics implemented"
5. Completes work
6. Checks: This is last sub-issue
7. PR includes: `Fixes #203` and `Fixes #200`
8. PR merges: Both issues close automatically

### Example 2: Middle Phase

**Agent assigned to #202 (Phase 2 of 3):**
1. Checks for parent: Finds #200
2. Reads parent: Understands Phase 1 complete, Phase 3 not started
3. Works on Phase 2
4. Updates parent: "Phase 2: Core Metrics (#202) - Counter metrics complete"
5. Completes work
6. Checks: NOT last sub-issue (#203 still open)
7. PR includes: `Fixes #202` only
8. PR merges: #202 closes, #200 stays open for Phase 3

### Example 3: Standalone Issue

**Agent assigned to #250:**
1. Checks for parent: None found
2. Proceeds with normal workflow (no parent tracking)
3. PR includes: `Fixes #250`

## Common Patterns

### Sequential Phases (Typical)
```
Phase 1 → Phase 2 → Phase 3
```
Each phase builds on the previous. Complete in order.

### Parallel Phases (Rare)
```
      ┌─ Phase 2a ─┐
Phase 1            Phase 4
      └─ Phase 2b ─┘
```
Avoid if possible. Complex to track. Consider separate parent issues.

### Discovery + Implementation
```
Phase 1: Research/Discovery → Phase 2: Implementation
```
Common pattern. Research phase creates handover for implementation phase.

## Troubleshooting

### Parent Issue Out of Date

**Symptom:** Parent description doesn't reflect current state

**Fix:**
1. Read all sub-issues to understand current state
2. Update parent description manually
3. Add note: "Updated by @copilot on [date] - synced with sub-issue status"

### Last Sub-Issue Didn't Close Parent

**Symptom:** All sub-issues closed but parent still open

**Root cause:** PR didn't include `Fixes #PARENT`

**Fix:**
1. Manually close parent issue
2. Add comment: "All sub-issues complete. Closing manually."

### Unclear Which Phase

**Symptom:** Can't determine which phase current issue represents

**Fix:**
1. Check parent description for phase list
2. If unclear, check sub-issue creation order
3. Ask in issue comments if still unclear

## Best Practices

### DO
- ✅ Check for parent issue at start of ALL workflows
- ✅ Keep parent description current
- ✅ Include parent context in decision-making
- ✅ Close parent automatically when completing last sub-issue
- ✅ Keep phase descriptions clear and specific

### DON'T  
- ❌ Skip parent check (it's quick for standalone issues)
- ❌ Let parent description drift out of date
- ❌ Close parent manually unless automated closure failed
- ❌ Create too many phases (3-5 is typical, 7+ is too many)
- ❌ Use multi-phase for independent tasks

## Implementation Checklist

When adding multi-phase procedures to a workflow:

- [ ] Add parent check section after label cleanup
- [ ] Add guidance on reading parent context
- [ ] Add guidance on updating parent during work
- [ ] Add guidance on closing parent for last sub-issue
- [ ] Add examples specific to the workflow
- [ ] Test with scenarios
- [ ] Update workflow navigation if needed

---

**See Also:**
- [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) - For workflow transitions
- [Implementation Duty](/.team/duties/IMPLEMENTATION_DUTY.md) - Implementation-specific guidance
- [Research Duty](/.team/duties/RESEARCH_DUTY.md) - Research-specific guidance
- [Multi-Phase Work Items Procedure](/.team/procedures/multi-phase-work-items.md) - Complete multi-phase guidance
