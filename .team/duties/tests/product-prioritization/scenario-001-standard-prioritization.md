# Scenario 001: Standard Prioritization

**Duty**: Product Prioritization  
**Type**: End-to-End Scenario  
**Complexity**: Low  
**Created**: 2025-11-12

---

## Purpose

Test product prioritization duty's ability to perform standard backlog prioritization with mixed priorities and select items for implementation queue.

---

## Context

A work item requests backlog prioritization. The backlog has items with various priorities, and the implementation queue has available capacity.

---

## Starting State

**Work Item #2001** (Triggering Prioritization):
- **Title**: "Product Backlog Prioritization - November 2025"
- **Description**: "Run standard prioritization of product backlog"
- **Duty**: `product-prioritization`
- **Status**: `open`

**Backlog Items** (All have `product-backlog` duty):
- **#2010**: "Add logging framework integration" - Feature request (normal priority)
- **#2011**: "Refactor error handling in TransformBlock" - Tech debt (quick win)
- **#2012**: "Fix memory leak in ProducerBlock" - Bug (high impact)
- **#2013**: "Add performance benchmarks" - Enhancement (nice-to-have)
- **#2014**: "Update documentation for BatchBlock" - Documentation (low priority)
- **#2015**: "Add cancellation token support" - Feature (high value)
- **#2016**: "Remove deprecated API methods" - Tech debt (medium effort)
- **#2017**: "Fix race condition in channel creation" - Bug (critical)

**Implementation Queue**:
- Currently has **3 open items** (limit is 10)
- **7 slots available**

---

## Procedure to Follow

[Product Prioritization Duty](../../../.team/duties/PRODUCT_PRIORITIZATION_DUTY.md)

---

## Expected Steps

### Step 1: Query Product Backlog Queue

```python
# Query product prioritization queue
prio_items = query_work_items_by_duty(duty="product-prioritization")
# Returns: [#2001]

# Get details
details = get_work_item_details(work_item_id="2001")

# Check multi-phase
is_multi = is_multi_phase(work_item_id="2001")  # Returns False
```

### Step 2: Collect All Backlog Items

```python
# Collect all backlog items
backlog_items = query_work_items_by_duty(
    duty="product-backlog",
    state="open"
)
# Returns: [#2010, #2011, #2012, #2013, #2014, #2015, #2016, #2017]

print(f"Collected {len(backlog_items)} open backlog items")
# Output: "Collected 8 open backlog items"
```

### Step 3: Apply Prioritization Criteria

Analyze items and assign priorities based on policy:

**Priority Analysis**:
- **#2017** (race condition) - **Priority 1** (critical bug, affects correctness)
- **#2012** (memory leak) - **Priority 2** (high-impact bug)
- **#2015** (cancellation token) - **Priority 3** (high-value feature)
- **#2011** (error handling refactor) - **Priority 3** (tech debt quick win - satisfies "at least 1" policy)
- **#2010** (logging integration) - **Priority 3** (normal feature)
- **#2016** (deprecated API) - **Priority 4** (tech debt, medium effort)
- **#2013** (benchmarks) - **Priority 4** (nice-to-have)
- **#2014** (documentation) - **Priority 5** (low priority)

**Sorted List**:
1. #2017 (P1)
2. #2012 (P2)
3. #2015 (P3)
4. #2011 (P3) - Tech debt
5. #2010 (P3)
6. #2016 (P4)
7. #2013 (P4)
8. #2014 (P5)

### Step 4: Present Prioritization for Review

```python
# Use comment patterns procedure
comment_text = """[Copilot-Duty: Product Prioritization] 📊 **Prioritization Analysis Complete**

I've analyzed 8 backlog items and assigned priorities based on the policy.

## Prioritized Backlog Items

**Priority 1 (Highest)**:
- #2017: Fix race condition in channel creation - Critical bug affecting correctness

**Priority 2 (High)**:
- #2012: Fix memory leak in ProducerBlock - High-impact bug

**Priority 3 (Normal)**:
- #2015: Add cancellation token support - High-value feature
- #2011: Refactor error handling in TransformBlock - Tech debt quick win
- #2010: Add logging framework integration - Normal feature request

**Priority 4 (Lower)**:
- #2016: Remove deprecated API methods - Tech debt, medium effort
- #2013: Add performance benchmarks - Nice-to-have enhancement

**Priority 5 (Lowest)**:
- #2014: Update documentation for BatchBlock - Low priority documentation

---

## Prioritization Policy Applied

✅ Security vulnerabilities assessed (none found)
✅ Priority overrides honored (none present)
✅ Tech debt items identified (2 found, 1 quick win selected)
✅ Standard selection criteria applied

---

**Next Step: Selection**

Implementation queue has 7 available slots. I can select up to 7 items.

To proceed with selection, reply with:
- `@copilot proceed with selection`
- `@copilot skip` - Review only, no selection
"""

add_work_item_comment(work_item_id="2001", text=comment_text)
```

**Wait for reviewer response: Assume reviewer responds with `@copilot proceed with selection`**

