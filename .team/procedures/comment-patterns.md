# Comment Patterns Procedure

**Purpose**: Standard comment formats and conventions for work items

**Layer**: 1 (Global Procedure)

**Used By**: All duties (Layer 2)

---

## Overview

Consistent comment patterns make work item history clear and searchable. This procedure defines standard comment formats for common scenarios.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `add_work_item_comment(work_item_id, text)` - Add comment to work item

**Comment Components**:
- Prefix - Identifies duty and comment type
- Emoji - Visual indicator of comment category
- Content - Structured information

---

## Comment Prefix Convention

**⚠️ REQUIRED**: All comments from duties MUST start with duty prefix.

**Format**: `[Copilot-Duty: {duty-name}] {emoji} {content}`

**Examples**:
- `[Copilot-Duty: research] 📊 Research findings documented in /research/caching/`
- `[Copilot-Duty: implementation] ✅ All tests passing, ready for review`
- `[Copilot-Duty: process-modeling] 🔄 Workflow updated per feedback`

**Purpose**:
- Confirms duty label was checked before proceeding
- Makes comment source clear in work item history
- Enables filtering/searching by duty
- Audit trail for duty transitions

---

## Standard Comment Patterns

### Pattern 1: Duty Assignment Comment

**When**: Assigning or changing duty designation

**Format**:
```
[Copilot-Duty: {duty}] 🎯 Duty assignment: {reason}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: research] 🎯 Duty assignment: Assigned to research based on investigation nature of work."
)
```

### Pattern 2: Label Cleanup Comment

**When**: Removing conflicting labels or fixing label pollution

**Format**:
```
[Copilot-Duty: {duty}] 🏷️ Label cleanup: {what was changed and why}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: implementation] 🏷️ Label cleanup: Removed conflicting `workflow:research` label. This work item is correctly in implementation duty."
)
```

### Pattern 3: Handover Comment

**When**: Transitioning work to another duty

**Format**:
```
[Copilot-Duty: {source-duty}] 🔄 Handover: {source} → {target}

{handover details}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=research_work_item_id,
    text="""[Copilot-Duty: research] 🔄 Handover: research → implementation

Research complete. Implementation work item created: #789

**Handover Document**: /research/caching-strategy/handover/implementation-handover.md

**Recommendation**: Use Redis with StackExchange.Redis client

See handover document for complete requirements and implementation guidance."""
)
```

### Pattern 4: Progress Update Comment

**When**: Updating work item progress during work

**Format**:
```
[Copilot-Duty: {duty}] 📝 Progress: {milestone or status}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: implementation] 📝 Progress: Core authentication implemented, moving to session management phase."
)
```

### Pattern 5: Multi-Phase Parent Update Comment

**When**: Updating parent work item from child work

**Format**:
```
[Copilot-Duty: {duty}] 🔗 Phase {N} (#{child-number}) progress: {update}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=parent_work_item_id,
    text="[Copilot-Duty: implementation] 🔗 Phase 2 (#456) progress: Session management core complete, tests passing."
)
```

### Pattern 6: Multi-Phase Completion Comment

**When**: Closing parent work item after last child completes

**Format**:
```
[Copilot-Duty: {duty}] 🎉 All phases complete! {summary}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=parent_work_item_id,
    text="[Copilot-Duty: implementation] 🎉 All phases complete! User authentication system fully implemented with core auth, session management, and security hardening."
)
```

### Pattern 7: Completion Comment

**When**: Marking work complete

**Format**:
```
[Copilot-Duty: {duty}] ✅ {work type} complete: {summary}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: research] ✅ Research complete: Redis validated as best approach for distributed caching. See /research/caching-strategy/ for details."
)
```

### Pattern 8: Issue/Blocker Comment

**When**: Documenting a problem or blocker

**Format**:
```
[Copilot-Duty: {duty}] ⚠️ Issue: {description}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: implementation] ⚠️ Issue: Discovered null reference edge case not covered in handover. Creating research work item for investigation."
)
```

### Pattern 9: Question/Clarification Comment

**When**: Requesting clarification or human input

**Format**:
```
[Copilot-Duty: {duty}] ❓ Question: {question}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: implementation] ❓ Question: Handover recommends Redis but current architecture uses in-memory only. Need human decision on introducing Redis dependency."
)
```

