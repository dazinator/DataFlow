# Scenario: Multi-Phase Plan with Explicit Phases (Refinement Needed)

## Context
Implementation duty receives a work item from product prioritization that describes a user authentication system. The work item clearly outlines multiple phases but hasn't been broken down into sub-issues yet. Need to refine it before implementation can proceed.

## Starting Point
- **Current work item**: #415 (Add User Authentication System)
- **Duty**: implementation
- **Parent work item**: None
- **Sub-issues**: None (needs refinement)
- **Status**: Just handed over from product prioritization
- **Agent**: Implementation duty

## Work Item Content

**Title**: Add User Authentication System

**Description**:
```markdown
## Overview
Implement complete user authentication system with JWT tokens and session management.

## Multi-Phase Plan

### Phase 1: Core Authentication
**Deliverables**: JWT generation/validation, user login/logout endpoints
**Dependencies**: None
**Success Criteria**: Users can log in and receive valid JWT tokens
**Estimated Effort**: 3 days

### Phase 2: Session Management
**Deliverables**: Session storage, refresh tokens, timeout handling
**Dependencies**: Phase 1 complete
**Success Criteria**: Sessions persist correctly and can be refreshed
**Estimated Effort**: 4 days

### Phase 3: Security Hardening
**Deliverables**: Rate limiting, brute force protection, audit logging
**Dependencies**: Phases 1-2 complete
**Success Criteria**: System resistant to common attacks
**Estimated Effort**: 3 days

## Overall Success Criteria
- Complete authentication flow working
- Security best practices implemented
- All tests passing
- Documentation complete
```

## Steps to Follow (from Issue Refinement Procedure)

### Step 1: Analyze Work Item for Multi-Phase Indicators

```python
# Agent: Implementation duty executing Step 2.5 (pre-flight check)
from procedures.issue_refinement import check_issue_refinement

details = get_work_item_details(work_item_id="415")
title = details['title']
description = details['description']

# Check for multi-phase indicators
# ✅ Found: "## Multi-Phase Plan" section
# ✅ Found: Multiple "### Phase N:" sections (3 phases)
# ✅ Found: Sequential dependencies between phases
# ✅ Found: Estimated effort > 1 week (10 days total)

# Decision: Multi-phase plan detected - proceed to Step 2
```

### Step 2: Check Existing Sub-Issues

```python
children = list_child_work_items(work_item_id="415")
# Returns: [] (empty - no sub-issues yet)

print("No sub-issues found - needs refinement")
# Proceed to Step 3
```

### Step 3: Extract Phase Structure

```python
# Extract phases from description
phases = [
    {
        "number": 1,
        "name": "Core Authentication",
        "deliverables": "JWT generation/validation, user login/logout endpoints",
        "dependencies": "None",
        "success_criteria": "Users can log in and receive valid JWT tokens",
        "effort": "3 days",
        "type": "implementation",
        "duty": "implementation"
    },
    {
        "number": 2,
        "name": "Session Management",
        "deliverables": "Session storage, refresh tokens, timeout handling",
        "dependencies": "Phase 1 complete",
        "success_criteria": "Sessions persist correctly and can be refreshed",
        "effort": "4 days",
        "type": "implementation",
        "duty": "implementation"
    },
    {
        "number": 3,
        "name": "Security Hardening",
        "deliverables": "Rate limiting, brute force protection, audit logging",
        "dependencies": "Phases 1-2 complete",
        "success_criteria": "System resistant to common attacks",
        "effort": "3 days",
        "type": "implementation",
        "duty": "implementation"
    }
]
```

### Step 4: Present Refinement Plan to Reviewer

```python
refinement_comment = """[Copilot-Duty: Implementation] 🔍 **Multi-Phase Plan Detected**

## Analysis

This work item appears to represent a multi-phase plan rather than a single implementable unit.

**Indicators Found**:
- Explicit "Multi-Phase Plan" section in description
- 3 distinct phases with clear boundaries
- Sequential dependencies between phases
- Total estimated effort: 10 days (exceeds single PR scope)

## Detected Phases

3 phases identified:

### Phase 1: Core Authentication
**Deliverables**: JWT generation/validation, user login/logout endpoints
**Dependencies**: None
**Estimated Effort**: 3 days

### Phase 2: Session Management
**Deliverables**: Session storage, refresh tokens, timeout handling
**Dependencies**: Phase 1 complete
**Estimated Effort**: 4 days

### Phase 3: Security Hardening
**Deliverables**: Rate limiting, brute force protection, audit logging
**Dependencies**: Phases 1-2 complete
**Estimated Effort**: 3 days

## Proposed Refinement

I will create 3 sub-issues:
- #415-1: [Phase 1] User Authentication - Core Authentication
- #415-2: [Phase 2] User Authentication - Session Management
- #415-3: [Phase 3] User Authentication - Security Hardening

Each sub-issue will:
- Link to this parent issue (#415)
- Include phase-specific requirements
- Be assigned to implementation duty
- Maintain dependencies between phases

## Next Steps

**To proceed with refinement**, reply with:
- `@copilot proceed with refinement` - Create the sub-issues as proposed

**To skip refinement**, reply with:
- `@copilot skip refinement` - Treat this as single-phase work item

**To adjust phases**, reply with:
- `@copilot adjust phases [instructions]` - Modify the phase breakdown
"""

add_work_item_comment(
    work_item_id="415",
    text=refinement_comment
)

# WAIT for reviewer approval
# Reviewer replies: "@copilot proceed with refinement"
```

### Step 5: Create Sub-Issues (After Approval)

