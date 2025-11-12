# Work Item Creation Procedure

**Purpose**: Common patterns for creating work items correctly

**Layer**: 1 (Global Procedure)

**Used By**: All duties (Layer 2)

---

## Overview

Creating work items is a fundamental operation across all duties. This procedure defines standard patterns for creating different types of work items with correct duty assignment and required information.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `create_work_item(type, title, description, duty, labels, assignee)` - Create work item
- `create_child_work_item(parent_id, type, title, description, duty)` - Create child work item
- `get_work_item_details(work_item_id)` - Get work item information

**Work Item Fields**:
- `type` - Work item type ("research", "implementation", "bug", etc.)
- `title` - Clear, descriptive title
- `description` - Detailed description with context
- `duty` - Duty assignment
- `labels` - Additional categorization
- `assignee` - Optional assignee

---

## Common Patterns

### Pattern 1: Research Work Item

**When**: Need to investigate approach before implementation

```python
work_item_id = create_work_item(
    type="research",
    title="Investigate [Technology/Approach] for [Purpose]",
    description="""
## Research Goal
[What question needs answering]

## Context
[Why this research is needed]

## Success Criteria
- [ ] Approach validated or invalidated
- [ ] Trade-offs documented
- [ ] Recommendation made
- [ ] Handover created for implementation (if approved)

## Scope
[What's in scope, what's out of scope]
""",
    duty="research",
    labels=["investigation"]
)
```

**Key Elements**:
- Title clearly states what's being investigated
- Research goal is specific
- Success criteria includes handover creation
- Scope boundaries defined

### Pattern 2: Implementation Work Item

**When**: Building a solution (from research or direct requirements)

```python
work_item_id = create_work_item(
    type="implementation",
    title="Implement [Feature/Component]",
    description="""
## Overview
[What is being implemented]

## Requirements
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

## Design Reference
[Link to design doc or research handover]

## Success Criteria
- [ ] Feature working as specified
- [ ] Tests passing
- [ ] Documentation updated
- [ ] Code reviewed
""",
    duty="implementation",
    labels=["feature"]  # or "bug-fix", "enhancement", etc.
)
```

**Key Elements**:
- Title describes what's being built
- Requirements clearly listed
- Design reference provided (if available)
- Success criteria includes tests and documentation

### Pattern 3: Process Modeling Work Item

**When**: Improving workflows, documentation, or processes

```python
work_item_id = create_work_item(
    type="process-modeling",
    title="[Update/Create] [Workflow/Document Name]",
    description="""
## Change Objective
[What process improvement is needed]

## Current State
[What exists today and why it's insufficient]

## Proposed Changes
- [ ] Change 1
- [ ] Change 2
- [ ] Change 3

## Testing Plan
- [ ] Tabletop scenarios created
- [ ] Leak detection run
- [ ] Graph updated

## Success Criteria
- [ ] Changes implemented
- [ ] Tests passing (>90%)
- [ ] Documentation updated
""",
    duty="process-modeling",
    labels=["workflow-improvement"]
)
```

**Key Elements**:
- Title specifies what's being changed
- Current state explains the problem
- Testing plan follows change procedure
- Success criteria includes leak detection

### Pattern 4: Tech Debt Work Item

**When**: Code quality improvement needed

```python
work_item_id = create_work_item(
    type="tech-debt",
    title="[Refactor/Cleanup] [Component/Area]",
    description="""
## Debt Description
[What technical debt exists]

## Impact
[Why this debt matters - performance, maintainability, etc.]

## Proposed Resolution
[How to address the debt]

## Estimated Effort
[Small/Medium/Large]

## Success Criteria
- [ ] Debt resolved
- [ ] Tests still passing
- [ ] No regression introduced
""",
    duty="tech-debt",
    labels=["code-quality"]
)
```

**Key Elements**:
- Title describes what needs refactoring
- Impact explains why it matters
- Estimated effort helps prioritization
- Success criteria includes regression testing

### Pattern 5: Handover Work Item (Duty Transition)

**When**: Transitioning work to another duty

