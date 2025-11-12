# Scenario 002: Ambiguous Request Requiring Clarification

**Duty**: Unassigned  
**Type**: Edge Case Scenario  
**Complexity**: Medium  
**Created**: 2025-11-12

---

## Purpose

Test unassigned duty's handling of work items where the appropriate duty cannot be confidently inferred, requiring human clarification.

---

## Context

A work item is created with a vague title and minimal description that doesn't match clear duty patterns. Duty cannot be automatically inferred.

---

## Starting State

**Work Item #3050**:
- **Title**: "Performance issues"
- **Description**: 
  ```
  The system is slow.
  
  Need to fix this.
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
unassigned_items = query_work_items_by_duty(duty="unassigned")
# Returns: [#3050]
```

### Step 2: Analyze Work Item

```python
details = get_work_item_details(work_item_id="3050")

title = details['title']  # "Performance issues"
description = details['description']  # "The system is slow. Need to fix this."
work_item_type = details['type']  # Not specified
```

**Analysis**:
- Title: "Performance issues" - Very vague
- Description: Minimal, no specific keywords
- Could be:
  - **Research** (investigate performance bottlenecks)
  - **Tech Debt** (optimize existing code)
  - **Implementation** (add performance features like caching)
  - **Triage** (needs assessment to understand scope)

### Step 3: Attempt Duty Inference

```python
# Check for duty indicators
# "performance" keyword alone is ambiguous

# Could match multiple duties:
possible_duties = {
    "research": "If investigation/profiling needed",
    "tech-debt": "If optimizing existing code",
    "implementation": "If adding new performance features",
    "triage": "If scope unclear"
}

# No clear winner
inferred_duty = None
confidence = "low"
reason_for_ambiguity = "Title and description are too vague. 'Performance issues' could mean investigation, optimization, or new features. Need more specifics about scope and desired outcome."
```

**Decision**: Cannot confidently infer duty - proceed to request clarification

### Step 4: Request Human Clarification

```python
# Add clarification request comment
clarification_comment = """[Copilot-Duty: Unassigned] ⚠️ **Unable to Determine Appropriate Duty**

I cannot automatically determine which duty should handle this work item.

**Analysis**:
- **Title**: "Performance issues"
- **Type**: Not specified
- **Ambiguity**: The description "The system is slow" is too vague to determine the appropriate duty. Performance work could be:
  - **Investigation** (research duty) - Profile and identify bottlenecks
  - **Code optimization** (tech debt duty) - Refactor slow code
  - **New features** (implementation duty) - Add caching, async processing
  - **General assessment** (triage duty) - Unclear scope needing evaluation

**Possible Duties**:
- `research` - If you need to **investigate and identify** performance bottlenecks
- `tech-debt` - If you want to **optimize existing code** that's slow
- `implementation` - If you want to **add new performance features** (caching, async)
- `triage` - If **scope is unclear** and needs assessment first

**Action Required**:
Please provide more details:
1. **What is slow?** (API calls, UI rendering, database queries, etc.)
2. **What outcome do you want?** (Investigation report, optimized code, new features)
3. **Is this a known issue or needs research?**

Or specify the correct duty directly by replying: `@copilot assign to [duty]`

**Available Duties**:
- `triage` - General issues needing assessment
- `research` - Validate approaches, investigate unknowns
- `implementation` - Build validated solutions
- `tech-debt` - Discover and document technical debt
- `product-backlog` - Feature requests awaiting prioritization
- `process-modeling` - Workflow and process improvements

See [Duty Assignment Procedure](../../../.team/procedures/duty-assignment.md) for details.
"""

add_work_item_comment(
    work_item_id="3050",
    text=clarification_comment
)
```

**Wait for human response** - Item remains in unassigned queue

### Step 5: Process Human Response (Simulation)

**Assume human responds**:
```
@copilot assign to research

We need to profile the API to identify bottlenecks. Please investigate performance issues in the data processing pipeline.
```

Parse and process:

```python
# Parse duty from comment
specified_duty = "research"  # Extracted from "@copilot assign to research"

# Validate duty
valid_duties = ["triage", "research", "implementation", "tech-debt", 
                "product-backlog", "process-modeling"]

if specified_duty in valid_duties:
    # Hand over to specified duty
    assign_work_item_to_duty(
        work_item_id="3050",
        duty="research"
    )
    
    # Add confirmation comment
    confirmation_comment = """[Copilot-Duty: Unassigned] ✅ Assigned to **research** duty as requested.

Thank you for the clarification. This work item will now be handled by the research duty for performance investigation and profiling.

See `.team/duties/RESEARCH_DUTY.md` for next steps.
"""
    
    add_work_item_comment(
        work_item_id="3050",
        text=confirmation_comment
    )
```

### Step 6: Update Work Item Description (Recommended)

**Note**: After clarification, recommend updating work item description for clarity:

```python
# Optional: Suggest description update
suggestion_comment = """[Copilot-Duty: Unassigned] 💡 **Recommendation**

To improve clarity for future work items, consider updating the description with:
- Specific scope: "Profile API data processing pipeline"
- Desired outcome: "Identify performance bottlenecks and recommend optimizations"
- Context: "API response times have degraded under load"

This helps with initial duty assignment and provides better context for the research duty.
"""

add_work_item_comment(
    work_item_id="3050",
    text=suggestion_comment
)
```

---

## Expected Outcome

**Duty Assignment**:
- #3050: Initially `unassigned`, then changed to `research` after clarification

**Comments Added**:
1. Clarification request comment on #3050 (detailed, actionable)
2. Confirmation comment on #3050 (after human response)
3. Optional: Suggestion comment for description improvement

**Queue State**:
- Unassigned queue: #3050 stays until human clarifies, then moves to research
- Research queue: #3050 added after clarification

**Human Interaction**:
- ✅ Clear, actionable clarification request
- ✅ Multiple options provided with rationale
- ✅ Human response processed correctly
- ✅ Confirmation provided after assignment

**Semantic Operations Used** (✅ Correct):
- `query_work_items_by_duty(duty="unassigned")`
- `get_work_item_details(work_item_id="3050")`
- `add_work_item_comment(work_item_id="3050", text=...)` (multiple times)
- `assign_work_item_to_duty(work_item_id="3050", duty="research")` (after clarification)

**No Platform-Specific Code** (✅ Correct):
- All operations through semantic layer

**Procedures Referenced** (✅ Correct):
- Duty Assignment Procedure (for inference logic and available duties)
- Comment Patterns Procedure (for comment format)
- Handover Procedure (for final duty transition)

**Clarification Quality**:
- ✅ Clear explanation of ambiguity
- ✅ Multiple possible duties with rationale
- ✅ Actionable next steps (provide details or assign directly)
- ✅ Reference to duty assignment procedure
- ✅ Professional, helpful tone

---

## Test Result

**PASS** / FAIL

**Notes**: [Verify clarification request is clear, actionable, and helpful for human user]

---

## Validation Points

1. ✅ Correctly identifies ambiguity (doesn't guess)
2. ✅ Explains why duty cannot be inferred
3. ✅ Provides helpful context about each possible duty
4. ✅ Clear call-to-action for human
5. ✅ Processes human response correctly
6. ✅ Confirms assignment after clarification

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial scenario for ambiguous request testing |
