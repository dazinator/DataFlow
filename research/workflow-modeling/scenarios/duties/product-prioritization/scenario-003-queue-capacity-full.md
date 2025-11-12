# Scenario 003: Queue Capacity Full

**Duty**: Product Prioritization  
**Type**: Edge Case Scenario  
**Complexity**: Low  
**Created**: 2025-11-12

---

## Purpose

Test product prioritization duty's handling of the edge case where implementation queue is at or over capacity, requiring graceful handling without selection.

---

## Context

Prioritization is requested, but the implementation queue is already at capacity (10/10 items). Duty should analyze and present priorities but NOT select any items for implementation.

---

## Starting State

**Work Item #2100** (Triggering Prioritization):
- **Title**: "Product Backlog Prioritization - Check Status"
- **Duty**: `product-prioritization`
- **Status**: `open`

**Backlog Items** (All have `product-backlog` duty):
- **#2101**: "Critical bug in error handling" - P1
- **#2102**: "Add caching layer" - P2
- **#2103**: "Refactor connection pooling" - P3 (Tech debt)
- **#2104**: "Update logging framework" - P3

**Implementation Queue**:
- Currently has **10 open items** (limit is 10)
- **0 slots available** ⚠️

---

## Procedure to Follow

[Product Prioritization Duty](../../../.team/duties/PRODUCT_PRIORITIZATION_DUTY.md)

---

## Expected Steps

### Step 1: Query Product Backlog Queue

```python
prio_items = query_work_items_by_duty(duty="product-prioritization")
details = get_work_item_details(work_item_id="2100")
is_multi = is_multi_phase(work_item_id="2100")  # False
```

### Step 2: Collect All Backlog Items

```python
backlog_items = query_work_items_by_duty(duty="product-backlog", state="open")
# Returns: [#2101, #2102, #2103, #2104]

print(f"Collected {len(backlog_items)} open backlog items")
# Output: "Collected 4 open backlog items"
```

### Step 3: Apply Prioritization Criteria

**Priority Analysis**:
- **#2101** - Critical bug - **Priority 1**
- **#2102** - Caching layer - **Priority 2**
- **#2103** - Tech debt refactor - **Priority 3**
- **#2104** - Logging update - **Priority 3**

**Sorted List**:
1. #2101 (P1)
2. #2102 (P2)
3. #2103 (P3) - Tech debt
4. #2104 (P3)

### Step 4: Present Prioritization for Review

```python
comment_text = """[Copilot-Duty: Product Prioritization] 📊 **Prioritization Analysis Complete**

I've analyzed 4 backlog items and assigned priorities based on the policy.

## Prioritized Backlog Items

**Priority 1 (Highest)**:
- #2101: Critical bug in error handling - Critical bug affecting core functionality

**Priority 2 (High)**:
- #2102: Add caching layer - High-value performance improvement

**Priority 3 (Normal)**:
- #2103: Refactor connection pooling - Tech debt (quick win)
- #2104: Update logging framework - Standard enhancement

---

## Prioritization Policy Applied

✅ Security vulnerabilities assessed (none found)
✅ Priority overrides honored (none present)
✅ Tech debt items identified (1 found)
✅ Standard selection criteria applied

---

**Next Step: Selection**

⚠️ **Implementation queue is at capacity** (10/10 items). I'll check capacity before selection.

To proceed with capacity check, reply with:
- `@copilot proceed with selection`
"""

add_work_item_comment(work_item_id="2100", text=comment_text)
```

**Wait for reviewer: Assume `@copilot proceed with selection`**

### Step 5: Check Implementation Queue Capacity

```python
impl_queue = query_work_items_by_duty(duty="implementation", state="open")
current_queue_size = len(impl_queue)  # Returns 10

IMPLEMENTATION_QUEUE_LIMIT = 10
available_slots = 10 - 10  # = 0

print(f"Implementation queue: {current_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items")
# Output: "Implementation queue: 10/10 items"
print(f"Available slots: {available_slots}")
# Output: "Available slots: 0"
```

**0 slots available** - ⚠️ **STOP HERE, DO NOT SELECT**

### Step 6: Report Queue Full (Skip Selection)

```python
# NO selection performed
# Instead, report queue status

queue_full_comment = """[Copilot-Duty: Product Prioritization] ⏸️ **Implementation Queue Full**

The implementation queue currently has 10 open items (limit: 10).

**No items will be selected** until implementation team completes current work.

## Prioritized Backlog Items (Awaiting Capacity)

Ready for selection when capacity allows:
1. #2101 (P1): Critical bug in error handling
2. #2102 (P2): Add caching layer
3. #2103 (P3): Refactor connection pooling (Tech debt)
4. #2104 (P3): Update logging framework

## Recommendation

**Wait for implementation queue to clear**, then re-run prioritization.

Current implementation queue items: [Would list top 5-10 items in actual execution]

**Next Steps**:
- Implementation team: Complete current work items to free up slots
- Product team: Re-trigger prioritization when capacity available
- Monitor: Check queue status periodically

See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for configuration.
"""

add_work_item_comment(work_item_id="2100", text=queue_full_comment)
```

### Step 7: Update Prioritization Analysis (Optional)

Update product-backlog analysis document with prioritization results:
- **Selection**: None (queue at capacity)
- **Queue Status**: 10/10 (full)
- **Awaiting Capacity**: 4 items (P1: 1, P2: 1, P3: 2)
- **Note**: Prioritization performed but selection deferred

### Step 8: Complete (No Items Selected)

No further action - prioritization complete, selection will occur in future when capacity allows.

---

## Expected Outcome

**Duty Assignment**:
- #2100: Remains `product-prioritization` (triggering work item)
- #2101, #2102, #2103, #2104: **Remain `product-backlog`** (NOT moved to implementation)

**No Handovers**:
- ✅ Correct: Zero items handed over to implementation duty
- ✅ Correct: All backlog items stay in product-backlog duty

**Comments Added**:
- Prioritization analysis comment on #2100
- Queue full notification comment on #2100
- **NO handover comments** on backlog items (correct behavior)

**Documents Updated**:
- Product-backlog analysis document (prioritization recorded, selection deferred)

**Edge Case Handling** (✅ Correct):
- ✅ Detected queue at capacity (available_slots = 0)
- ✅ Did NOT select or hand over any items
- ✅ Reported queue status clearly
- ✅ Provided actionable next steps
- ✅ Preserved prioritization analysis for future use

**Semantic Operations Used** (✅ Correct):
- `query_work_items_by_duty()` - Used correctly
- `get_work_item_details()` - Used correctly
- `is_multi_phase()` - Used correctly
- `add_work_item_comment()` - Used correctly
- `assign_work_item_to_duty()` - **NOT used** (correct - no selection)

**No Platform-Specific Code** (✅ Correct):
- All operations through semantic layer

**Procedures Referenced** (✅ Correct):
- Comment Patterns Procedure (for comment format)
- Multi-Phase Work Items (for checking parent)
- Handover Procedure (NOT used - no handover occurred, which is correct)

---

## Test Result

**PASS** / FAIL

**Notes**: [Verify graceful handling of zero capacity and no selection occurs]

---

## Key Validation Points

1. ✅ Prioritization analysis still performed (provides value even without selection)
2. ✅ Zero items selected when capacity is 0
3. ✅ Clear communication about queue status
4. ✅ No incorrect handovers or duty changes
5. ✅ Actionable next steps provided
6. ✅ Analysis preserved for future selection

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial scenario for queue capacity full edge case |
