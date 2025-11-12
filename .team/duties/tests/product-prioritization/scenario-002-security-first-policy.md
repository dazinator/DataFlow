# Scenario 002: Security-First Policy

**Duty**: Product Prioritization  
**Type**: End-to-End Scenario  
**Complexity**: Medium  
**Created**: 2025-11-12

---

## Purpose

Test product prioritization duty's ability to prioritize security vulnerabilities correctly using core vs non-core assessment and CVE criticality ratings.

---

## Context

Backlog contains security vulnerabilities in different locations (core code vs non-core) with varying CVE ratings. Security policy must be applied correctly.

---

## Starting State

**Work Item #2050** (Triggering Prioritization):
- **Title**: "Product Backlog Prioritization - Security Focus"
- **Duty**: `product-prioritization`
- **Status**: `open`

**Backlog Items** (All have `product-backlog` duty):
- **#2051**: "CVE-2024-1234: SQL Injection in DataFlow.Core authentication" 
  - Location: `/src/DataFlow.Core/Auth.cs` (Core code)
  - CVE Rating: **Critical**
  - Category: Security
  
- **#2052**: "CVE-2024-5678: XSS vulnerability in sample dashboard"
  - Location: `/sample/Dashboard/Views/Index.cshtml` (Non-core)
  - CVE Rating: **High**
  - Category: Security
  
- **#2053**: "Dependency vulnerability in test project"
  - Location: `/src/DataFlow.Tests/` (Non-core - test project)
  - CVE Rating: **Medium**
  - Category: Security
  
- **#2054**: "Potential timing attack in BlockBase"
  - Location: `/src/DataFlow.Core/BlockBase.cs` (Core code)
  - CVE Rating: None (security analysis, no CVE assigned)
  - Category: Security
  
- **#2055**: "Add retry logic to HTTP blocks"
  - Category: Feature request

**Implementation Queue**:
- Currently has **8 open items** (limit is 10)
- **2 slots available**

---

## Procedure to Follow

[Product Prioritization Duty](../../../.team/duties/PRODUCT_PRIORITIZATION_DUTY.md)

---

## Expected Steps

### Step 1: Query Product Backlog Queue

```python
prio_items = query_work_items_by_duty(duty="product-prioritization")
details = get_work_item_details(work_item_id="2050")
is_multi = is_multi_phase(work_item_id="2050")  # False
```

### Step 2: Collect All Backlog Items

```python
backlog_items = query_work_items_by_duty(duty="product-backlog", state="open")
# Returns: [#2051, #2052, #2053, #2054, #2055]

print(f"Collected {len(backlog_items)} open backlog items")
# Output: "Collected 5 open backlog items"
```

### Step 3: Apply Prioritization Criteria

**Security Risk Assessment**:

1. **#2051** - SQL Injection in Core Code
   - Location: `/src/DataFlow.Core/` (Core)
   - CVE: Critical
   - **Priority: 1** (Highest - Critical CVE in core code)

2. **#2054** - Timing Attack in Core Code
   - Location: `/src/DataFlow.Core/` (Core)
   - CVE: None
   - **Priority: 3** (Normal - Core code but no CVE or Low CVE)

3. **#2052** - XSS in Sample Dashboard
   - Location: `/sample/` (Non-core)
   - CVE: High
   - **Priority: 3** (Normal - Non-core capped at P3 maximum)

4. **#2053** - Dependency Vulnerability in Tests
   - Location: `/src/DataFlow.Tests/` (Non-core - test project)
   - CVE: Medium
   - **Priority: 3** (Normal - Non-core capped at P3)

5. **#2055** - Retry Logic Feature
   - Category: Feature
   - **Priority: 3** (Normal - Standard feature)

**Sorted List**:
1. #2051 (P1) - Critical security in core
2. #2054 (P3) - Security in core, no CVE
3. #2052 (P3) - Security in non-core
4. #2053 (P3) - Security in test project
5. #2055 (P3) - Feature request

### Step 4: Present Prioritization for Review

```python
comment_text = """[Copilot-Duty: Product Prioritization] 📊 **Prioritization Analysis Complete**

I've analyzed 5 backlog items and assigned priorities based on the policy.

## Prioritized Backlog Items

**Priority 1 (Highest)**:
- #2051: CVE-2024-1234: SQL Injection in DataFlow.Core authentication - **Critical CVE in core code**

**Priority 3 (Normal)**:
- #2054: Potential timing attack in BlockBase - Security issue in core code (no CVE assigned)
- #2052: CVE-2024-5678: XSS vulnerability in sample dashboard - High CVE but non-core (sample)
- #2053: Dependency vulnerability in test project - Medium CVE in test project (non-core)
- #2055: Add retry logic to HTTP blocks - Standard feature request

---

## Security Risk Assessment

✅ **Core Code Vulnerabilities**:
- 1 Critical CVE → Priority 1 (#2051)
- 1 No CVE → Priority 3 (#2054)

✅ **Non-Core Vulnerabilities**:
- Sample dashboard (High CVE) → Priority 3 (capped) (#2052)
- Test project (Medium CVE) → Priority 3 (capped) (#2053)

## Prioritization Policy Applied

✅ Security vulnerabilities assessed (4 security items, core vs non-core)
✅ CVE criticality ratings applied
✅ Priority overrides honored (none present)
✅ Tech debt items identified (none found)
✅ Standard selection criteria applied

---

**Next Step: Selection**

Implementation queue has 2 available slots.

To proceed with selection, reply with:
- `@copilot proceed with selection`
"""

add_work_item_comment(work_item_id="2050", text=comment_text)
```

