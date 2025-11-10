# Integration Patterns for Workflow Topology System

## Overview

This document defines how each workflow integrates with the centralized workflow topology system using labels for state management.

**System**: Hybrid approach, Phase 1 (label-based MVP)
**State Storage**: GitHub Labels
**Handover**: Label change + comment

---

## Label Schema

### Workflow Designation Labels

| Label | Purpose | Entry Point |
|-------|---------|-------------|
| `workflow:triage` | Default for new issues, awaiting assessment | Auto-applied on issue creation |
| `workflow:research` | Designated to Research Workflow | From triage or tech debt |
| `workflow:implementation` | Designated to Implementation Workflow | From research, product, or triage |
| `workflow:tech-debt` | Designated to Tech Debt Workflow | From triage or implementation |
| `workflow:product-backlog` | Designated to Product Prioritization | From triage, research, or implementation |
| `workflow:process-modeling` | Designated to Process Modeling Workflow | From triage or any workflow |

### Optional Enhancement Labels (Phase 1.5)

| Label | Purpose | Used By |
|-------|---------|---------|
| `priority:high` | High priority item | Product Prioritization |
| `priority:medium` | Medium priority item | Product Prioritization |
| `priority:low` | Low priority item | Product Prioritization |
| `status:blocked` | Issue blocked on external dependency | Any workflow |

---

## Core Integration Pattern

All workflows follow the same basic pattern:

### 1. Query Workflow Queue

```bash
# Query issues designated to this workflow
gh issue list \
  --label "workflow:WORKFLOW_NAME" \
  --state open \
  --json number,title,url,labels \
  --jq '.[] | {number, title, url}'
```

**Example for Research Workflow**:
```bash
gh issue list \
  --label "workflow:research" \
  --state open \
  --json number,title,url
```

### 2. Process Issue

- Read issue content and context
- Perform workflow-specific work
- Create deliverables (code, docs, etc.)

### 3. Handover to Next Workflow

```bash
# Remove current workflow label, add next workflow label
gh issue edit $ISSUE_NUMBER \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

# Post comment explaining handover
gh issue comment $ISSUE_NUMBER \
  --body "🔄 **Handover: Research → Implementation**

Research is complete. See \`/research/[topic]/\` for findings and design.

**Key Deliverables**:
- Research findings: \`/research/[topic]/README.md\`
- Design docs: \`/research/[topic]/design/\`
- Implementation issue: \`/research/[topic]/handover/github-issue-implement-[feature].md\`

Ready for implementation."
```

### 4. Or Close Issue

```bash
# If work is complete and no handover needed
gh issue close $ISSUE_NUMBER --comment "✅ Work complete. [Reason]"
```

---

## Workflow-Specific Integration Patterns

### Triage Workflow

**Purpose**: Assess new issues and designate to appropriate workflow

**Queue Query**:
```bash
gh issue list --label "workflow:triage" --state open
```

**Assessment Process**:
1. Read issue content
2. Determine issue type (bug, feature request, research question, tech debt, etc.)
3. Assess complexity and scope
4. Designate to appropriate workflow

**Handover Pattern**:
```bash
# Example: Triage → Research
gh issue edit $ISSUE --remove-label "workflow:triage" --add-label "workflow:research"
gh issue comment $ISSUE --body "🔍 **Triage → Research**

This issue requires research to validate feasibility. Designated to Research Workflow.

**Research Questions**:
- [Question 1]
- [Question 2]

See \`.team/prompts/RESEARCH_WORKFLOW.md\` for process."
```

