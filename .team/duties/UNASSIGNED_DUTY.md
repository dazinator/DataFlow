# Unassigned Duty

**Purpose**: Handle work items where duty cannot be inferred automatically, requiring human clarification

**Layer**: 2 (Duty - specialized procedure)

**Version**: 1.0  
**Created**: 2025-11-12

---

## Required Context

**⚠️ IMPORTANT**: Read the following documents before proceeding:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction layer
- **[Duty Assignment Procedure](../procedures/duty-assignment.md)** - Determining duty from work items
- **[Work Item Creation Procedure](../procedures/work-item-creation.md)** - Creating new work items
- **[Comment Patterns Procedure](../procedures/comment-patterns.md)** - Standard comment formats
- **[Handover Procedure](../procedures/handover.md)** - Transitioning work items between duties

**Semantic Operations Used**:
- `query_work_items_by_duty(duty)` - Find work items assigned to this duty
- `get_work_item_details(work_item_id)` - Retrieve work item information
- `get_work_item_duty(work_item_id)` - Get current duty assignment
- `assign_work_item_to_duty(work_item_id, duty)` - Change duty (handover)
- `add_work_item_comment(work_item_id, text)` - Add comment
- `update_work_item(work_item_id, fields)` - Update work item fields

---

## Overview

**Purpose**: Handle work items where the appropriate duty cannot be automatically determined, requiring human clarification or additional information before routing to the correct duty.

**Entry Point**: Work items that:
- Have no duty designation (missing labels)
- Have ambiguous or unclear descriptions
- Span multiple duty areas
- Require human judgment to classify

**Typical Duration**: 1-2 days (waiting for clarification)

**Key Principle**: The unassigned duty is a **temporary holding state**, not a permanent assignment. All work items should eventually be routed to an appropriate specialized duty.

---

## When Work Items Become Unassigned

Work items enter the unassigned duty when:

1. **No Duty Designation**: Created without duty label
2. **Ambiguous Content**: Description doesn't match any duty patterns
3. **Multiple Matches**: Could fit several duties, unclear which is primary
4. **Invalid Designation**: Had a duty that was removed or is no longer valid
5. **Label Pollution Cleanup**: Multiple conflicting duty labels removed, correct duty unclear

---

## Quick Start

**Comment Prefix Convention:**
- Follow [Comment Patterns Procedure](../procedures/comment-patterns.md)
- Prefix ALL comments with `[Copilot-Duty: Unassigned]`
- Example: `[Copilot-Duty: Unassigned] Unable to determine appropriate duty. Requesting clarification.`

**Standard Unassigned Flow**:
1. Query unassigned queue
2. Analyze work item to attempt duty inference
3. If duty can be inferred: Hand over to appropriate duty
4. If duty cannot be inferred: Request human clarification
5. Once clarified: Hand over to specified duty

---

## Procedure

### Step 1: Query Unassigned Queue

Use semantic operation to find work items assigned to unassigned duty:

```python
# Query unassigned queue
unassigned_items = query_work_items_by_duty(duty="unassigned")

print(f"Found {len(unassigned_items)} unassigned work items")
```

**Note**: The unassigned queue should ideally be **empty** or have very few items. Large numbers indicate a problem with duty assignment logic or work item creation.

### Step 2: Analyze Work Item

For each unassigned work item, gather information to attempt duty inference:

```python
# Get work item details
details = get_work_item_details(work_item_id)

title = details['title']
description = details['description']
work_item_type = details['type']
labels = details['labels']
```

**Review**:
- Work item title and description
- Work item type
- Additional labels
- Comments or history (if available)

### Step 3: Attempt Duty Inference

Use [Duty Assignment Procedure](../procedures/duty-assignment.md) to attempt inferring the correct duty:

**Check for Duty Indicators**:

- **Triage**: General issues needing assessment
- **Research**: "investigate", "research", "evaluate", "analyze", "validate approach"
- **Implementation**: "implement", "build", "create", "add feature", clear requirements
- **Tech Debt**: "refactor", "cleanup", "debt", "improve code quality", "technical debt"
- **Product Backlog**: "prioritize", "backlog item", feature requests awaiting prioritization
- **Process Modeling**: "workflow", "process", changes to `.team/` or `.github/copilot-instructions.md`

