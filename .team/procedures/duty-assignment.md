# Duty Assignment Procedure

**Purpose**: Determine which duty should handle a work item

**Layer**: 1 (Global Procedure)

**Used By**: Orchestration (Layer 0), all duties (Layer 2)

---

## Overview

Every work item must be assigned to exactly one duty. This procedure defines how to infer the correct duty from work item properties.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `get_work_item_duty(work_item_id)` - Extract duty designation
- `assign_work_item_to_duty(work_item_id, duty)` - Change duty assignment
- `get_work_item_details(work_item_id)` - Get work item information
- `add_work_item_comment(work_item_id, text)` - Add comment

**Work Item Fields Required**:
- `duty` - Current duty assignment (may be None)
- `type` - Work item type
- `title` - Work item title
- `description` - Work item description
- `labels` - Additional labels

---

## Procedure

### Step 1: Get Current Duty Designation

```python
# Get current duty from work item
duty = get_work_item_duty(work_item_id)
```

**Possible Results**:
- `duty = "triage"` - Awaiting assessment
- `duty = "research"` - Research work
- `duty = "implementation"` - Implementation work
- `duty = "tech-debt"` - Technical debt work
- `duty = "product-backlog"` - Needs prioritization
- `duty = "process-modeling"` - Process improvement work
- `duty = None` - Unassigned (missing duty designation)

### Step 2: Handle Multiple Duty Designations (Label Pollution)

If `get_work_item_duty()` returns an **error** indicating multiple duties:

1. **Recognize the conflict**: Work item has multiple duty designations
2. **Get work item details** to see all labels:
   ```python
   details = get_work_item_details(work_item_id)
   conflicting_duties = [label for label in details['labels'] 
                        if label.startswith('workflow:')]
   ```

3. **Determine correct duty** based on context:
   - Check work item description for duty indicators
   - Check work item type
   - Use most recent activity/comments as tiebreaker

4. **Clean up labels**:
   ```python
   # Remove all conflicting duties, assign correct one
   assign_work_item_to_duty(work_item_id, correct_duty)
   ```

5. **Add cleanup comment**:
   ```python
   add_work_item_comment(
       work_item_id,
       f"[Copilot-Duty: {correct_duty}] 🏷️ Label cleanup: Removed conflicting duty labels. This work item is correctly in the {correct_duty} duty."
   )
   ```

### Step 3: Handle Unassigned Work Items

If `duty = None` (no duty designation):

1. **Assess work item type**:
   ```python
   details = get_work_item_details(work_item_id)
   work_item_type = details['type']
   title = details['title']
   description = details['description']
   ```

2. **Assign duty based on indicators**:
   
   **Triage Duty** (`triage`):
   - New work items without clear assignment
   - Work items needing assessment
   
   **Research Duty** (`research`):
   - Title/description contains: "investigate", "research", "evaluate", "analyze"
   - Work item type is "research"
   - Requires validation before implementation
   
   **Implementation Duty** (`implementation`):
   - Title/description contains: "implement", "build", "create", "add feature"
   - Work item type is "implementation"
   - Has clear requirements or research handover
   
   **Tech Debt Duty** (`tech-debt`):
   - Title/description contains: "refactor", "cleanup", "debt", "improve code quality"
   - Work item type is "tech-debt"
   
   **Product Backlog** (`product-backlog`):
   - Title/description contains: "prioritize", "backlog"
   - Needs prioritization decision
   
   **Process Modeling Duty** (`process-modeling`):
   - Title/description contains: "workflow", "process", "documentation"
   - Changes to `.team/prompts/`, `.github/copilot-instructions.md`, etc.
   - Work item type is "process-modeling"

3. **Assign duty**:
   ```python
   assign_work_item_to_duty(work_item_id, inferred_duty)
   ```

4. **Add assignment comment**:
   ```python
   add_work_item_comment(
       work_item_id,
       f"[Copilot-Duty: {inferred_duty}] 🎯 Duty assignment: Assigned to {inferred_duty} based on work item type and description."
   )
   ```

### Step 4: Validate Current Assignment

If `duty` is already assigned, validate it's correct:

1. **Check if duty makes sense** for work item type
2. **If assignment is wrong**:
   - Determine correct duty (see Step 3)
   - Reassign using `assign_work_item_to_duty()`
   - Add comment explaining the change

3. **If assignment is correct**: Proceed with duty execution

---

## Examples

### Example 1: Clear Assignment

```python
# Work item: "Implement user authentication"
duty = get_work_item_duty(work_item_id="123")
# Result: duty = "implementation"
# Action: Proceed with implementation duty
```

### Example 2: Multiple Duties (Label Pollution)

```python
# Work item has labels: ["workflow:research", "workflow:implementation"]
duty = get_work_item_duty(work_item_id="456")
# Result: Error - multiple duties detected

details = get_work_item_details(work_item_id="456")
# Check description: "Research handover complete. Ready for implementation."
# Correct duty: "implementation"

assign_work_item_to_duty(work_item_id="456", duty="implementation")
add_work_item_comment(
    work_item_id="456",
    "[Copilot-Duty: implementation] 🏷️ Label cleanup: Removed workflow:research. Implementation phase starting."
)
```

### Example 3: Unassigned Work Item

```python
# Work item: "Evaluate caching strategies for data pipeline"
duty = get_work_item_duty(work_item_id="789")
# Result: duty = None (unassigned)

details = get_work_item_details(work_item_id="789")
# Title contains "Evaluate" - indicates research work
# Assign to research duty

assign_work_item_to_duty(work_item_id="789", duty="research")
add_work_item_comment(
    work_item_id="789",
    "[Copilot-Duty: research] 🎯 Assigned to research duty based on evaluation nature of work."
)
```

---

## Edge Cases

### Case 1: Conflicting Indicators

**Scenario**: Title says "Implement X" but description says "First evaluate approaches"

**Resolution**:
1. Description takes precedence over title
2. If truly ambiguous, assign to `triage` for human review
3. Add comment explaining ambiguity

### Case 2: Wrong Duty Mid-Work

**Scenario**: Work starts in research, but research complete and implementation needed

**Resolution**:
1. This is a **handover**, not a reassignment
2. Create new work item for implementation duty
3. Close current research work item
4. See [Handover Procedure](handover.md)

### Case 3: Unknown Work Item Type

**Scenario**: Work item type doesn't match any known patterns

**Resolution**:
1. Assign to `triage` duty
2. Add comment requesting human assessment
3. Do not guess or force-fit into wrong duty

---

## Anti-Patterns

### ❌ Don't: Assign Based on Personal Preference

```python
# Wrong - forcing assignment without evidence
assign_work_item_to_duty(work_item_id, duty="implementation")  # "I prefer implementation work"
```

✅ **Correct**: Follow evidence from work item properties

### ❌ Don't: Skip Cleanup of Multiple Duties

```python
# Wrong - proceeding with multiple duties
duty = get_work_item_duty(work_item_id)
# Error: multiple duties
# Ignoring error and proceeding anyway - WRONG
```

✅ **Correct**: Always clean up label pollution before proceeding

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong - using GitHub-specific MCP tool directly
issue_write(  # ❌ Don't use platform-specific operations
    method="update",
    labels=["workflow:research"]  # ❌ Don't use platform-specific labels
)
```

✅ **Correct**: Use semantic operation `assign_work_item_to_duty()`

---

## Success Criteria

- [ ] Work item has exactly ONE duty assignment
- [ ] Duty assignment matches work item type and description
- [ ] Label pollution cleaned up (if present)
- [ ] Comment added explaining assignment (if changed)
- [ ] Semantic operations used exclusively (no platform code)

---

## Related Procedures

- [Handover](handover.md) - Transitioning between duties
- [Work Item Creation](work-item-creation.md) - Creating work items with correct duty
- [Comment Patterns](comment-patterns.md) - Standard comment formats

---

## Testing

**Test Scenarios**: `.team/procedures/tests/duty-assignment/`

**Key Scenarios**:
1. Clear duty assignment (happy path)
2. Multiple duties (label pollution)
3. Unassigned work item (inference needed)
4. Wrong duty (correction needed)
5. Ambiguous indicators (triage fallback)
