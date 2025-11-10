# Scenario 004: Triage - Priority and Date Ordering

## Test Type
Process Modeling - Feedback Backlog Triage

## Scenario Description
Testing Rule 5: Processing order (Priority → Date descending).

After triage assigns priorities to all issues, they should be processed in the correct order: P1 first, then P2, then P3. Within each priority tier, process most recent feedback first (date descending).

## Given (Initial State)

**Mock Processing Queue After Triage**:

```python
processing_queue = [
    {"priority": "P2", "created_at": "2025-11-09T10:00:00Z", "number": 260, "title": "Test boilerplate"},
    {"priority": "P1", "created_at": "2025-11-08T14:00:00Z", "number": 271, "title": "PM workflow improvement A"},
    {"priority": "P3", "created_at": "2025-11-10T09:00:00Z", "number": 262, "title": "Missing context issue"},
    {"priority": "P2", "created_at": "2025-11-07T11:00:00Z", "number": 258, "title": "Old implementation feedback"},
    {"priority": "P1", "created_at": "2025-11-10T11:00:00Z", "number": 276, "title": "PM workflow improvement B"},
    {"priority": "P3", "created_at": "2025-11-06T10:00:00Z", "number": 255, "title": "Very old unclear feedback"},
]
```

**Workflow State**:
- All issues have been triaged
- Priorities assigned (P1/P2/P3)
- Ready to sort and process

## When (Action)

Agent applies Rule 5 sorting:
```python
# Sort by priority first (P1 > P2 > P3), then by date descending (newest first)
from datetime import datetime

processing_queue.sort(
    key=lambda x: (
        x["priority"],  # P1 comes before P2 before P3 (string sort)
        -datetime.fromisoformat(x["created_at"]).timestamp()  # Negative for descending
    )
)
```

## Then (Expected Outcome)

### Expected Sorted Order

After sorting, queue should be:

```python
[
    # P1 tier (most recent first)
    {"priority": "P1", "created_at": "2025-11-10T11:00:00Z", "number": 276, "title": "PM workflow improvement B"},
    {"priority": "P1", "created_at": "2025-11-08T14:00:00Z", "number": 271, "title": "PM workflow improvement A"},
    
    # P2 tier (most recent first)
    {"priority": "P2", "created_at": "2025-11-09T10:00:00Z", "number": 260, "title": "Test boilerplate"},
    {"priority": "P2", "created_at": "2025-11-07T11:00:00Z", "number": 258, "title": "Old implementation feedback"},
    
    # P3 tier (most recent first)
    {"priority": "P3", "created_at": "2025-11-10T09:00:00Z", "number": 262, "title": "Missing context issue"},
    {"priority": "P3", "created_at": "2025-11-06T10:00:00Z", "number": 255, "title": "Very old unclear feedback"},
]
```

### Processing Order

Issues processed in this sequence:
1. **First**: #276 (P1, 2025-11-10) - Most recent process modeling improvement
2. **Second**: #271 (P1, 2025-11-08) - Older process modeling improvement
3. **Third**: #260 (P2, 2025-11-09) - Recent implementation feedback
4. **Fourth**: #258 (P2, 2025-11-07) - Older implementation feedback
5. **Fifth**: #262 (P3, 2025-11-10) - Recent but missing context
6. **Sixth**: #255 (P3, 2025-11-06) - Old and missing context

### Success Criteria
- ✅ All P1 issues processed before any P2 issues
- ✅ All P2 issues processed before any P3 issues
- ✅ Within P1: Most recent (2025-11-10) before older (2025-11-08)
- ✅ Within P2: Most recent (2025-11-09) before older (2025-11-07)
- ✅ Within P3: Most recent (2025-11-10) before oldest (2025-11-06)
- ✅ High-value, recent feedback gets earliest attention

## Test Result

**Status**: ⏳ PENDING (waiting for tabletop simulation)

**Actual Outcome**: [To be filled during simulation]

**Verification Method**:
```python
# Verify sort order
assert processing_queue[0]["number"] == 276  # P1, newest
assert processing_queue[1]["number"] == 271  # P1, older
assert processing_queue[2]["number"] == 260  # P2, newest
assert processing_queue[3]["number"] == 258  # P2, older
assert processing_queue[4]["number"] == 262  # P3, newest
assert processing_queue[5]["number"] == 255  # P3, oldest
```

**Notes**: [Any observations during testing]

## Regression Test Value

**Retain as regression test?** YES
- Tests critical sorting logic
- Ensures high-value feedback isn't delayed by low-value backlog
- Clear, verifiable outcomes
- Demonstrates complete triage workflow