### Pattern 10: Reference/Link Comment

**When**: Adding links to related work items or documents

**Format**:
```
[Copilot-Duty: {duty}] 🔗 Related: {description}
```

**Example**:
```python
add_work_item_comment(
    work_item_id=work_item_id,
    text="[Copilot-Duty: implementation] 🔗 Related: Parent work item #123 tracks overall multi-phase plan."
)
```

---

## Emoji Reference

Standard emojis for comment types:

| Emoji | Meaning | When to Use |
|-------|---------|-------------|
| 🎯 | Assignment | Duty assignment or reassignment |
| 🏷️ | Labels | Label cleanup or changes |
| 🔄 | Handover | Transitioning between duties |
| 📝 | Progress | Progress updates |
| 🔗 | Link/Phase | Multi-phase updates or references |
| 🎉 | Completion | All phases complete |
| ✅ | Success | Work complete successfully |
| ⚠️ | Warning/Issue | Problems or blockers |
| ❓ | Question | Requesting clarification |
| 📊 | Data/Results | Research findings, benchmarks |
| 🧪 | Testing | Test results or testing updates |
| 📚 | Documentation | Documentation updates |
| 🐛 | Bug | Bug-related comments |
| 🔧 | Fix | Bug fix or correction |

---

## Examples

### Example 1: Complete Research Work Item Comments

```python
# Start of work
add_work_item_comment(
    work_item_id="123",
    text="[Copilot-Duty: research] 📝 Progress: Research folder created at /research/caching-strategy/"
)

# Mid-work update
add_work_item_comment(
    work_item_id="123",
    text="[Copilot-Duty: research] 📊 Redis benchmarks complete: 8ms avg latency, 95th percentile 12ms"
)

# Completion and handover
add_work_item_comment(
    work_item_id="123",
    text="""[Copilot-Duty: research] 🔄 Handover: research → implementation

Research complete. Implementation work item created: #456

**Handover Document**: /research/caching-strategy/handover/implementation-handover.md
**Recommendation**: Redis with StackExchange.Redis client

See handover document for complete requirements."""
)
```

### Example 2: Multi-Phase Parent Updates

```python
# Phase 1 starting
add_work_item_comment(
    work_item_id=parent_id,
    text="[Copilot-Duty: implementation] 🔗 Phase 1 (#456) started: Core authentication implementation beginning"
)

# Phase 1 complete
add_work_item_comment(
    work_item_id=parent_id,
    text="[Copilot-Duty: implementation] 🔗 Phase 1 (#456) complete: ✅ Core authentication implemented and tested"
)

# Phase 2 progress
add_work_item_comment(
    work_item_id=parent_id,
    text="[Copilot-Duty: implementation] 🔗 Phase 2 (#457) progress: Session storage complete, working on refresh tokens"
)

# All phases complete
add_work_item_comment(
    work_item_id=parent_id,
    text="[Copilot-Duty: implementation] 🎉 All phases complete! User authentication system fully implemented."
)
```

### Example 3: Issue and Resolution

```python
# Issue discovered
add_work_item_comment(
    work_item_id="789",
    text="[Copilot-Duty: implementation] ⚠️ Issue: Handover missing edge case for null user IDs. Tests failing."
)

# Question for clarification
add_work_item_comment(
    work_item_id="789",
    text="[Copilot-Duty: implementation] ❓ Question: Should null user IDs throw exception or return empty result?"
)

# Resolution
add_work_item_comment(
    work_item_id="789",
    text="[Copilot-Duty: implementation] 🔧 Fix: Added null check with exception throw based on existing pattern in codebase. Tests now passing."
)
```

---

## Multi-Comment Scenarios

### When to Use Multiple Comments

**Use separate comments for**:
- Different events (start, progress, completion)
- Different topics (testing update, then documentation update)
- Chronological updates (progress at different times)

**Example**:
```python
# Comment 1: Progress
add_work_item_comment(work_item_id, "[Copilot-Duty: implementation] 📝 Progress: Core implementation complete")

# Comment 2 (later): Testing
add_work_item_comment(work_item_id, "[Copilot-Duty: implementation] 🧪 All tests passing, coverage 95%")

# Comment 3 (later): Completion
add_work_item_comment(work_item_id, "[Copilot-Duty: implementation] ✅ Implementation complete, ready for review")
```