See [Handover Procedure](handover.md) for complete details.

```python
work_item_id = create_work_item(
    type="implementation",  # Type of work being handed over
    title="Implement [Feature] (from Research)",
    description="""
**Research Handover**: [Link to research folder/document]

## Research Summary
[Brief summary of research findings]

## Recommended Approach
[What research validated]

## Implementation Requirements
[What needs to be built]

## Success Criteria
[How to know implementation is complete]
""",
    duty="implementation",  # Duty receiving the work
    labels=["from-research"]
)
```

**Key Elements**:
- Title indicates handover source
- Research handover linked
- Approach clearly stated
- Requirements extracted from research

---

## Multi-Phase Work Item Creation

For multi-phase plans, see [Multi-Phase Work Items Procedure](multi-phase-work-items.md).

**Quick Reference**:

```python
# 1. Create parent
parent_id = create_work_item(
    type="plan",
    title="[Feature/Project Name]",
    description="[Multi-phase plan structure]",
    duty="product-backlog"
)

# 2. Create children
phase1_id = create_child_work_item(
    parent_id=parent_id,
    type="research",
    title="[Phase 1] [Feature] - [Phase Name]",
    description="[Phase-specific details]",
    duty="research"
)
```

---

## Title Conventions

### Good Titles

✅ **Specific and actionable**:
- "Implement user authentication with JWT"
- "Investigate caching strategies for data pipeline"
- "Refactor BatchBlock memory pooling"
- "Update implementation workflow with testing guidance"

✅ **Includes context**:
- "[Phase 2] Add User Authentication - Session Management"
- "[Bug] Fix null reference in TransformBlock"
- "[Urgent] Update security dependencies"

### Poor Titles

❌ **Vague**:
- "Fix the bug"
- "Improve performance"
- "Update workflow"

❌ **Too generic**:
- "Research"
- "Implementation"
- "Cleanup"

---

## Description Best Practices

### Structure

Use consistent structure for each work item type:

1. **Overview/Goal** - What this work item is about
2. **Context/Background** - Why it's needed
3. **Requirements/Details** - What needs to be done (checklist)
4. **Success Criteria** - How to know it's complete
5. **References** - Links to related docs, handovers, etc.

### Formatting

- Use markdown formatting for readability
- Use checklists for requirements/tasks
- Include links to related work items, docs, handovers
- Keep descriptions focused and scannable

### Context Inclusion

**Always include**:
- Why this work is needed
- Links to parent issues (if multi-phase)
- References to research/design docs
- Success criteria

**Avoid**:
- Platform-specific implementation details
- Copy-pasting large code blocks
- Redundant information already in linked docs

---

## Examples

### Example 1: Creating Research Work Item

```python
work_item_id = create_work_item(
    type="research",
    title="Investigate distributed caching for pipeline blocks",
    description="""
## Research Goal
Evaluate distributed caching solutions (Redis, Memcached) for sharing state between pipeline blocks across processes.

## Context
Current in-memory caching doesn't work in distributed scenarios. Need solution for multi-process pipelines.

## Success Criteria
- [ ] Redis and Memcached evaluated
- [ ] Performance benchmarked
- [ ] Trade-offs documented
- [ ] Recommendation made
- [ ] Implementation handover created (if approved)

## Scope
**In Scope**: Redis, Memcached, serialization overhead
**Out of Scope**: Custom distributed cache implementation
""",
    duty="research",
    labels=["performance", "distributed"]
)
```

### Example 2: Creating Implementation Work Item from Research

```python
# After research complete, create implementation work item
work_item_id = create_work_item(
    type="implementation",
    title="Implement Redis caching for distributed pipelines",
    description="""
**Research Handover**: /research/distributed-caching/

## Research Summary
Research validated Redis as best approach for distributed pipeline caching. See handover for detailed evaluation.

## Recommended Approach
- Use StackExchange.Redis client
- Implement IDistributedCache wrapper
- Add cache configuration to pipeline builder

## Implementation Requirements
- [ ] StackExchange.Redis integration
- [ ] IDistributedCache wrapper implementation
- [ ] Pipeline builder cache configuration
- [ ] Unit tests for cache operations
- [ ] Integration tests for distributed scenarios
- [ ] Documentation updates

## Success Criteria
- [ ] Distributed pipelines can share state via Redis
- [ ] All tests passing
- [ ] Performance acceptable (<10ms cache operations)
- [ ] Documentation complete
""",
    duty="implementation",
    labels=["feature", "distributed"]
)
```

