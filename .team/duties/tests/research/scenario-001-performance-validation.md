# Scenario 001: Performance Validation Research

**Duty**: Research  
**Type**: End-to-End Scenario  
**Complexity**: Medium  
**Created**: 2025-11-12

---

## Purpose

Test research duty's ability to conduct performance validation research, create benchmarks, document findings, and create implementation handover.

---

## Context

A work item requires validation that a proposed caching approach can meet performance requirements before implementation.

---

## Starting State

**Work Item #789**:
- **Title**: "Research: Validate caching strategy for API response times"
- **Description**: 
  ```
  Investigate if adding a caching layer can reduce API response times by at least 50%.
  
  Current State:
  - Average response time: 200ms
  - No caching implemented
  
  Research Questions:
  - Can caching reduce response time to <100ms?
  - Which caching approach (Redis vs in-memory) performs better?
  - What is the memory overhead?
  
  Success Criteria:
  - Benchmark showing response time improvements
  - Memory usage analysis
  - Recommendation for implementation
  ```
- **Duty**: `research`
- **Type**: `research`
- **Status**: `open`

---

## Procedure to Follow

[Research Duty](../../../.team/duties/RESEARCH_DUTY.md)

---

## Expected Steps

### Step 1: Get Work Item and Check Multi-Phase

```python
# Query research queue
research_items = query_work_items_by_duty(duty="research")

# Get work item details
details = get_work_item_details(work_item_id="789")

# Check if multi-phase
is_multi = is_multi_phase(work_item_id="789")
```

**Result**: Work item found, not multi-phase.

### Step 2: Create Research Plan

Create `/research/caching-strategy/research-plan.md`:

```markdown
# Research Plan: Caching Strategy

## Research Objective
Validate if caching can reduce API response times by 50% (200ms → <100ms)

## Research Questions
- Can caching achieve <100ms response time?
- Redis vs in-memory: which performs better?
- What is memory overhead?

## Success Metrics
- Quantitative: Response time reduction to <100ms
- Baseline: Current 200ms average
- Validation: Benchmark with 1000 requests

## Expected Outcomes
- Research documentation with findings
- Implementation-ready work item
- Benchmark data
```

### Step 3: Conduct Research

**Create prototype** (will be reverted later):
- Build working prototype with Redis caching
- Build working prototype with in-memory caching
- Create benchmark tests

**Run benchmarks** and save results to `/research/caching-strategy/benchmarks/redis-vs-memory.md`:

```markdown
# Benchmark: Redis vs In-Memory Caching

## Results

| Approach | Avg Response Time | Memory Usage | Notes |
|----------|------------------|--------------|--------|
| No Cache | 200ms | Baseline | Current state |
| Redis | 75ms | +50MB | 62% improvement |
| In-Memory | 85ms | +30MB | 57% improvement |

## Recommendation
Redis provides best performance with acceptable memory overhead.
```

### Step 4: Document Findings

Create `/research/caching-strategy/README.md`:

```markdown
# Research: Caching Strategy

## Research Objective
Validate caching approach for API performance

## Approaches Explored

### Approach 1: Redis Caching
- ✅ Best performance (75ms, 62% improvement)
- ✅ Scales across instances
- ❌ Higher memory overhead (+50MB)

### Approach 2: In-Memory Caching
- ✅ Good performance (85ms, 57% improvement)
- ✅ Lower memory overhead (+30MB)
- ❌ Doesn't scale across instances

## Recommended Approach
Redis caching provides optimal balance of performance and scalability.

## Success Metrics Results
✅ Achieved <100ms target (75ms with Redis)
✅ 62% improvement over baseline
✅ Memory overhead acceptable for performance gain
```

### Step 5: Create Implementation Handover