### When to Use Single Comment

**Combine in single comment when**:
- All related to same event
- Completing work (include all final details)
- Creating handover (all handover details together)

**Example**:
```python
add_work_item_comment(
    work_item_id,
    text="""[Copilot-Duty: implementation] ✅ Implementation complete

**Deliverables**:
- Core authentication ✅
- Session management ✅
- Security hardening ✅

**Testing**: All tests passing (Unit: 127/127, Integration: 42/42)
**Documentation**: Updated README and API docs
**Performance**: Meets all criteria (<10ms cache operations)

Ready for review."""
)
```

---

## Edge Cases

### Case 1: Long Comment with Details

**Scenario**: Comment needs extensive details (handover, completion summary)

**Resolution**: Use markdown structure within comment for readability

**Example**:
```python
add_work_item_comment(
    work_item_id,
    text="""[Copilot-Duty: research] 🔄 Handover: research → implementation

Research complete. Implementation work item: #456

## Summary
Redis validated as optimal for distributed caching.

## Handover Document
/research/caching-strategy/handover/implementation-handover.md

## Key Findings
- Redis: 8ms avg, best .NET support
- Memcached: 6ms avg, limited .NET libraries
- Recommendation: Redis

See handover document for complete analysis."""
)
```

### Case 2: Error During Operation

**Scenario**: Adding comment about an error encountered

**Resolution**: Use ⚠️ emoji and clear error description

**Example**:
```python
add_work_item_comment(
    work_item_id,
    text="[Copilot-Duty: implementation] ⚠️ Issue: Build failed due to missing dependency. Added to package.json and rebuilding."
)
```

### Case 3: Human Interaction Needed

**Scenario**: Need human decision or clarification

**Resolution**: Use ❓ emoji and be specific about what's needed

**Example**:
```python
add_work_item_comment(
    work_item_id,
    text="[Copilot-Duty: implementation] ❓ Question: Handover suggests Pattern A, but codebase uses Pattern B. Which should I follow? @human-reviewer"
)
```

---

## Anti-Patterns

### ❌ Don't: Skip Duty Prefix

```python
# Wrong - no duty prefix
add_work_item_comment(
    work_item_id,
    text="Implementation complete"  # Missing [Copilot-Duty: implementation]
)
```

✅ **Correct**: Always include duty prefix

### ❌ Don't: Use Inconsistent Formats

```python
# Wrong - inconsistent formatting
add_work_item_comment(work_item_id, "Handover to implementation")
add_work_item_comment(work_item_id, "[implementation] handover")
add_work_item_comment(work_item_id, "HANDOVER: Implementation")
```

✅ **Correct**: Use standard format consistently

### ❌ Don't: Make Comments Too Vague

```python
# Wrong - vague
add_work_item_comment(
    work_item_id,
    text="[Copilot-Duty: implementation] Progress update"  # What progress?
)
```

✅ **Correct**: Be specific about what changed

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong - GitHub-specific API
add_issue_comment(  # ❌ Don't use platform-specific operations
    issue_number=123,
    body="Comment text"
)
```

✅ **Correct**: Use semantic operation `add_work_item_comment()`

---

## Success Criteria

- [ ] All comments include duty prefix `[Copilot-Duty: {duty}]`
- [ ] Appropriate emoji used for comment type
- [ ] Comment content is clear and specific
- [ ] Standard patterns followed for common scenarios
- [ ] Markdown formatting used for readability
- [ ] Semantic operations used exclusively

---

## Related Procedures

- [Duty Assignment](duty-assignment.md) - Duty names for prefixes
- [Handover](handover.md) - Handover comment patterns
- [Multi-Phase Work Items](multi-phase-work-items.md) - Multi-phase comment patterns
- [Self-Improvement](self-improvement.md) - Feedback submission

---

## Testing

**Test Scenarios**: `.team/procedures/tests/comment-patterns/`

**Key Scenarios**:
1. Standard comment types (assignment, handover, completion)
2. Multi-phase comments (parent updates)
3. Issue/question comments
4. Long structured comments
5. Multiple comments for same work item
