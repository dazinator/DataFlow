# Scenario 004: Multi-Phase Research Plan

**Duty**: Research  
**Type**: End-to-End Scenario  
**Complexity**: High  
**Created**: 2025-11-12

---

## Purpose

Test research duty's ability to handle multi-phase research where work is split into multiple sub-work-items with parent coordination.

---

## Context

A large research effort requiring multiple phases has been split into a parent tracking issue and sub-work-items for each phase.

---

## Starting State

**Parent Work Item #950**:
- **Title**: "[Research Plan] Complete performance optimization research"
- **Description**: 
  ```
  Multi-phase research plan for performance optimization.
  
  ## Sub-Work-Items
  - #951: Phase 1 - Baseline performance benchmarks
  - #952: Phase 2 - Identify bottlenecks
  - #953: Phase 3 - Evaluate optimization approaches
  
  ## Overall Objective
  Research and validate performance optimization strategies.
  ```
- **Duty**: `research`
- **Status**: `open`
- **Type**: `parent`

**Sub-Work-Item #952** (Current):
- **Title**: "Phase 2: Identify bottlenecks"
- **Description**: 
  ```
  Identify performance bottlenecks in current implementation.
  
  Prerequisites:
  - #951 (Phase 1) must be complete
  
  Deliverables:
  - Profiling data
  - Bottleneck analysis
  - Prioritized list of optimization opportunities
  ```
- **Duty**: `research`
- **Status**: `open`
- **Parent**: `#950`

**Note**: Phase 1 (#951) is already completed.

---

## Procedure to Follow

[Research Duty](../../../.team/duties/RESEARCH_DUTY.md) + [Multi-Phase Work Items Procedure](../../../.team/procedures/multi-phase-work-items.md)

---

## Expected Steps

### Step 1: Check Multi-Phase Status

```python
# Get work item
details = get_work_item_details(work_item_id="952")

# Check if multi-phase
is_multi = is_multi_phase(work_item_id="952")  # Returns True

# Get parent
parent_id = get_parent_work_item(work_item_id="952")  # Returns "950"
parent = get_work_item_details(parent_id)
```

**Result**: This is Phase 2 of a 3-phase research plan. Phase 1 is complete.

### Step 2: Review Parent and Completed Phases

```python
# Review parent to understand overall plan
# Read Phase 1 results from #951 for context
phase1_details = get_work_item_details(work_item_id="951")
```

**Context from Phase 1**:
- Baseline benchmarks established
- Current throughput: 500 items/second
- Target throughput: 2000 items/second

### Step 3: Conduct Phase 2 Research

Create `/research/performance-optimization/phase2-bottlenecks/`:

**Profiling and Analysis**:
- Run performance profiling
- Identify top 3 bottlenecks:
  1. Database queries (40% of time)
  2. JSON serialization (30% of time)
  3. Memory allocations (20% of time)

**Document in README**:
```markdown
# Phase 2: Bottleneck Analysis

## Bottlenecks Identified

### 1. Database Queries (40% of time)
- N+1 query pattern in data loading
- Opportunity: Batch queries

### 2. JSON Serialization (30% of time)
- Using reflection-based serializer
- Opportunity: Source generators

### 3. Memory Allocations (20% of time)
- Excessive allocations in hot path
- Opportunity: Object pooling

## Prioritized Optimization Opportunities
1. Batch database queries (highest impact)
2. Use JSON source generators
3. Implement object pooling

## Recommendation for Phase 3
Focus Phase 3 research on evaluating approaches for top 2 bottlenecks.
```

### Step 4: Update Parent Work Item

```python
# Update parent description with Phase 2 progress
parent_description = get_work_item_details("950")['description']

updated_description = parent_description + """

## Phase 2 Status: ✅ Complete

**Bottlenecks Identified**:
1. Database queries (40% of time)
2. JSON serialization (30% of time)
3. Memory allocations (20% of time)

**Documentation**: `/research/performance-optimization/phase2-bottlenecks/`
"""

update_work_item(
    work_item_id="950",
    description=updated_description
)
```

### Step 5: Complete Phase 2

```python
# Add completion comment
add_work_item_comment(
    work_item_id="952",
    body="[Copilot-Duty: Research] ✅ Phase 2 Complete\n\n"
         "**Deliverables**:\n"
         "- Profiling data collected\n"
         "- Top 3 bottlenecks identified\n"
         "- Prioritized optimization opportunities\n\n"
         "**Documentation**: `/research/performance-optimization/phase2-bottlenecks/`\n\n"
         "**Parent Updated**: #950 updated with Phase 2 results\n\n"
         "**Next Phase**: Phase 3 (#953) can now proceed to evaluate optimization approaches"
)

# Close Phase 2 work item
update_work_item(
    work_item_id="952",
    status="completed"
)
```

### Step 6: Add Cross-Reference to Phase 3

```python
# Add comment to Phase 3 with context
add_work_item_comment(
    work_item_id="953",
    body="[Copilot-Duty: Research] 📋 Phase 2 Complete - Context for Phase 3\n\n"
         "Phase 2 bottleneck analysis is complete. Top priorities:\n"
         "1. Database query batching (40% improvement potential)\n"
         "2. JSON source generators (30% improvement potential)\n\n"
         "**Recommendation**: Focus Phase 3 on evaluating approaches for these two bottlenecks.\n\n"
         "**Documentation**: `/research/performance-optimization/phase2-bottlenecks/`"
)
```

### Step 7: Submit Feedback

Standard self-improvement feedback submission.

---

## Expected Outcome

**Work Item #952** (Phase 2):
- **Status**: `completed`
- **Comments**: Completion comment with deliverables

**Parent Work Item #950**:
- **Description**: Updated with Phase 2 results
- **Status**: Still `open` (Phase 3 pending)

**Work Item #953** (Phase 3):
- **Comments**: Context comment added for next phase
- **Status**: Still `open` (ready to start)

**Deliverables**:
- Phase 2 research documentation
- Bottleneck analysis
- Parent tracking updated
- Phase 3 prepared with context

---

## Success Criteria

- [ ] Multi-phase status detected correctly
- [ ] Parent work item reviewed for context
- [ ] Phase 1 results reviewed before starting Phase 2
- [ ] Phase 2 research conducted
- [ ] Parent work item updated with Phase 2 results
- [ ] Phase 2 work item completed
- [ ] Context provided to Phase 3
- [ ] Multi-phase procedure followed correctly
- [ ] No platform-specific code (zero kernel leaks)

---

## Test Result

**Status**: [PENDING]

---

## Semantic Operations Used

- `get_work_item_details(work_item_id)`
- `is_multi_phase(work_item_id)`
- `get_parent_work_item(work_item_id)`
- `update_work_item(work_item_id, fields)`
- `add_work_item_comment(work_item_id, text)`
- `query_feedback_tracker()`