### Example 3: Creating Process Modeling Work Item

```python
work_item_id = create_work_item(
    type="process-modeling",
    title="Add null handling guidance to implementation workflow",
    description="""
## Change Objective
Implementation workflow missing guidance on null value handling patterns, causing inconsistent null checks in code.

## Current State
Implementation workflow has general coding standards but no specific null handling guidance. Discovered during #456 implementation.

## Proposed Changes
- [ ] Add null handling section to implementation workflow
- [ ] Include examples of null checking patterns
- [ ] Add null handling to code review checklist
- [ ] Create tabletop scenarios for testing

## Testing Plan
- [ ] Create 3 test scenarios (null in input, null in config, optional null)
- [ ] Run tabletop tests
- [ ] Run leak detection
- [ ] Update graph

## Success Criteria
- [ ] Guidance added to implementation workflow
- [ ] Test scenarios passing (>90%)
- [ ] Zero leaks detected
- [ ] Graph updated
""",
    duty="process-modeling",
    labels=["workflow-improvement"]
)
```

---

## Edge Cases

### Case 1: Uncertain About Duty Assignment

**Scenario**: Not sure if work should be research or implementation

**Resolution**:
1. If validation needed → Research duty
2. If requirements clear → Implementation duty
3. If truly uncertain → Triage duty (let triage decide)

### Case 2: Work Item Type Doesn't Fit Standard Patterns

**Scenario**: Work doesn't clearly fit "research", "implementation", etc.

**Resolution**:
1. Use closest matching type
2. Clarify in description why this type was chosen
3. Use labels for additional categorization

### Case 3: Multiple Related Work Items Needed

**Scenario**: Need to create several related work items

**Resolution**:
1. If sequential phases → Use multi-phase pattern
2. If independent tasks → Create separate work items with shared labels
3. If parent-child relationship → Use `create_child_work_item()`

---

## Anti-Patterns

### ❌ Don't: Create Work Items Without Clear Duty Assignment

```python
# Wrong - no duty specified
work_item_id = create_work_item(
    type="implementation",
    title="Implement feature X",
    description="...",
    duty=None  # WRONG - every work item needs a duty
)
```

✅ **Correct**: Always assign appropriate duty

### ❌ Don't: Skip Success Criteria

```python
# Wrong - no success criteria
description = """
## Overview
Implement feature X

## Requirements
- Build feature X
"""
# Missing: How to know when it's complete?
```

✅ **Correct**: Always include success criteria

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong - using GitHub-specific API
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="...",
    labels=["workflow:research"]
)
```

✅ **Correct**: Use semantic operation `create_work_item()`

---

## Success Criteria

- [ ] Work item type matches work nature
- [ ] Title is clear and specific
- [ ] Description includes overview, context, requirements, success criteria
- [ ] Duty correctly assigned
- [ ] Labels used appropriately
- [ ] Multi-phase structure used if applicable
- [ ] Semantic operations used exclusively

---

## Related Procedures

- [Duty Assignment](duty-assignment.md) - Choosing correct duty
- [Multi-Phase Work Items](multi-phase-work-items.md) - Creating multi-phase structures
- [Handover](handover.md) - Creating handover work items
- [Comment Patterns](comment-patterns.md) - Standard comment formats

---

## Testing

**Test Scenarios**: `/research/workflow-modeling/scenarios/procedures/work-item-creation/`

**Key Scenarios**:
1. Creating research work item (happy path)
2. Creating implementation work item from research
3. Creating multi-phase parent and children
4. Creating process modeling work item
5. Creating handover work item