**Wait for reviewer: Assume `@copilot proceed with selection`**

### Step 5: Check Implementation Queue Capacity

```python
impl_queue = query_work_items_by_duty(duty="implementation", state="open")
current_queue_size = len(impl_queue)  # Returns 8

IMPLEMENTATION_QUEUE_LIMIT = 10
available_slots = 10 - 8  # = 2

print(f"Implementation queue: {current_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items")
# Output: "Implementation queue: 8/10 items"
print(f"Available slots: {available_slots}")
# Output: "Available slots: 2"
```

**2 slots available** - Select top 2 items

### Step 6: Select Items for Implementation Queue

```python
# Select top 2 items
items_to_select = [
    {"work_item_id": "2051", "priority": 1, "rationale": "Critical CVE in core code - SQL injection"},
    {"work_item_id": "2054", "priority": 3, "rationale": "Security issue in core code (timing attack)"}
]

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
**Security Classification**: {'Core code' if item['work_item_id'] in ['2051', '2054'] else 'Non-core'}

See `.team/duties/IMPLEMENTATION_DUTY.md` for implementation guidance.
"""
    )
```

**Note**: Items #2052, #2053, #2055 remain in backlog (not selected due to capacity limit)

### Step 7: Update Prioritization Analysis

Update product-backlog analysis document:
- Selected items: 2 items (P1: 1, P3: 1) - Both security items in core code
- Remaining: 3 items (all P3) - 2 security (non-core), 1 feature
- Queue status: 10/10 (now full)
- Security priority enforced (Critical CVE selected first)

### Step 8: Report Completion

```python
summary_comment = """[Copilot-Duty: Product Prioritization] ✅ **Prioritization and Selection Complete**

## Summary

**Backlog Analyzed**: 5 open items (4 security, 1 feature)
**Items Selected**: 2 items moved to implementation queue
**Implementation Queue**: 10/10 items (now full)

## Selected Items

1. #2051 (P1): CVE-2024-1234: SQL Injection in DataFlow.Core - **Critical CVE in core code**
2. #2054 (P3): Potential timing attack in BlockBase - Security in core code

## Implementation Queue Status

**Before**: 8 items
**After**: 10 items  
**Remaining Capacity**: 0 slots (full)

## Prioritization Details

**Priority Distribution** (remaining in backlog):
- P1 (Highest): 0 items
- P2 (High): 0 items
- P3 (Normal): 3 items (#2052, #2053, #2055)
- P4-P5 (Lower): 0 items

**Remaining Security Items** (awaiting capacity):
- #2052: XSS in sample dashboard (High CVE, non-core)
- #2053: Dependency vulnerability in tests (Medium CVE, non-core)

**Policy Compliance**:
- ✅ **Security vulnerabilities prioritized** (Critical CVE selected first)
- ✅ **Core vs non-core assessment applied** (Core items prioritized over non-core)
- ✅ **CVE ratings honored** (Critical > High > Medium > None)
- ✅ Queue capacity managed (2 slots used, queue now full)

**Security Note**: All core code security issues selected. Non-core security items (#2052, #2053) will be prioritized in next cycle when capacity allows.

**Updated Document**: Product-backlog analysis

---

**Next Steps**:
- Implementation team: Address critical security vulnerability #2051 immediately
- Security team: Review remaining non-core vulnerabilities for next cycle
"""

add_work_item_comment(work_item_id="2050", text=summary_comment)
```

---

## Expected Outcome

**Duty Assignment**:
- #2050: Remains `product-prioritization` (triggering work item)
- #2051, #2054: Changed from `product-backlog` to `implementation` (security in core)
- #2052, #2053, #2055: Remain `product-backlog` (awaiting capacity)

**Comments Added**:
- Prioritization analysis comment on #2050 (with security risk assessment)
- Handover comment on #2051 and #2054 (with security classification)
- Completion summary comment on #2050 (with security policy compliance)

**Documents Updated**:
- Product-backlog analysis document with security-focused selection

**Policy Validation**:
- ✅ Core code Critical CVE gets Priority 1
- ✅ Core code no-CVE gets Priority 3
- ✅ Non-core High CVE capped at Priority 3
- ✅ Non-core Medium CVE capped at Priority 3
- ✅ Security items prioritized over feature requests

**Semantic Operations Used** (✅ Correct):
- All operations via semantic layer
- No platform-specific code

---

## Test Result

**PASS** / FAIL

**Notes**: [Verify security risk assessment logic and CVE criticality handling]

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial scenario for security-first policy testing |
