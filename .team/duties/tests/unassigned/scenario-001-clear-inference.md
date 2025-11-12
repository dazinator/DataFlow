# Scenario 001: Clear Duty Inference

**Duty**: Unassigned  
**Type**: End-to-End Scenario  
**Complexity**: Low  
**Created**: 2025-11-12

---

## Purpose

Test unassigned duty's ability to correctly infer the appropriate duty from a work item with clear indicators and automatically hand over.

---

## Context

A work item is created without a duty label, but has clear keywords and description that indicate it should be assigned to the research duty.

---

## Starting State

**Work Item #3001**:
- **Title**: "Investigate Redis vs Memcached for caching layer"
- **Description**: 
  ```
  We need to evaluate caching solutions for the API layer.
  
  **Requirements**:
  - Research Redis and Memcached capabilities
  - Compare performance, features, and ease of integration
  - Recommend solution with implementation plan
  
  **Deliverables**:
  - Analysis document comparing options
  - Recommendation with rationale
  - POC code demonstrating chosen approach
  ```
- **Duty**: `unassigned` (no duty label)
- **Status**: `open`
- **Type**: Not specified

---

## Procedure to Follow

[Unassigned Duty](../../../.team/duties/UNASSIGNED_DUTY.md)

---

## Expected Steps

### Step 1: Query Unassigned Queue

```python
# Query unassigned queue
unassigned_items = query_work_items_by_duty(duty="unassigned")
# Returns: [#3001]

print(f"Found {len(unassigned_items)} unassigned work items")
# Output: "Found 1 unassigned work items"
```

### Step 2: Analyze Work Item

```python
# Get work item details
details = get_work_item_details(work_item_id="3001")

title = details['title']  # "Investigate Redis vs Memcached for caching layer"
description = details['description']
work_item_type = details['type']  # Not specified
labels = details['labels']  # ["unassigned"] or similar
```

**Review**:
- Title contains: "Investigate" (strong research indicator)
- Description contains: "evaluate", "research", "compare" (research keywords)
- Deliverables mention: "Analysis document", "Recommendation", "POC" (research outputs)

### Step 3: Attempt Duty Inference

Use duty assignment procedure logic:

```python
# Check for duty indicators
research_keywords = ["investigate", "research", "evaluate", "analyze"]

# Title: "Investigate Redis vs Memcached..."
# Contains "Investigate" → Strong match for research

inferred_duty = "research"
confidence = "high"
rationale = "Title and description contain clear research keywords: 'investigate', 'evaluate', 'research', 'compare'. Deliverables include analysis document and recommendation."
```

**Decision**: High confidence in `research` duty assignment

### Step 4: Hand Over to Inferred Duty

```python
# Hand over to research duty
assign_work_item_to_duty(
    work_item_id="3001",
    duty="research"
)

# Add handover comment following comment patterns
handover_comment = """[Copilot-Duty: Unassigned] 🔄 **Duty Assignment Complete**

Based on analysis of the work item, I've assigned this to the **research** duty.

**Assignment Rationale**: Title and description contain clear research keywords: 'investigate', 'evaluate', 'research', 'compare'. This work requires validation and comparison before implementation, which is the core responsibility of the research duty.

See `.team/duties/RESEARCH_DUTY.md` for next steps.

If this assignment is incorrect, please update the duty label and add a comment with the correct duty.
"""

add_work_item_comment(
    work_item_id="3001",
    text=handover_comment
)
```

---

## Expected Outcome

**Duty Assignment**:
- #3001: Changed from `unassigned` to `research`

**Comments Added**:
- Handover comment on #3001 explaining assignment and rationale

**Queue State**:
- Unassigned queue: Now empty (0 items)
- Research queue: Now contains #3001

**Semantic Operations Used** (✅ Correct):
- `query_work_items_by_duty(duty="unassigned")`
- `get_work_item_details(work_item_id="3001")`
- `assign_work_item_to_duty(work_item_id="3001", duty="research")`
- `add_work_item_comment(work_item_id="3001", text=...)`

**No Platform-Specific Code** (✅ Correct):
- All operations through semantic layer
- No GitHub MCP tools used directly

**Procedures Referenced** (✅ Correct):
- Duty Assignment Procedure (for inference logic)
- Handover Procedure (for duty transition)
- Comment Patterns Procedure (for comment format)

**Inference Quality**:
- ✅ Correct duty assigned (research)
- ✅ Clear rationale provided
- ✅ Automatic handover without human intervention
- ✅ Fast routing (< 1 day)

---

## Test Result

**PASS** / FAIL

**Notes**: [Verify inference logic correctly identifies research keywords and assigns appropriate duty]

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial scenario for clear duty inference testing |
