# Scenario: Ambiguous Phase Boundaries (Edge Case)

## Context
Implementation duty receives a work item that mentions "phases" but the boundaries are not clearly defined. The refinement check should identify this ambiguity and ask for reviewer clarification.

## Starting Point
- **Current work item**: #430 (Implement Pipeline Monitoring Dashboard)
- **Duty**: implementation
- **Parent work item**: None
- **Sub-issues**: None
- **Status**: Just handed over from research
- **Agent**: Implementation duty

## Work Item Content

**Title**: Implement Pipeline Monitoring Dashboard

**Description**:
```markdown
## Overview

Build a monitoring dashboard for pipeline execution with real-time metrics and historical data.

## Features Needed

We'll need to implement this in phases to manage complexity:

**Core Metrics Collection**
- Capture block execution times
- Track throughput rates
- Monitor memory usage
- Record error counts

**Dashboard UI**
- Real-time metric display
- Historical charts
- Alert configuration
- Export functionality

**Data Storage**
- Time-series database integration
- Metric aggregation
- Data retention policies
- Query optimization

## Implementation Notes

This is a larger feature that should be phased, but exact phase boundaries depend on technical design. Each component could be a phase, or we could group them differently (e.g., backend vs frontend).

## Success Criteria
- All metrics captured
- Dashboard functional
- Performance acceptable
- Data persisted correctly
```

## Steps to Follow (from Issue Refinement Procedure)

### Step 1: Analyze Work Item for Multi-Phase Indicators

```python
details = get_work_item_details(work_item_id="430")
description = details['description']

# Check for multi-phase indicators
# ✅ Found: "implement this in phases" phrase
# ✅ Found: Multiple major features (3 distinct areas)
# ✅ Found: "larger feature" mentioned
# ⚠️ Ambiguous: No clear "Phase 1", "Phase 2" sections
# ⚠️ Ambiguous: Multiple possible phase breakdowns suggested
# ⚠️ Unclear: "exact phase boundaries depend on technical design"

# Indicator count: 3 (multi-phase likely, but unclear boundaries)

# Decision: Multi-phase plan likely but boundaries AMBIGUOUS - proceed with caution
```

### Step 2: Check Existing Sub-Issues

```python
children = list_child_work_items(work_item_id="430")
# Returns: [] (empty - no sub-issues)

print("No sub-issues found - needs refinement, but boundaries unclear")
```

### Step 3: Extract Phase Structure (Multiple Interpretations)

```python
# Possible interpretation 1: By component
possible_phases_v1 = [
    {"number": 1, "name": "Core Metrics Collection"},
    {"number": 2, "name": "Dashboard UI"},
    {"number": 3, "name": "Data Storage"}
]

# Possible interpretation 2: By layer (as suggested in description)
possible_phases_v2 = [
    {"number": 1, "name": "Backend (Metrics + Storage)"},
    {"number": 2, "name": "Frontend (Dashboard UI)"}
]

# Possible interpretation 3: By functionality
possible_phases_v3 = [
    {"number": 1, "name": "Basic Metrics (capture + display)"},
    {"number": 2, "name": "Historical Data (storage + charts)"},
    {"number": 3, "name": "Advanced Features (alerts + export)"}
]

# ⚠️ Multiple valid interpretations exist
# ⚠️ No clear guidance in description about which to use
# ⚠️ Technical design needed to determine optimal breakdown
```

### Step 4: Present Ambiguity to Reviewer

Instead of proposing a specific refinement, flag the ambiguity:

```python
ambiguity_comment = """[Copilot-Duty: Implementation] ⚠️ **Multi-Phase Plan with Ambiguous Boundaries**

## Analysis

This work item appears to represent a multi-phase plan, but the phase boundaries are not clearly defined.

**Indicators Found**:
- Description mentions "implement this in phases"
- Multiple major features identified (Metrics, Dashboard, Storage)
- Described as "larger feature"

**Ambiguity Detected**:
- No explicit "Phase 1", "Phase 2" sections in description
- Multiple valid phase breakdowns possible
- Description notes "exact phase boundaries depend on technical design"

## Possible Phase Breakdowns

### Option 1: By Component (3 phases)
1. **Core Metrics Collection** - Backend metrics capture
2. **Dashboard UI** - Frontend visualization
3. **Data Storage** - Persistence and querying

**Pros**: Clear separation of concerns
**Cons**: Each phase touches both frontend and backend

### Option 2: By Layer (2 phases)
1. **Backend** - Metrics collection + data storage
2. **Frontend** - Dashboard UI + visualization

**Pros**: Fewer phases, full-stack per phase
**Cons**: Larger scope per phase

### Option 3: By Functionality (3 phases)
1. **Basic Metrics** - Capture + real-time display
2. **Historical Data** - Storage + historical charts
3. **Advanced Features** - Alerts + export

**Pros**: Incremental value delivery
**Cons**: More complex dependencies

## Recommendation Needed

**Please clarify the preferred phase breakdown**:

1. Which option best aligns with project goals?
2. Are there other phase boundaries to consider?
3. Should I defer this to research for technical design?

**Reply with**:
- `@copilot use option 1` - Component-based breakdown
- `@copilot use option 2` - Layer-based breakdown
- `@copilot use option 3` - Functionality-based breakdown
- `@copilot hand to research` - Need technical design first
- `@copilot custom phases: [your breakdown]` - Specify custom structure

---

⏸️ **Pausing implementation until phase boundaries are clarified.**
"""

add_work_item_comment(
    work_item_id="430",
    text=ambiguity_comment
)

# Return status indicating ambiguity
return {
    "refined": False,
    "multi_phase": True,
    "ambiguous": True,
    "needs_clarification": True,
    "possible_interpretations": 3,
    "recommended_action": "await_reviewer_clarification"
}
```

### Step 5A: Reviewer Selects Option (Option 1)

```python
# Reviewer replies: "@copilot use option 1"

# Proceed with component-based breakdown
phases = [
    {
        "number": 1,
        "name": "Core Metrics Collection",
        "deliverables": "Backend metrics capture and processing",
        "type": "implementation",
        "duty": "implementation"
    },
    {
        "number": 2,
        "name": "Dashboard UI",
        "deliverables": "Frontend visualization and real-time display",
        "dependencies": "Phase 1 complete (need metrics API)",
        "type": "implementation",
        "duty": "implementation"
    },
    {
        "number": 3,
        "name": "Data Storage",
        "deliverables": "Time-series DB integration and historical data",
        "dependencies": "Phase 1 complete (need metrics schema)",
        "type": "implementation",
        "duty": "implementation"
    }
]

# Now proceed with Steps 5-7 of refinement procedure
# (same as scenario-001)
```

### Step 5B: Reviewer Requests Research

```python
# Alternative: Reviewer replies "@copilot hand to research"

# Hand over to research for technical design
assign_work_item_to_duty(
    work_item_id="430",
    duty="research"
)

add_work_item_comment(
    work_item_id="430",
    text="""[Copilot-Duty: Implementation] 🔄 **Implementation → Research**

Handing back to research for technical design clarification.

**Reason**: Phase boundaries unclear - need technical design to determine optimal breakdown.

**Research Should Clarify**:
- Optimal phase breakdown (component vs layer vs functionality)
- Dependencies between phases
- Technical constraints affecting phase boundaries

Once research completes, implementation can proceed with refined structure.
"""
)
```

## Expected Outcome

**✅ PASS** - Ambiguity handling successful when:

1. **Ambiguity detected**:
   - ✅ Identified multi-phase indicators
   - ✅ Recognized unclear boundaries
   - ✅ Did not make assumptions

2. **Multiple interpretations presented**:
   - ✅ Listed 2-3 valid phase breakdowns
   - ✅ Explained pros/cons of each
   - ✅ Asked for clarification

3. **No premature refinement**:
   - ✅ Did not create sub-issues without approval
   - ✅ Did not choose arbitrary breakdown
   - ✅ Paused implementation appropriately

4. **Clear options provided**:
   - ✅ Specific response formats given
   - ✅ Multiple resolution paths offered
   - ✅ Handover to research suggested as option

5. **Proper handling of clarification**:
   - ✅ After reviewer selects option, proceed with refinement
   - ✅ After reviewer requests research, proper handover performed
   - ✅ No work lost, context preserved

6. **Return value appropriate**:
   - ✅ `ambiguous: True` flag set
   - ✅ `needs_clarification: True`
   - ✅ `recommended_action` clear

## Actual Outcome

**[To be filled during tabletop execution]**

Result: PASS / FAIL

Notes:
- [Quality of ambiguity detection]
- [Usefulness of presented options]
- [Clarity of guidance for next steps]
- [Any improvements needed]