```python
# Update parent description
parent_description = """## Multi-Phase Plan

⚠️ **This is a parent issue for a multi-phase plan.**

Work is split into the following sub-issues:

- [ ] Phase 1: Core Authentication (#416)
- [ ] Phase 2: Session Management (#417)
- [ ] Phase 3: Security Hardening (#418)

## Original Description

## Overview
Implement complete user authentication system with JWT tokens and session management.

[... rest of original description ...]
"""

update_work_item(
    work_item_id="415",
    description=parent_description
)

# Create Phase 1 sub-issue
sub1_id = create_child_work_item(
    parent_id="415",
    type="implementation",
    title="[Phase 1] User Authentication - Core Authentication",
    description="""**Parent Issue**: #415

## Phase 1: Core Authentication

**Deliverables**: JWT generation/validation, user login/logout endpoints
**Dependencies**: None
**Success Criteria**: Users can log in and receive valid JWT tokens

## Detailed Requirements

- [ ] Implement JWT token generation
- [ ] Implement JWT token validation
- [ ] Create user login endpoint
- [ ] Create user logout endpoint
- [ ] Add unit tests for token operations
- [ ] Add integration tests for auth endpoints
- [ ] Update API documentation

---

**Note**: This is Phase 1 of 3. 
See parent issue #415 for overall plan.
""",
    duty="implementation",
    labels=["feature", "security"]
)
# Returns: "416"

# Create Phase 2 sub-issue
sub2_id = create_child_work_item(
    parent_id="415",
    type="implementation",
    title="[Phase 2] User Authentication - Session Management",
    description="""**Parent Issue**: #415

## Phase 2: Session Management

**Deliverables**: Session storage, refresh tokens, timeout handling
**Dependencies**: Phase 1 complete (#416)
**Success Criteria**: Sessions persist correctly and can be refreshed

## Detailed Requirements

- [ ] Implement session storage (Redis)
- [ ] Implement refresh token mechanism
- [ ] Add session timeout handling
- [ ] Add session renewal API
- [ ] Add unit tests for session operations
- [ ] Add integration tests for session lifecycle
- [ ] Update API documentation

---

**Note**: This is Phase 2 of 3. Depends on Phase 1 (#416).
See parent issue #415 for overall plan.
""",
    duty="implementation",
    labels=["feature", "security"]
)
# Returns: "417"

# Create Phase 3 sub-issue
sub3_id = create_child_work_item(
    parent_id="415",
    type="implementation",
    title="[Phase 3] User Authentication - Security Hardening",
    description="""**Parent Issue**: #415

## Phase 3: Security Hardening

**Deliverables**: Rate limiting, brute force protection, audit logging
**Dependencies**: Phases 1-2 complete (#416, #417)
**Success Criteria**: System resistant to common attacks

## Detailed Requirements

- [ ] Implement rate limiting for auth endpoints
- [ ] Add brute force protection
- [ ] Implement audit logging for auth events
- [ ] Add security tests (penetration testing)
- [ ] Add integration tests for security features
- [ ] Update security documentation

---

**Note**: This is Phase 3 of 3. Final phase.
See parent issue #415 for overall plan.
""",
    duty="implementation",
    labels=["feature", "security"]
)
# Returns: "418"
```

### Step 7: Provide Next Steps Guidance

```python
completion_comment = """[Copilot-Duty: Implementation] ✅ **Issue Refinement Complete**

## Refinement Summary

This multi-phase plan has been split into 3 sub-issues:

1. **#416**: [Phase 1] User Authentication - Core Authentication
2. **#417**: [Phase 2] User Authentication - Session Management
3. **#418**: [Phase 3] User Authentication - Security Hardening

## Structure

**Parent Issue**: This issue (#415)
- Tracks overall multi-phase plan
- Remains open until all phases complete
- Stays in implementation duty

**Sub-Issues**: Individual phases
- Each represents a single implementable unit
- Each will have its own PR
- Assigned to implementation duty

## Next Steps for Implementation

**Immediate Action**: Close this PR (work on parent not appropriate)

**To Continue**:
1. Start with Phase 1 (#416)
2. Create new PR for Phase 1
3. Implement Core Authentication
4. After Phase 1 merged, proceed to Phase 2 (#417)
5. After Phase 2 merged, proceed to Phase 3 (#418)

---

**For Implementation**: Start with Phase 1 (#416). 
Do not work on this parent issue directly.

See [Multi-Phase Work Items Procedure](../.team/procedures/multi-phase-work-items.md) for complete guidance.
"""

add_work_item_comment(
    work_item_id="415",
    text=completion_comment
)
```

## Expected Outcome

**✅ PASS** - Issue refinement successful when:

1. **Multi-phase indicators correctly detected**:
   - ✅ Explicit phase sections identified
   - ✅ 3 phases extracted accurately
   - ✅ Dependencies between phases captured

2. **Refinement plan presented to reviewer**:
   - ✅ Comment added with detected phases
   - ✅ Proposed sub-issue structure clear
   - ✅ Waited for approval before creating sub-issues

3. **Sub-issues created correctly**:
   - ✅ 3 child work items created
   - ✅ Each linked to parent (#415)
   - ✅ Phase-specific requirements included
   - ✅ Dependencies documented
   - ✅ All assigned to implementation duty

4. **Parent updated**:
   - ✅ Description updated with sub-issue links
   - ✅ Marked as multi-phase plan
   - ✅ Original description preserved

5. **Guidance provided**:
   - ✅ Next steps clear (start with Phase 1)
   - ✅ Recommended closing PR for parent
   - ✅ Referenced multi-phase procedure

6. **Semantic operations used exclusively**:
   - ✅ No platform-specific GitHub code
   - ✅ All operations use kernel layer

## Actual Outcome

**[To be filled during tabletop execution]**

Result: PASS / FAIL

Notes:
- [Any observations about procedure clarity]
- [Any ambiguities encountered]
- [Suggestions for improvement]