```python
# Create implementation work item
impl_id = create_work_item(
    type="implementation",
    title="Implement Redis caching for API responses",
    description="""
# Implementation: Redis Caching

**Research Reference**: #789
**Research Documentation**: `/research/caching-strategy/`

## Objective
Implement Redis-based caching layer to reduce API response times.

## Approach (Validated by Research)
Redis caching with 75ms average response time (62% improvement).

## Success Criteria
- Average response time <100ms
- Memory overhead <60MB
- Cache invalidation on data updates

## Test Scenarios
- Cache hit scenario (expect <80ms)
- Cache miss scenario (expect ~200ms)
- Cache invalidation scenario

## Performance Requirements
- Target: <100ms average response time
- Validated: 75ms in benchmarks

## Design References
- Research: `/research/caching-strategy/README.md`
- Benchmarks: `/research/caching-strategy/benchmarks/redis-vs-memory.md`
""",
    duty="implementation"
)

# Add handover comment to research work item
add_work_item_comment(
    work_item_id="789",
    body="[Copilot-Duty: Research] 🔄 Research → Implementation\n\n"
         f"Research complete. Created implementation work item #{impl_id}\n\n"
         "**Research Findings**: Redis caching achieves 75ms response time (62% improvement)\n\n"
         "**Documentation**: See `/research/caching-strategy/` for complete findings and benchmarks\n\n"
         "**Next Steps**: Implementation duty will complete the work based on research specifications."
)
```

### Step 6: Save Prototype

```bash
# Create prototype directory
mkdir -p /research/caching-strategy/handover/prototype

# Save prototype code
# (Copy prototype files to handover/prototype/)
```

### Step 7: Wait for Reviewer Approval

**Reviewer approves research findings**

### Step 8: Revert Exploratory Code

```bash
# Revert exploratory code from /poc/ and /src/
git checkout HEAD -- poc/ src/

# Keep research artifacts - DO NOT revert:
# - /research/caching-strategy/ (research folder)
```

### Step 9: Submit Self-Improvement Feedback

```python
# Find feedback tracker
tracker = query_feedback_tracker()

# Add feedback
add_work_item_comment(
    tracker.id,
    body="""## Workflow Feedback Entry

**Date**: 2025-11-12
**Issue/PR**: #789
**Duty**: Research

### What Worked Well
- Benchmark template was clear and effective
- Research folder structure made organization easy

### What Didn't Work Well
- Unclear when to create formal documentation vs research notes

### Suggested Improvement
Add decision criteria for when to create formal ADRs during research.
"""
)
```

### Step 10: Complete Work

```python
# Update status
update_work_item(
    work_item_id="789",
    status="completed"
)

# Add completion comment
add_work_item_comment(
    work_item_id="789",
    body="[Copilot-Duty: Research] ✅ Research Complete\n\n"
         "**Deliverables**:\n"
         "- Research documentation: `/research/caching-strategy/`\n"
         f"- Implementation work item: #{impl_id}\n"
         "- Benchmarks: `/research/caching-strategy/benchmarks/`\n"
         "- Prototype code: `/research/caching-strategy/handover/prototype/`\n\n"
         "**Status**: Ready for implementation"
)
```

---

## Expected Outcome

**Work Item #789** updated:
- **Status**: `completed`
- **Comments**: Handover and completion comments added
- **Handover Created**: New implementation work item created

**Deliverables**:
- `/research/caching-strategy/` folder with research artifacts
- Implementation work item with specifications
- Benchmark data
- Prototype code saved (exploratory code reverted)

---

## Success Criteria

- [ ] Work item queried using semantic operations
- [ ] Research folder structure created correctly
- [ ] Benchmarks conducted and documented
- [ ] Implementation handover work item created with specifications
- [ ] Handover comment added
- [ ] Prototype saved before code reversion
- [ ] Exploratory code reverted (after approval)
- [ ] Self-improvement feedback submitted
- [ ] No platform-specific code used (zero kernel leaks)

---

## Test Result

**Status**: [PENDING]

**Agent Observations**: [To be filled when executing tabletop test]

**Issues Found**: [Any unclear steps, missing info, etc.]

---

## Semantic Operations Used

- `query_work_items_by_duty(duty)` - Find research work items
- `get_work_item_details(work_item_id)` - Retrieve work item
- `is_multi_phase(work_item_id)` - Check multi-phase status
- `create_work_item(...)` - Create implementation handover
- `add_work_item_comment(work_item_id, text)` - Add comments
- `update_work_item(work_item_id, fields)` - Update status
- `query_feedback_tracker()` - Find feedback tracker for self-improvement