**Example Analysis**:
```python
# Attempt to infer duty
inferred_duty = None

if any(keyword in title.lower() or keyword in description.lower() 
       for keyword in ["investigate", "research", "evaluate", "analyze"]):
    inferred_duty = "research"
elif any(keyword in title.lower() or keyword in description.lower() 
         for keyword in ["implement", "build", "create", "add"]):
    inferred_duty = "implementation"
elif any(keyword in title.lower() or keyword in description.lower() 
         for keyword in ["refactor", "cleanup", "debt", "improve"]):
    inferred_duty = "tech-debt"
elif any(keyword in title.lower() or keyword in description.lower() 
         for keyword in ["workflow", "process", "procedure"]):
    inferred_duty = "process-modeling"
elif any(keyword in title.lower() or keyword in description.lower() 
         for keyword in ["prioritize", "backlog"]):
    inferred_duty = "product-backlog"
else:
    # Default to triage for general issues
    inferred_duty = "triage"
```

### Step 4: Decision Point - Can Duty Be Inferred?

#### If Duty Can Be Inferred (Confidence High)

**Proceed to Step 5**: Hand over to inferred duty

#### If Duty Cannot Be Inferred (Confidence Low)

**Proceed to Step 6**: Request human clarification

**Indicators of Low Confidence**:
- Title and description are vague or generic
- Work item could fit multiple duties equally well
- No clear keywords or patterns match
- Work item spans concerns of multiple duties
- Unusual or novel request type

### Step 5: Hand Over to Inferred Duty

If duty can be confidently inferred, hand over using [Handover Procedure](../procedures/handover.md):

```python
# Hand over to inferred duty
assign_work_item_to_duty(
    work_item_id=work_item_id,
    duty=inferred_duty
)

# Add handover comment following comment patterns
handover_comment = f"""[Copilot-Duty: Unassigned] 🔄 **Duty Assignment Complete**

Based on analysis of the work item, I've assigned this to the **{inferred_duty}** duty.

**Assignment Rationale**: {rationale}

See `.team/duties/{inferred_duty.upper()}_DUTY.md` for next steps.

If this assignment is incorrect, please update the duty label and add a comment with the correct duty.
"""

add_work_item_comment(
    work_item_id=work_item_id,
    text=handover_comment
)
```

**Work complete** - Item is now assigned and out of unassigned queue.

### Step 6: Request Human Clarification

If duty cannot be confidently inferred, request human clarification:

```python
# Add clarification request comment
clarification_comment = """[Copilot-Duty: Unassigned] ⚠️ **Unable to Determine Appropriate Duty**

I cannot automatically determine which duty should handle this work item.

**Analysis**:
- **Title**: {title}
- **Type**: {work_item_type}
- **Ambiguity**: {reason_for_ambiguity}

**Possible Duties**:
{list_of_potential_duties_with_rationale}

**Action Required**:
Please add a comment specifying the correct duty, or update the work item with one of:
- More specific title/description
- Duty label (`workflow:research`, `workflow:implementation`, etc.)
- Work item type

**Available Duties**:
- `triage` - General issues needing assessment
- `research` - Validate approaches, investigate unknowns
- `implementation` - Build validated solutions
- `tech-debt` - Discover and document technical debt
- `product-backlog` - Feature requests awaiting prioritization
- `process-modeling` - Workflow and process improvements

Reply with: `@copilot assign to [duty]` or update the work item labels directly.

See [Duty Assignment Procedure](../.team/procedures/duty-assignment.md) for details.
"""

add_work_item_comment(
    work_item_id=work_item_id,
    text=clarification_comment
)
```

**Wait for human response** - Item remains in unassigned queue until clarified.

### Step 7: Process Human Response

When human provides clarification (via comment or label update):

```python
# Human responds with duty assignment
# Example: "@copilot assign to research"

# Extract duty from response (inline parsing)
# Look for pattern: "assign to [duty]" or "assign to: [duty]"
import re
match = re.search(r'assign\s+to:?\s+(\w+(?:-\w+)*)', comment_text.lower())
specified_duty = match.group(1) if match else None

# Validate duty is valid
valid_duties = ["triage", "research", "implementation", "tech-debt", 
                "product-backlog", "process-modeling"]

if specified_duty in valid_duties:
    # Hand over to specified duty
    assign_work_item_to_duty(
        work_item_id=work_item_id,
        duty=specified_duty
    )
    
    # Add confirmation comment
    add_work_item_comment(
        work_item_id=work_item_id,
        text=f"[Copilot-Duty: Unassigned] ✅ Assigned to **{specified_duty}** duty as requested. See `.team/duties/{specified_duty.upper()}_DUTY.md` for next steps."
    )
else:
    # Invalid duty specified
    add_work_item_comment(
        work_item_id=work_item_id,
        text=f"[Copilot-Duty: Unassigned] ❌ Invalid duty '{specified_duty}'. Valid duties are: {', '.join(valid_duties)}. Please specify a valid duty."
    )
```

---

## Handover Points

### Handover to Any Specialized Duty

**When**: Duty has been determined (inferred or human-specified)

**Process**: Use [Handover Procedure](../procedures/handover.md)

