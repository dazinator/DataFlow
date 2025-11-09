# Gap Analysis: Workflow Topology System Integration

**Date**: 2025-11-09
**Purpose**: Identify gaps between proposed workflow topology system and current implementation

---

## Summary

The implementation team has prepared example workflow files with workflow topology integration, but these have NOT been deployed to actual workflow files (except for TRIAGE_WORKFLOW.md which is already deployed and identical).

**Key Finding**: The Triage workflow is already deployed, but the other 5 workflows are NOT updated yet.

---

## Files Comparison Status

| Workflow | Actual File | Example File | Status |
|----------|-------------|--------------|---------|
| Triage | `.team/workflows/TRIAGE_WORKFLOW.md` | `research/.../examples/TRIAGE_WORKFLOW.md` | ✅ **IDENTICAL** - Already deployed |
| Research | `.team/workflows/RESEARCH_WORKFLOW.md` | `research/.../examples/RESEARCH_WORKFLOW.md` | ❌ **DIFFERENT** - Not deployed |
| Implementation | `.team/workflows/IMPLEMENTATION_WORKFLOW.md` | `research/.../examples/IMPLEMENTATION_WORKFLOW.md` | ❌ **DIFFERENT** - Not deployed |
| Tech Debt | `.team/workflows/TECH_DEBT_WORKFLOW.md` | `research/.../examples/TECH_DEBT_WORKFLOW.md` | ❌ **DIFFERENT** - Not deployed |
| Product Prioritization | `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md` | `research/.../examples/PRODUCT_PRIORITIZATION_WORKFLOW.md` | ❌ **DIFFERENT** - Not deployed |
| Process Modeling | `.team/workflows/PROCESS_MODELING_WORKFLOW.md` | `research/.../examples/PROCESS_MODELING_WORKFLOW.md` | ❌ **DIFFERENT** - Not deployed |

---

## Pattern of Changes Required

Based on diff analysis of example files, each workflow needs:

### 1. Workflow Queue Section (NEW)

**Location**: Near top of workflow, after "When to Use" section
**Content**:
- `gh issue list` command with workflow label
- Reference to query script
- Entry points (where issues come from)

**Example**:
```markdown
## Workflow Queue

**Query issues designated to this workflow:**

```bash
gh issue list \
  --label "workflow:research" \
  --state open \
  --json number,title,url
```

**Or use the query script:**
```bash
./research/workflow-topology-design/handover/prototype/query-workflow-queue.sh research
```

**Entry Points:**
- From Triage workflow (needs validation)
- From Tech Debt workflow (needs research)
- From Implementation workflow (uncovered unknowns)
```

### 2. Handover to Next Workflow Section (NEW)

**Location**: Near end of workflow, before completion
**Content**:
- Handover patterns for each possible next workflow
- Both manual `gh` commands and helper script examples
- Comments template with handover reasoning

**Example**:
```markdown
## Handover to Next Workflow

### Handover to Implementation

**When**: Research validates approach and creates implementation-ready specifications

```bash
gh issue edit $ISSUE \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE --body "🔬 **Handover: Research → Implementation**

Research validated approach. Ready for implementation.

**Research Deliverables**:
- Findings: `/research/[topic]/README.md`
- Design: `/research/[topic]/design/[component].md`
..."
```

**Or use the handover script**:
```bash
./research/workflow-topology-design/handover/prototype/handover-issue.sh \
  $ISSUE research implementation "Research validated approach"
```
```

---

## Scripts Location Gap

**Current Location**: `research/workflow-topology-design/handover/prototype/`

**Problem**: Scripts are in a temporary handover folder, not a permanent location accessible to copilot agents

**Proposed Solutions**:
1. **Option A**: Move to `.team/scripts/workflow/` (new directory)
   - Pro: Grouped with team workflows
   - Pro: Clear purpose
   - Con: New directory structure
   
2. **Option B**: Move to `research/workflow-modeling/tools/` (existing directory)
   - Pro: Already exists and has other workflow tools
   - Pro: Consistent with existing structure
   - Con: Might be confusing (research folder vs team-wide tools)

3. **Option C**: Move to `.github/scripts/` (new directory)
   - Pro: Close to GitHub Actions
   - Pro: Clear that these interact with GitHub
   - Con: New directory structure

**Recommendation**: **Option A** - Create `.team/scripts/workflow/` because:
- Workflows are in `.team/workflows/`
- Scripts are workflow-related tools
- Clear separation from research artifacts
- Easily accessible by all copilot agents

---

## Copilot Instructions Gap

**Current State**: No mention of workflow topology system

**Required Updates**:

1. **Quick Navigation Section**: Add Triage Workflow
   ```markdown
   1. **Triage Task** (assess and route new issues)
      → See `.team/workflows/TRIAGE_WORKFLOW.md`
   ```

2. **New Section**: Workflow Topology System
   - Explain label-based workflow routing
   - List all workflow labels
   - Reference helper scripts
   - Explain handover patterns

3. **Research vs Implementation Table**: Update to include workflow labels
   | Indicator | Research | Implementation |
   |-----------|----------|----------------|
   | Issue label | `research` OR `workflow:research` | `implementation` OR `workflow:implementation` |

4. **Repository Structure**: Add scripts location
   ```markdown
   .team/
   ├── workflows/          # Workflow documentation
   └── scripts/
       └── workflow/      # Workflow helper scripts
   ```

---

## GitHub Actions Gap

**File**: `.github/workflows/auto-label-triage.yml`

**Status**: NOT deployed (prototype available)

**Purpose**: Automatically label new issues with `workflow:triage`

**Deployment Decision**: Should be deployed AFTER:
- Workflow documentation is updated
- Scripts are in proper location
- Copilot instructions are updated
- Tabletop testing is complete

**Note**: This makes the system "active" for all new issues

---

## Label Creation Gap

**Required Labels** (need manual creation):
```
workflow:triage
workflow:research
workflow:implementation
workflow:tech-debt
workflow:product-backlog
workflow:process-modeling
```

**Status**: Not created yet (need admin permissions)

**Action Required**: Document in testing notes that reviewer will need to create labels

---

## Common Patterns Factoring Gap

**Observation**: All workflows have similar handover patterns

**Opportunity**: Create shared documentation for:
- Handover comment templates
- Label change patterns
- Common handover scenarios

**Recommendation**: Add to Process Modeling Workflow a section on "Using Workflow Topology" that other workflows can reference, reducing duplication.

**Alternative**: Create a separate document `.team/workflows/WORKFLOW_TOPOLOGY_GUIDE.md` that all workflows reference.

**Decision**: Use shared guide approach to avoid duplication across 6 workflows.

---

## Testing Requirements

Based on gap analysis, need test scenarios for:

1. **Script Migration**:
   - Scenario: Scripts moved to new location
   - Test: All script paths in documentation work
   - Test: Scripts are executable
   - Test: Scripts have correct permissions

2. **Workflow Integration**:
   - Scenario: Research to Implementation handover
   - Test: Can query research queue
   - Test: Can handover to implementation
   - Test: Comment is posted correctly
   - Test: Labels are changed correctly

3. **Copilot Instructions**:
   - Scenario: New issue arrives
   - Test: Copilot can find triage workflow
   - Test: Copilot understands workflow topology
   - Test: Copilot knows how to query queues

4. **End-to-End Flow**:
   - Scenario: New issue → Triage → Research → Implementation → Close
   - Test: Each transition works
   - Test: Audit trail is clear
   - Test: No gaps in workflow

---

## Gaps Summary

| Gap Area | Severity | Action Required |
|----------|----------|-----------------|
| 5 workflows not updated | **HIGH** | Apply example changes to actual workflows |
| Scripts in wrong location | **HIGH** | Move to `.team/scripts/workflow/` |
| Copilot instructions not updated | **HIGH** | Add workflow topology section |
| Auto-label workflow not deployed | **MEDIUM** | Deploy after testing complete |
| Labels not created | **MEDIUM** | Coordinate with reviewer |
| Common patterns not factored | **LOW** | Create shared guide (optional but recommended) |
| Missing test scenarios | **HIGH** | Create before deploying changes |

---

## Recommended Deployment Order

1. ✅ **Create Shared Topology Guide** (factor out common patterns)
2. ✅ **Move Scripts** to `.team/scripts/workflow/`
3. ✅ **Update All 5 Workflows** with topology integration
4. ✅ **Update Copilot Instructions** with topology system
5. ✅ **Create Test Scenarios** for all transitions
6. ✅ **Execute Tabletop Tests** and refine
7. ⏸️ **Request Reviewer to Create Labels** (manual step)
8. ⏸️ **Deploy Auto-Label Workflow** (makes system active)
9. ⏸️ **Run Migration Script** (label existing issues)

**Note**: Steps 7-9 require reviewer action and should be done AFTER all testing is complete.

---

## Next Steps for Process Modeling

1. Review this gap analysis
2. Decide on script location (recommend Option A)
3. Decide on shared guide vs duplication (recommend shared guide)
4. Create test scenarios
5. Begin implementing changes following the recommended deployment order