### Step 5: Check Implementation Queue Capacity

```python
# Query implementation queue
impl_queue = query_work_items_by_duty(duty="implementation", state="open")
current_queue_size = len(impl_queue)  # Returns 3

IMPLEMENTATION_QUEUE_LIMIT = 10
available_slots = 10 - 3  # = 7

print(f"Implementation queue: {current_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items")
print(f"Available slots: {available_slots}")
# Output: "Implementation queue: 3/10 items"
# Output: "Available slots: 7"
```

**7 slots available** - Proceed to selection

### Step 6: Select Items for Implementation Queue

```python
# Select top 7 items (all P1, P2, and P3 items)
items_to_select = [
    {"work_item_id": "2017", "priority": 1, "rationale": "Critical race condition bug"},
    {"work_item_id": "2012", "priority": 2, "rationale": "High-impact memory leak"},
    {"work_item_id": "2015", "priority": 3, "rationale": "High-value feature"},
    {"work_item_id": "2011", "priority": 3, "rationale": "Tech debt quick win"},
    {"work_item_id": "2010", "priority": 3, "rationale": "Normal feature request"},
    {"work_item_id": "2016", "priority": 4, "rationale": "Tech debt, fits in available slots"},
    {"work_item_id": "2013", "priority": 4, "rationale": "Enhancement, fits in available slots"}
]

# Hand over to implementation duty
for item in items_to_select:
    assign_work_item_to_duty(
        work_item_id=item['work_item_id'],
        duty="implementation"
    )
    
    add_work_item_comment(
        work_item_id=item['work_item_id'],
        text=f"""[Copilot-Duty: Product Prioritization] 🔄 **Selected for Implementation**

This item has been prioritized and moved to the implementation queue.

**Priority**: {item['priority']}
**Rationale**: {item['rationale']}

See `.team/duties/IMPLEMENTATION_DUTY.md` for implementation guidance.
"""
    )
```

### Step 7: Update Prioritization Analysis

Update product-backlog analysis document:
- Selected items: 7 items (P1: 1, P2: 1, P3: 3, P4: 2)
- Remaining: 1 item (P5: 1)
- Queue status: 10/10 (now full)
- Tech debt: 1 quick win selected (policy met)

### Step 8: Report Completion

```python
summary_comment = """[Copilot-Duty: Product Prioritization] ✅ **Prioritization and Selection Complete**

## Summary

**Backlog Analyzed**: 8 open items
**Items Selected**: 7 items moved to implementation queue
**Implementation Queue**: 10/10 items (now full)

## Selected Items

1. #2017 (P1): Fix race condition in channel creation
2. #2012 (P2): Fix memory leak in ProducerBlock
3. #2015 (P3): Add cancellation token support
4. #2011 (P3): Refactor error handling in TransformBlock (Tech debt)
5. #2010 (P3): Add logging framework integration
6. #2016 (P4): Remove deprecated API methods
7. #2013 (P4): Add performance benchmarks

## Implementation Queue Status

**Before**: 3 items
**After**: 10 items  
**Remaining Capacity**: 0 slots (full)

## Prioritization Details

**Priority Distribution** (remaining in backlog):
- P1 (Highest): 0 items
- P2 (High): 0 items
- P3 (Normal): 0 items
- P4-P5 (Lower): 1 item (#2014)

**Policy Compliance**:
- ✅ Security vulnerabilities prioritized (none found)
- ✅ Tech debt requirement met (1 quick win selected)
- ✅ Priority overrides honored (none present)
- ✅ Queue capacity managed (7 slots used)

**Updated Document**: Product-backlog analysis

---

**Next Steps**:
- Implementation team: Select from items now in implementation queue
- Product team: Queue is now full, next prioritization after items complete
"""

add_work_item_comment(work_item_id="2001", text=summary_comment)
```

---

## Expected Outcome

**Duty Assignment**:
- #2001: Remains `product-prioritization` (triggering work item)
- #2017, #2012, #2015, #2011, #2010, #2016, #2013: Changed from `product-backlog` to `implementation`
- #2014: Remains `product-backlog` (not selected)

**Comments Added**:
- Prioritization analysis comment on #2001
- Handover comment on each selected item (#2017, #2012, #2015, #2011, #2010, #2016, #2013)
- Completion summary comment on #2001

**Documents Updated**:
- Product-backlog analysis document with selection results

**Semantic Operations Used** (✅ Correct):
- `query_work_items_by_duty()`
- `get_work_item_details()`
- `is_multi_phase()`
- `assign_work_item_to_duty()`
- `add_work_item_comment()`

**No Platform-Specific Code** (✅ Correct):
- No GitHub MCP tools used directly
- All operations through semantic layer

**Procedures Referenced** (✅ Correct):
- Comment Patterns Procedure (for comment format)
- Handover Procedure (for duty transitions)
- Multi-Phase Work Items (for checking parent)

---

## Test Result

**PASS** / FAIL

**Notes**: [Any deviations or observations during execution]

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial scenario for standard prioritization testing |