**Possible Designations**:
- → `workflow:research` (needs validation)
- → `workflow:implementation` (clear, ready to implement)
- → `workflow:tech-debt` (technical debt item)
- → `workflow:product-backlog` (needs prioritization)
- → `workflow:process-modeling` (workflow/process improvement)
- → Close (duplicate, invalid, won't fix)

---

### Research Workflow

**Purpose**: Validate approaches, prototype solutions, document findings

**Queue Query**:
```bash
gh issue list --label "workflow:research" --state open
```

**Process**:
1. Create research folder `/research/[topic]/`
2. Prototype and validate approaches
3. Document findings
4. Create implementation-ready issue

**Handover Pattern**:
```bash
# Research → Implementation
gh issue edit $ISSUE --remove-label "workflow:research" --add-label "workflow:implementation"
gh issue comment $ISSUE --body "🔬 **Handover: Research → Implementation**

Research validated approach. Ready for implementation.

**Research Deliverables**:
- Findings: \`/research/[topic]/README.md\`
- Design: \`/research/[topic]/design/[component].md\`
- Implementation issue: \`/research/[topic]/handover/github-issue-[feature].md\`

**Next Steps**: Implement based on research specifications."
```

**Alternative Handovers**:
- → `workflow:product-backlog` (needs prioritization before implementation)
- → `workflow:triage` (approach not feasible, needs reassessment)
- → Close (research shows not viable)

---

### Implementation Workflow

**Purpose**: Implement validated designs and features

**Queue Query**:
```bash
gh issue list --label "workflow:implementation" --state open
```

**Additional Filters** (optional):
```bash
# High priority implementations
gh issue list \
  --label "workflow:implementation" \
  --label "priority:high" \
  --state open
```

**Process**:
1. Read implementation issue (from research or direct)
2. Implement code changes
3. Write tests
4. Update documentation
5. Create PR

**Handover Pattern**:
```bash
# Implementation complete
gh issue close $ISSUE --comment "✅ **Implementation Complete**

Changes merged in PR #$PR_NUMBER.

**Deliverables**:
- Code: [files changed]
- Tests: [test files]
- Docs: [documentation updated]"
```

**Alternative Handovers**:
- → `workflow:tech-debt` (implementation revealed tech debt)
- → `workflow:product-backlog` (needs refinement)
- → `workflow:triage` (requirements unclear)

---

### Tech Debt Workflow

**Purpose**: Analyze and address technical debt

**Queue Query**:
```bash
gh issue list --label "workflow:tech-debt" --state open
```

**Process**:
1. Analyze codebase for debt
2. Document findings
3. Create prioritized backlog items
4. Create implementation issues

**Handover Pattern**:
```bash
# Tech Debt → Implementation
gh issue edit $ISSUE --remove-label "workflow:tech-debt" --add-label "workflow:implementation"
gh issue comment $ISSUE --body "🔧 **Handover: Tech Debt → Implementation**

Analysis complete. Tech debt items documented.

**Findings**: See \`/research/tech-debt-[date]/README.md\`

**Backlog Items Created**:
- \`/product/backlog/[item-1].md\`
- \`/product/backlog/[item-2].md\`

Ready for prioritization and implementation."
```

**Alternative Handovers**:
- → Close (analysis complete, backlog items created)
- → `workflow:product-backlog` (items need prioritization)

---

### Product Prioritization Workflow

**Purpose**: Prioritize backlog items for implementation

**Queue Query**:
```bash
gh issue list --label "workflow:product-backlog" --state open
```

**Enhanced Query** (with priority labels):
```bash
# Sort by priority (client-side)
gh issue list \
  --label "workflow:product-backlog" \
  --state open \
  --json number,title,labels \
  --jq '.[] | select(.labels[].name | contains("priority:")) | {number, title, priority: (.labels[] | select(.name | startswith("priority:")) | .name)}'
```

**Process**:
1. Review backlog items
2. Assess business value, urgency, dependencies
3. Assign priority
4. Hand over high-priority items to implementation

**Handover Pattern**:
```bash
# Add priority label
gh issue edit $ISSUE --add-label "priority:high"

# Product → Implementation
gh issue edit $ISSUE \
  --remove-label "workflow:product-backlog" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE --body "📊 **Handover: Product → Implementation**

Prioritized as HIGH priority. Ready for implementation.

**Business Value**: [reasoning]
**Dependencies**: [if any]

Proceed with implementation."
```

**Alternative Handovers**:
- → `workflow:research` (needs validation before implementation)
- → Close (deprioritized, won't implement)

---

### Process Modeling Workflow

**Purpose**: Improve workflows and processes

**Queue Query**:
```bash
gh issue list --label "workflow:process-modeling" --state open
```

**Smart Mode Bulk Query**:
```bash
# Query all workflows for bulk processing
for wf in triage research implementation tech-debt product-backlog process-modeling; do
  echo "=== Checking workflow:$wf ==="
  gh issue list --label "workflow:$wf" --state open --limit 5
done
```

**Process**:
1. Analyze workflow pain points
2. Design improvements
3. Update workflow documentation
4. Create regression tests

**Handover Pattern**:
```bash
# Process modeling complete
gh issue close $ISSUE --comment "📋 **Process Modeling Complete**

Workflow improvements implemented.

**Changes**:
- Updated: \`.team/prompts/[workflow].md\`
- Regression tests: \`/research/workflow-modeling/regression-tests/\`

Process improvements deployed."
```

**Alternative Handovers**:
- → `workflow:triage` (needs reassessment)
- → Any workflow (process improvement impacts specific workflow)

---

## Bulk Processing Pattern (Process Modeling Smart Mode)

**Purpose**: Process multiple issues across workflows in a batch

**Query Pattern**:
```bash
#!/bin/bash
# Query all workflow queues and collect issues

declare -a WORKFLOWS=("triage" "research" "implementation" "tech-debt" "product-backlog" "process-modeling")

for wf in "${WORKFLOWS[@]}"; do
  echo "=== Workflow: $wf ==="
  gh issue list \
    --label "workflow:$wf" \
    --state open \
    --limit 10 \
    --json number,title,url
done
```

**Batch Processing**:
1. Query all workflow queues
2. Select subset (max 5 issues per batch)
3. Process each issue
4. Track batch ID for audit

**Batch Comment Template**:
```markdown
🔄 **Batch Processing: [Batch ID]**

Processed as part of bulk run.

**Batch**: batch_2025-11-09_01
**Action**: [action taken]
**Result**: [outcome]

See batch summary for full details.
```

---

## Error Handling and Edge Cases

### Issue Misassigned

**Scenario**: Issue labeled with wrong workflow

**Solution**:
```bash
# Re-triage
gh issue edit $ISSUE \
  --remove-label "workflow:implementation" \
  --add-label "workflow:triage"

gh issue comment $ISSUE --body "⚠️ **Re-triage Requested**

Issue reassigned to triage for correct workflow designation.

**Reason**: [why misassigned]"
```

### Issue Blocked

**Scenario**: Work cannot proceed due to external dependency

**Solution**:
```bash
# Add blocked label
gh issue edit $ISSUE --add-label "status:blocked"

gh issue comment $ISSUE --body "🚧 **Blocked**

Work paused pending external dependency.

**Blocking Issue**: [dependency]
**Resolution**: [when can resume]"
```

**Or close temporarily**:
```bash
gh issue close $ISSUE --comment "🚧 Closed (blocked). Will reopen when [dependency] resolved."
```

### Workflow Cycle (e.g., Implementation → Research → Implementation)

**Scenario**: Implementation reveals need for additional research

**Solution**:
```bash
# Implementation → Research
gh issue edit $ISSUE \
  --remove-label "workflow:implementation" \
  --add-label "workflow:research"

gh issue comment $ISSUE --body "🔄 **Handover: Implementation → Research**

Implementation revealed open questions. Returning to research.

**Research Questions**:
- [question 1]
- [question 2]

Will return to implementation after research."
```

This is valid and supported by the workflow topology.

---

## Migration from Current System

### Step 1: Add Labels to Existing Issues

```bash
#!/bin/bash
# Migration script: Add labels to existing issues

# Research issues (from .team/prompts/RESEARCH_WORKFLOW.md usage)
gh issue list --search "is:open label:research" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-label "workflow:research"

# Implementation issues (from /product/backlog/)
gh issue list --search "is:open label:implementation" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-label "workflow:implementation"

# Default: all other open issues go to triage
gh issue list --search "is:open -label:workflow:*" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-label "workflow:triage"
```

### Step 2: Update Workflow Documentation

Update each workflow document (`.team/prompts/*.md`) to reference label-based queries:

**Before**:
```markdown
Select issue from GitHub issue templates or `/product/backlog/`
```

**After**:
```markdown
Query issues designated to this workflow:
\`\`\`bash
gh issue list --label "workflow:implementation" --state open
\`\`\`
```

### Step 3: Deploy Auto-Label Action

Copy `/tmp/workflow-prototypes/auto-label-new-issues.yml` to `.github/workflows/`

### Step 4: Test with New Issue

1. Create test issue
2. Verify auto-labeled with `workflow:triage`
3. Test handover (change label)
4. Verify query works

---

## Monitoring and Metrics

### Query Workflow State

```bash
#!/bin/bash
# Workflow dashboard

echo "=== Workflow State Dashboard ==="

for wf in triage research implementation tech-debt product-backlog process-modeling; do
  count=$(gh issue list --label "workflow:$wf" --state open --json number --jq 'length')
  echo "$wf: $count open issues"
done
```

**Example Output**:
```
=== Workflow State Dashboard ===
triage: 5 open issues
research: 2 open issues
implementation: 8 open issues
tech-debt: 1 open issues
product-backlog: 12 open issues
process-modeling: 0 open issues
```

### Audit Workflow Transitions

```bash
# Find recent workflow transitions (via comment search)
gh issue list --search "Handover" --state all --limit 20
```

---

## Summary

**Integration Complexity**: ✅ **LOW**

All workflows follow the same simple pattern:
1. Query by label
2. Process issue
3. Handover (change label + comment) or close

**Key Benefits**:
- ✅ Consistent interface across all workflows
- ✅ Simple queries (GitHub CLI)
- ✅ Clear handover mechanism
- ✅ Audit trail via comments
- ✅ Easy to understand and maintain

**Migration Effort**: ✅ **LOW** (add labels to existing issues, update docs)

**Recommendation**: Use these patterns for Phase 1 MVP deployment.
