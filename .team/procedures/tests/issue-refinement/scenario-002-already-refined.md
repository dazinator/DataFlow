# Scenario: Already Refined Multi-Phase Plan (Validation Check)

## Context
Implementation duty receives a work item that has already been refined into sub-issues. The pre-flight check should detect this and validate the existing structure rather than creating new sub-issues.

## Starting Point
- **Current work item**: #420 (Distributed Caching System)
- **Duty**: implementation
- **Parent work item**: None (this is the parent)
- **Sub-issues**: Already exists (3 sub-issues created previously)
- **Status**: Just handed over from product prioritization
- **Agent**: Implementation duty

## Work Item Content

**Title**: Distributed Caching System

**Description**:
```markdown
## Multi-Phase Plan

⚠️ **This is a parent issue for a multi-phase plan.**

Work is split into the following sub-issues:

- [ ] Phase 1: Redis Integration (#421)
- [ ] Phase 2: Cache Configuration (#422)
- [ ] Phase 3: Performance Testing (#423)

## Original Description

## Overview
Add distributed caching support using Redis for multi-process pipeline scenarios.

### Phase 1: Redis Integration
**Deliverables**: StackExchange.Redis client integration
**Dependencies**: None

### Phase 2: Cache Configuration
**Deliverables**: Pipeline builder cache configuration API
**Dependencies**: Phase 1 complete

### Phase 3: Performance Testing
**Deliverables**: Benchmark and optimize cache operations
**Dependencies**: Phases 1-2 complete
```

**Existing Sub-Issues**:
- #421: [Phase 1] Distributed Caching - Redis Integration (status: open, duty: implementation)
- #422: [Phase 2] Distributed Caching - Cache Configuration (status: open, duty: implementation)
- #423: [Phase 3] Distributed Caching - Performance Testing (status: open, duty: implementation)

## Steps to Follow (from Issue Refinement Procedure)

### Step 1: Analyze Work Item for Multi-Phase Indicators

```python
# Agent: Implementation duty executing Step 2.5 (pre-flight check)
details = get_work_item_details(work_item_id="420")
title = details['title']
description = details['description']

# Check for multi-phase indicators
# ✅ Found: "Multi-Phase Plan" marker in description
# ✅ Found: Sub-issue links in checklist
# ✅ Found: Original multi-phase structure

# Decision: Multi-phase plan detected - proceed to Step 2
```

### Step 2: Check Existing Sub-Issues

```python
children = list_child_work_items(work_item_id="420")
# Returns: [
#   {"id": "421", "title": "[Phase 1] Distributed Caching - Redis Integration", "status": "open"},
#   {"id": "422", "title": "[Phase 2] Distributed Caching - Cache Configuration", "status": "open"},
#   {"id": "423", "title": "[Phase 3] Distributed Caching - Performance Testing", "status": "open"}
# ]

print(f"Found {len(children)} existing sub-issues")
# Proceed to Step 3 for validation
```

### Step 3: Extract Phase Structure (Validation Mode)

```python
# Extract phases from description to validate against existing sub-issues
detected_phases = [
    {"number": 1, "name": "Redis Integration"},
    {"number": 2, "name": "Cache Configuration"},
    {"number": 3, "name": "Performance Testing"}
]

# Compare with existing sub-issues
# ✅ Sub-issue #421 matches Phase 1
# ✅ Sub-issue #422 matches Phase 2
# ✅ Sub-issue #423 matches Phase 3

# All phases have corresponding sub-issues
```

### Step 4: Present Validation Results

Since the issue is already refined, present validation results instead of refinement plan:

```python
validation_comment = """[Copilot-Duty: Implementation] ✅ **Multi-Phase Plan Already Refined**

## Validation

This work item has already been properly refined into sub-issues.

**Existing Structure**:
- ✅ 3 sub-issues created
- ✅ All sub-issues match detected phases
- ✅ Parent-child links established
- ✅ All sub-issues assigned to implementation duty

**Sub-Issues**:
1. **#421**: [Phase 1] Distributed Caching - Redis Integration
2. **#422**: [Phase 2] Distributed Caching - Cache Configuration
3. **#423**: [Phase 3] Distributed Caching - Performance Testing

## Next Steps for Implementation

**This PR**: Should be closed (work on parent not appropriate)

**To Continue**:
- Start with Phase 1 (#421)
- Create new PR for Phase 1
- Each phase will have its own PR

See [Multi-Phase Work Items Procedure](../.team/procedures/multi-phase-work-items.md) for complete guidance.
"""

add_work_item_comment(
    work_item_id="420",
    text=validation_comment
)

# Return refinement status
return {
    "refined": False,  # Not newly refined
    "has_sub_issues": True,
    "sub_issue_count": 3,
    "first_phase_id": "421",
    "next_action": "close_pr_and_work_on_first_phase"
}
```

### Step 5: Implementation Duty Response

```python
# Implementation duty receives refinement result
refinement_result = check_issue_refinement(work_item_id="420")

# refinement_result = {
#     "refined": False,
#     "has_sub_issues": True,
#     "first_phase_id": "421"
# }

# Since has_sub_issues is True, treat similarly to newly refined
first_phase_number = get_work_item_details("421")['number']

add_work_item_comment(
    work_item_id="420",
    text=f"""[Copilot-Duty: Implementation] 🔄 **Work on Sub-Issues**

This is a parent issue for a multi-phase plan with existing sub-issues.

**First Phase**: #{first_phase_number}

**Next Steps**:
- This PR should be closed (work on parent not appropriate)
- Create new PR for Phase 1 sub-issue (#{first_phase_number})
- Each phase will have its own PR
"""
)

# STOP implementation on parent
```

## Expected Outcome

**✅ PASS** - Already refined validation successful when:

1. **Multi-phase structure detected**:
   - ✅ Identified as multi-phase plan
   - ✅ Found existing sub-issues

2. **Validation performed**:
   - ✅ Checked sub-issues against phases
   - ✅ Verified all phases have sub-issues
   - ✅ Confirmed parent-child links

3. **No new sub-issues created**:
   - ✅ Recognized existing refinement
   - ✅ Did not duplicate sub-issues
   - ✅ No modifications made to structure

4. **Guidance provided**:
   - ✅ Validation results communicated
   - ✅ Next steps clear (work on first sub-issue)
   - ✅ Recommended closing PR for parent

5. **Semantic operations used exclusively**:
   - ✅ No platform-specific code
   - ✅ All operations use kernel layer

6. **Return value correct**:
   - ✅ `refined: False` (not newly refined)
   - ✅ `has_sub_issues: True`
   - ✅ `first_phase_id` provided

## Actual Outcome

**[To be filled during tabletop execution]**

Result: PASS / FAIL

Notes:
- [Any observations about validation logic]
- [Any edge cases discovered]
- [Suggestions for improvement]