```python
# Hand over to target duty
assign_work_item_to_duty(
    work_item_id=work_item_id,
    duty=target_duty
)

# Add handover comment
add_work_item_comment(
    work_item_id=work_item_id,
    text=f"[Copilot-Duty: Unassigned] 🔄 Assigned to {target_duty} duty. See `.team/duties/{target_duty.upper()}_DUTY.md`."
)
```

**Common Handover Targets**:
- **Triage** - General issues, unclear scope
- **Research** - Investigations, approach validation
- **Implementation** - Feature building, clear requirements
- **Tech Debt** - Code quality improvements
- **Product Backlog** - Feature requests needing prioritization
- **Process Modeling** - Workflow improvements

---

## Common Patterns

### Pattern 1: New Work Item Without Duty

**Scenario**: Work item created without duty label

**Action**:
1. Analyze title/description
2. If clear match to duty patterns → Hand over immediately
3. If unclear → Request clarification

**Example**:
- Title: "Add caching to API calls" → Infer `implementation` → Hand over
- Title: "Improve performance" → Ambiguous → Request clarification

### Pattern 2: Label Pollution Cleanup

**Scenario**: Work item had multiple conflicting duty labels, all removed

**Action**:
1. Review work item history/comments
2. Determine intended duty from context
3. If clear → Hand over
4. If unclear → Request clarification

### Pattern 3: Cross-Cutting Concern

**Scenario**: Work item spans multiple duty areas

**Action**:
1. Identify primary duty (which should handle first)
2. Note in comment that work may involve multiple duties
3. Hand over to primary duty
4. Let primary duty create sub-items for other duties if needed

**Example**:
- "Research caching strategy and implement" → Primary: `research` (validate first)
- Research duty will later hand over to `implementation`

### Pattern 4: Novel or Unusual Request

**Scenario**: Work item doesn't fit standard patterns

**Action**:
1. Default to `triage` duty (assessment needed)
2. Add comment noting unusual nature
3. Let triage duty assess and route appropriately

---

## Monitoring Unassigned Queue

**Health Metrics**:
- **Queue Size**: Should be **< 5 items** typically
- **Age**: Items should not stay unassigned for **> 3 days**
- **Trend**: Increasing queue suggests duty assignment problems

**If Queue Is Large** (> 10 items):
1. Review recent items for common patterns
2. Check if duty assignment procedure needs updates
3. Consider if new duty is needed for common pattern
4. Escalate to process modeling if systematic issue

**If Items Stay Unassigned Long** (> 5 days):
1. Follow up with requestor for clarification
2. Consider closing if no response after 2 follow-ups
3. Document reason for closure

---

## Success Criteria

Unassigned duty handling is successful when:

- ✅ Work items are routed to appropriate duty quickly (< 1 day)
- ✅ Unassigned queue stays small (< 5 items)
- ✅ Clarification requests are clear and actionable
- ✅ Human responses are processed promptly
- ✅ Duty assignments are accurate (low reassignment rate)

---

## Examples

### Example 1: Clear Research Item

**Work Item**:
- Title: "Investigate Redis for caching layer"
- Description: "Need to evaluate Redis vs Memcached for API caching"
- Duty: `unassigned`

**Action**:
1. Analyze: Keywords "Investigate", "evaluate" → `research` duty
2. High confidence → Hand over to research
3. Add comment: "Assigned to research based on investigation nature"

**Outcome**: Work item moves to research queue

### Example 2: Ambiguous Request

**Work Item**:
- Title: "Performance problems"
- Description: "System is slow"
- Duty: `unassigned`

**Action**:
1. Analyze: Vague, could be research OR tech-debt OR implementation
2. Low confidence → Request clarification
3. Add comment: "Need more details - is this an investigation, cleanup, or feature?"

**Outcome**: Wait for human clarification

### Example 3: Cross-Cutting Work

**Work Item**:
- Title: "Add authentication and evaluate OAuth providers"
- Description: "Need to research OAuth options and implement chosen solution"
- Duty: `unassigned`

**Action**:
1. Analyze: Contains both "research" and "implement"
2. Primary: `research` (validate approach first)
3. Hand over to research with note about implementation phase
4. Add comment: "Assigned to research for OAuth evaluation. Implementation will follow after research."

**Outcome**: Research duty handles evaluation, will hand over to implementation later

---

## Related Documentation

- **[Duty Assignment Procedure](../procedures/duty-assignment.md)** - Duty inference logic
- **[Handover Procedure](../procedures/handover.md)** - Transitioning work items between duties
- **[Work Item Creation Procedure](../procedures/work-item-creation.md)** - Creating well-formed work items
- **[Comment Patterns Procedure](../procedures/comment-patterns.md)** - Standard comment formats

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial Unassigned Duty created (Phase 3.2) |
