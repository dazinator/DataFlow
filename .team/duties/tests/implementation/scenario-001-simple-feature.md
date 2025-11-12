# Scenario 001: Simple Feature Implementation

**Duty**: Implementation  
**Type**: End-to-End Scenario  
**Complexity**: Low  
**Created**: 2025-11-12

---

## Purpose

Test implementation duty's ability to implement a straightforward feature with clear requirements in a single-phase workflow.

---

## Context

A work item has clear requirements for adding a new feature with no unknowns.

---

## Starting State

**Work Item #1024**:
- **Title**: "Add TimeoutPeriod property to BatchBlock"
- **Description**: 
  ```
  Add a configurable timeout period to BatchBlock that flushes pending items when no new items arrive within the timeout window.
  
  Requirements:
  - Add TimeoutPeriod property (TimeSpan) to BatchBlock
  - Default value: TimeSpan.MaxValue (no timeout)
  - When timeout expires with pending items, flush batch immediately
  - Reset timeout when new item arrives
  
  Acceptance Criteria:
  - Property added with XML documentation
  - Timeout behavior implemented correctly
  - Unit tests added:
    - Test timeout flush with pending items
    - Test timeout reset on new item
    - Test no timeout with TimeSpan.MaxValue
  - Documentation updated
  ```
- **Duty**: `implementation`
- **Status**: `open`

---

## Procedure to Follow

[Implementation Duty](../../../.team/duties/IMPLEMENTATION_DUTY.md)

---

## Expected Steps

### Step 1: Query and Check Multi-Phase

```python
impl_items = query_work_items_by_duty(duty="implementation")
details = get_work_item_details(work_item_id="1024")

# Check multi-phase
is_multi = is_multi_phase(work_item_id="1024")  # Returns False
```

### Step 2: Review Requirements

Requirements are clear and complete. No design references or research needed.

### Step 3: Implement Changes

```markdown
## Implementation Plan
- [ ] Add TimeoutPeriod property to BatchBlock class
- [ ] Implement timeout logic in batch window
- [ ] Add unit tests
- [ ] Update XML documentation
```

Implement code changes following DataFlow coding standards.

### Step 4: Write Tests

```csharp
[Trait("Category", "UnitTest")]
public class BatchBlockTimeoutTests
{
    [Fact]
    public async Task Should_FlushBatch_WhenTimeoutExpires()
    {
        // Test timeout flush behavior
    }
    
    [Fact]
    public async Task Should_ResetTimeout_WhenNewItemArrives()
    {
        // Test timeout reset
    }
    
    [Fact]
    public async Task Should_NotTimeout_WhenTimeSpanMaxValue()
    {
        // Test default no-timeout behavior
    }
}
```

### Step 5: Build and Test

```bash
dotnet format
dotnet build
dotnet test
```

All tests pass.

### Step 6: Submit Feedback

```python
tracker = query_feedback_tracker()

add_work_item_comment(
    tracker.id,
    """## Workflow Feedback Entry

**Date**: 2025-11-12
**Issue/PR**: #1024
**Duty**: Implementation

### What Worked Well
- Clear requirements made implementation straightforward
- Test patterns were easy to follow

### What Didn't Work Well
- None identified

### Suggested Improvement
None needed for this straightforward case.
"""
)
```

### Step 7: Complete and Create PR

```python
add_work_item_comment(
    work_item_id="1024",
    "[Copilot-Duty: Implementation] ✅ Implementation Complete\n\n"
    "**Changes**:\n"
    "- Added TimeoutPeriod property to BatchBlock\n"
    "- Implemented timeout flush logic\n"
    "- Added 3 unit tests\n\n"
    "**Tests**: All passing\n"
    "**Documentation**: Updated with XML comments\n\n"
    "**Status**: Ready for review"
)
```

---

## Expected Outcome

**Work Item #1024**:
- **Duty**: Still `implementation` (until PR merged)
- **Status**: `open` with completion comment

**Deliverables**:
- Code changes implementing feature
- Unit tests passing
- XML documentation added
- PR ready for review

---

## Success Criteria

- [ ] Work item queried using semantic operations
- [ ] Requirements reviewed and found complete
- [ ] Code implemented following coding standards
- [ ] Tests written and passing
- [ ] Documentation updated
- [ ] Self-improvement feedback submitted
- [ ] Completion comment added
- [ ] No platform-specific code (zero kernel leaks)

---

## Test Result

**Status**: [PENDING]

---

## Semantic Operations Used

- `query_work_items_by_duty(duty)`
- `get_work_item_details(work_item_id)`
- `is_multi_phase(work_item_id)`
- `add_work_item_comment(work_item_id, text)`
- `query_feedback_tracker()`
