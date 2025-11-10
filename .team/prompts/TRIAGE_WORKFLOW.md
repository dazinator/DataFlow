# Triage Workflow

This workflow guides the initial assessment of new issues and designation to appropriate workflows.

---

## Overview

**Purpose**: Assess new issues and designate them to the appropriate workflow (research, implementation, tech debt, product prioritization, or process modeling).

**Entry Point**: Issues automatically labeled with `workflow:triage` when created.

**Typical Duration**: 5-15 minutes per issue (single mode) or 30-90 minutes (bulk mode)

---

## Triage Modes

The triage workflow supports two modes:

### Single Issue Mode (Default)

**When to use**: You're assigned to a specific issue that needs triage

**Behavior**:
- Process the assigned issue only
- Assess and designate to appropriate workflow
- Stop after completing this one issue

### Bulk Triage Mode

**When to use**: You're assigned to a "Bulk Triage" issue created from the `triage.md` template

**Behavior**:
- Query ALL issues with `workflow:triage` label
- Exclude the bulk triage issue itself
- Process each issue in the queue sequentially
- Update the bulk triage issue with progress summaries
- Continue until queue is empty
- Close the bulk triage issue when complete

**How to identify bulk mode**:
- Issue title starts with `[Triage] Bulk triage`
- Issue body contains "Bulk Triage Instructions for @copilot"
- Issue explicitly requests processing the entire triage queue

---

## Quick Start

**Single Issue Mode**:
1. Read and assess the assigned issue
2. Determine appropriate workflow
3. Handover to designated workflow with comment

**Bulk Triage Mode**:
1. Query all issues in triage queue
2. For each issue: read, assess, and handover
3. Update bulk triage issue with summary
4. Close bulk triage issue when done

**⚠️ Comment Prefix Convention:**
- Prefix ALL comments with `[Copilot-Workflow: Triage]` to confirm you're following this workflow
- Example: `[Copilot-Workflow: Triage] After reviewing this issue, I recommend...`

---

## Step 1: Query Triage Queue

**For Copilot Agents** (use MCP tools):

```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:triage"],
    state="OPEN"
)
```

**For Manual/CI Use** (GitHub CLI):

```bash
gh issue list \
  --label "workflow:triage" \
  --state open \
  --json number,title,url,createdAt
```

---

## Label Cleanup (Before Starting Triage)

**⚠️ IMPORTANT**: Before triaging issues, check for and clean up conflicting workflow labels.

### Workflow Label Validation

When you start triage work (whether single-issue or bulk mode):

1. **For each issue you're about to triage**, check its labels for workflow conflicts
2. **Identify conflicts**: If an issue has MULTIPLE workflow labels (e.g., both `workflow:triage` AND `workflow:implementation`)
3. **Determine correct label**: 
   - If the issue is in the triage queue awaiting assessment, `workflow:triage` is correct
   - If it has another workflow label, that suggests it was already triaged but the label wasn't removed
4. **Remove conflicting labels**: Remove any workflow label that is NOT `workflow:triage`
5. **Add cleanup comment** noting what was corrected

### Label Cleanup Example

**For Copilot Agents** (use MCP tools):

```python
# Example: Issue has both workflow:triage and workflow:implementation labels
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    labels=["workflow:triage"]  # Only keep the correct label
)

add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    body="[Copilot-Workflow: Triage] 🏷️ Label cleanup: Removed conflicting `workflow:implementation` label. This issue is being re-triaged."
)
```

**Why this matters**: Issues should have exactly ONE workflow label at a time. Multiple labels create confusion about which workflow owns the issue.

**When to skip**: If the issue only has `workflow:triage` label (no conflicts), proceed directly to triage assessment.

---

## Step 2: Assessment Criteria

For each issue, assess the following:

### Issue Type

**Bug Report**:
- Is this a defect in existing functionality?
- Is the bug reproducible?
- What is the severity/priority?

**Feature Request**:
- Is this a new feature or enhancement?
- Is the requirement clear?
- Is research needed to validate feasibility?

**Research Question**:
- Does this need investigation or prototyping?
- Are there unknown unknowns?

**Tech Debt**:
- Does this address code quality, maintainability, or architecture?
- Is this a refactoring or cleanup task?

**Process Improvement**:
- Does this relate to workflows, tooling, or team processes?
- Is this a meta-issue about how we work?

### Clarity & Scope

- [ ] Requirements are clear and specific
- [ ] Scope is well-defined
- [ ] Success criteria are stated
- [ ] No significant unknowns

### Complexity Assessment

**Low**: Simple change, clear path forward → `workflow:implementation`
**Medium**: Some unknowns, may need design → `workflow:research` or `workflow:implementation`
**High**: Significant unknowns, needs validation → `workflow:research`

---

## Step 3: Workflow Designation

Based on assessment, designate to appropriate workflow:

### → Research Workflow

**When to use**:
- Significant unknowns or technical uncertainty
- Needs prototyping or proof-of-concept
- Multiple approaches need evaluation
- Feasibility needs validation

**Example handover**:
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:triage" \
  --add-label "workflow:research"

gh issue comment $ISSUE --body "🔍 **Triage → Research**

This issue requires research to validate feasibility and approach.

**Research Questions**:
- [Question 1]
- [Question 2]

See \`.team/prompts/RESEARCH_WORKFLOW.md\` for research process."
```

---

### → Implementation Workflow

**When to use**:
- Requirements are clear and specific
- No significant technical unknowns
- Straightforward bug fix or enhancement
- Design is already validated

**Example handover**:
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:triage" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE --body "⚙️ **Triage → Implementation**

Requirements are clear. Ready for implementation.

**Summary**: [Brief description]

See \`.team/prompts/IMPLEMENTATION_WORKFLOW.md\` for implementation process."
```

---

### → Tech Debt Workflow

**When to use**:
- Code quality or maintainability issue
- Refactoring or cleanup needed
- Architecture improvement
- Test coverage gap

**Example handover**:
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:triage" \
  --add-label "workflow:tech-debt"

gh issue comment $ISSUE --body "🔧 **Triage → Tech Debt**

This is a technical debt item requiring analysis and prioritization.

**Debt Category**: [Code Quality / Architecture / Testing / Documentation]

See \`.team/prompts/TECH_DEBT_WORKFLOW.md\` for tech debt process."
```

---

### → Product Prioritization Workflow

**When to use**:
- Multiple competing priorities
- Needs business value assessment
- Resource allocation decision needed
- Part of larger backlog needing prioritization

**Example handover**:
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:triage" \
  --add-label "workflow:product-backlog"

gh issue comment $ISSUE --body "📊 **Triage → Product Prioritization**

This item needs prioritization against other backlog items.

**Context**: [Business context or dependency info]

See \`.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md\` for prioritization process."
```

---

### → Process Modeling Workflow

**When to use**:
- Workflow or process improvement
- Tooling enhancement
- Team process optimization
- Meta-issue about how we work

**Example handover**:
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:triage" \
  --add-label "workflow:process-modeling"

gh issue comment $ISSUE --body "📋 **Triage → Process Modeling**

This is a process improvement item.

**Area**: [Workflow / Tooling / Documentation / Other]

See \`.team/prompts/PROCESS_MODELING_WORKFLOW.md\` for process modeling workflow."
```

---

### → Close Issue

**When to close**:
- Duplicate of existing issue
- Invalid or not reproducible
- Out of scope
- Won't fix

**Example close**:
```bash
gh issue close $ISSUE --comment "❌ **Closing Issue**

**Reason**: [Duplicate of #123 / Invalid / Out of Scope / Won't Fix]

[Additional context if needed]"
```

---

## Step 4: Handover Methods

**For Copilot Agents** (use MCP tools):

```python
# Update workflow label
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    labels=["workflow:TARGET_WORKFLOW"]
)

# Add handover comment
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    body="🔄 Triage → [Target Workflow]\n\nReason for handover"
)
```

**For Manual Use** (GitHub CLI):

Use the examples shown in Step 3 above, or:

```bash
gh issue edit ISSUE_NUMBER --remove-label "workflow:triage" --add-label "workflow:TARGET_WORKFLOW"
gh issue comment ISSUE_NUMBER --body "🔄 Triage → [Target Workflow]\n\nReason for handover"
```

**Example**:
```bash
gh issue edit 123 --remove-label "workflow:triage" --add-label "workflow:research"
gh issue comment 123 --body "🔄 Triage → Research\n\nNeeds validation of approach before implementation"
```

---

## Common Patterns

### Feature Request with Unknowns

```
Issue: "Add support for X feature"
Assessment: Unclear if X is feasible with current architecture
Designation: workflow:research
Reason: "Need to validate if X is compatible with pull-based architecture"
```

### Clear Bug Report

```
Issue: "NullReferenceException in BatchBlock when maxBatchSize=0"
Assessment: Reproducible bug, clear fix needed
Designation: workflow:implementation
Reason: "Clear bug with known fix location"
```

### Code Quality Issue

```
Issue: "Refactor ActorPool to use modern C# patterns"
Assessment: Tech debt, code cleanup
Designation: workflow:tech-debt
Reason: "Refactoring task to improve maintainability"
```

### Competing Priorities

```
Issue: "Add feature Y" (one of many feature requests)
Assessment: Valid feature but needs prioritization
Designation: workflow:product-backlog
Reason: "Valid feature request needing business value assessment"
```

---

## Bulk Mode Execution

When assigned to a **Bulk Triage** issue, follow this process:

### Step 1: Identify Bulk Mode

Check if the assigned issue is a bulk triage request:
- Title starts with `[Triage] Bulk triage`
- Body contains "Bulk Triage Instructions for @copilot"
- Explicitly requests processing entire triage queue

If YES → Continue with bulk mode execution
If NO → Follow single issue mode (process only the assigned issue)

### Step 2: Query and Filter

**Query the full triage queue**:
```python
issues = list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:triage"],
    state="OPEN"
)
```

**Filter out the bulk triage issue itself**:
- Get the current issue number (the bulk triage issue)
- Exclude it from the list of issues to process
- Only process actual issues needing triage, not the coordination issue

### Step 3: Process Each Issue

For each issue in the filtered queue:

1. **Read the issue** to understand context
2. **Assess using Step 2 criteria** (issue type, clarity, complexity)
3. **Determine workflow** using Step 3 designation guidance
4. **Update workflow label** using MCP tools
5. **Add handover comment** with reasoning

**Example iteration**:
```python
for issue in filtered_issues:
    # Read issue
    issue_data = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue['number']
    )
    
    # Assess and determine workflow (manual analysis)
    # ...
    
    # Update label
    issue_write(
        method="update",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue['number'],
        labels=["workflow:implementation"]  # or appropriate workflow
    )
    
    # Add handover comment
    add_issue_comment(
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue['number'],
        body="[Copilot-Workflow: Triage] 🔄 Triage → Implementation\n\n[Reasoning...]"
    )
```

### Step 4: Track Progress

**Update the bulk triage issue with progress summaries**:

After every 5 issues (or when complete), add a progress comment:

```python
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=BULK_TRIAGE_ISSUE_NUMBER,
    body="""[Copilot-Workflow: Triage] Progress Update

**Processed**: 5 issues
**Remaining**: 3 issues

**Distribution**:
- → Research: 2 issues
- → Implementation: 2 issues
- → Tech Debt: 1 issue

Continuing...
"""
)
```

### Step 5: Complete and Close

When all issues in the queue are processed:

1. **Add final summary comment** to bulk triage issue:
```python
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=BULK_TRIAGE_ISSUE_NUMBER,
    body="""[Copilot-Workflow: Triage] ✅ Bulk Triage Complete

**Total Issues Processed**: 8

**Final Distribution**:
- → Research: 2 issues (#124, #127)
- → Implementation: 3 issues (#125, #128, #131)
- → Tech Debt: 2 issues (#126, #130)
- → Closed: 1 issue (#129 - duplicate)

All issues in triage queue have been processed.
"""
)
```

2. **Close the bulk triage issue**:
```python
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=BULK_TRIAGE_ISSUE_NUMBER,
    state="closed"
)
```

### Edge Cases

**Empty Queue**:
- If no issues need triage (queue only contains the bulk triage issue)
- Comment that queue is empty
- Close the bulk triage issue immediately

**Issues Needing Clarification**:
- If an issue needs clarification, add a comment requesting it
- Keep the issue in `workflow:triage`
- Note it in the bulk triage summary
- Continue processing other issues

**Errors or Blockers**:
- If you encounter an issue you can't triage (unclear, ambiguous)
- Add a comment requesting help or clarification
- Note it in the bulk triage summary
- Continue with remaining issues

---

## Decision Tree

```mermaid
flowchart TD
    A[New Issue] --> B{Clear Requirements?}
    B -->|No| C{Needs Research?}
    B -->|Yes| D{Technical Unknowns?}
    
    C -->|Yes| E[workflow:research]
    C -->|No| F[Clarify in comments<br/>Keep in triage]
    
    D -->|Yes| E
    D -->|No| G{Type?}
    
    G -->|Feature/Bug| H[workflow:implementation]
    G -->|Tech Debt| I[workflow:tech-debt]
    G -->|Process| J[workflow:process-modeling]
    G -->|Prioritization Needed| K[workflow:product-backlog]
    G -->|Invalid/Duplicate| L[Close]
```

---

## Best Practices

### Do:
- ✅ Read the full issue context before designating
- ✅ Ask clarifying questions if requirements are unclear
- ✅ Provide clear reasoning in handover comment
- ✅ Link to relevant documentation or similar issues
- ✅ Assess urgency and add priority labels if needed

### Don't:
- ❌ Rush to designate without understanding context
- ❌ Send unclear issues to implementation
- ❌ Overthink simple issues (bugs usually go to implementation)
- ❌ Skip the handover comment (audit trail is important)

---

## Edge Cases

### Issue Needs Clarification

If issue is unclear:
1. Add comment requesting clarification
2. Keep in `workflow:triage`
3. Wait for response
4. Re-assess after clarification

```bash
gh issue comment $ISSUE --body "❓ **Clarification Needed**

To properly triage this issue, please provide:
- [Specific information needed]

Keeping in triage until clarified."
```

### Multiple Workflows Applicable

If issue could go to multiple workflows:
1. Choose the **first** necessary workflow
2. Note in handover that additional workflows may follow
3. Trust the workflow to hand over appropriately

**Example**: Research → Implementation → Tech Debt (if research reveals debt)

### Issue Already in Progress

If someone is already working on it:
1. Verify they're following the appropriate workflow
2. Update label to match current workflow
3. Add comment noting the correction

---

## Monitoring

### View Triage Queue Status

```bash
./.github/scripts/workflow/workflow-dashboard.sh
```

### Find Stale Triage Issues

```bash
gh issue list \
  --label "workflow:triage" \
  --state open \
  --json number,title,createdAt \
  --jq '.[] | select(.createdAt | fromdateiso8601 < (now - 604800)) | "Issue #\(.number) (>7 days old): \(.title)"'
```

---

## Success Metrics

**Good Triage**:
- Issues move to appropriate workflow within 24 hours
- Clear handover comments with reasoning
- Low rate of re-triage (issue sent to wrong workflow)
- Minimal back-and-forth for clarification

**Poor Triage**:
- Issues sit in triage for days
- Issues frequently re-triaged
- Handover comments lack context
- Implementation gets unclear issues

---

## Integration with Other Workflows

### From Triage

```mermaid
flowchart LR
    Triage[workflow:triage] --> Research[workflow:research]
    Triage --> Implementation[workflow:implementation]
    Triage --> TechDebt[workflow:tech-debt]
    Triage --> Product[workflow:product-backlog]
    Triage --> Process[workflow:process-modeling]
    Triage --> Close[Closed]
```

### To Triage (Re-triage)

Any workflow can send an issue back to triage if:
- Requirements change significantly
- Initial assessment was incorrect
- Issue needs re-evaluation

---

## References

- **Integration Patterns**: `/research/workflow-topology-design/design/integration-patterns.md`
- **Workflow Topology ADR**: `.team/adr/2025-11-09-workflow-state-storage.md`
- **Research Findings**: `/research/workflow-topology-design/README.md`
- **Helper Scripts**: `/research/workflow-topology-design/handover/prototype/`

---

## Quick Reference

**Query triage queue** (Copilot agents):
```python
list_issues(owner="uniun-technology", repo="lib-dataflow", labels=["workflow:triage"], state="OPEN")
```

**Query triage queue** (manual):
```bash
gh issue list --label "workflow:triage" --state open
```

**Handover to research** (Copilot agents):
```python
issue_write(method="update", owner="uniun-technology", repo="lib-dataflow", issue_number=ISSUE, labels=["workflow:research"])
add_issue_comment(owner="uniun-technology", repo="lib-dataflow", issue_number=ISSUE, body="🔄 Triage → Research\n\nReason")
```

**Handover to research** (manual):
```bash
gh issue edit ISSUE --remove-label "workflow:triage" --add-label "workflow:research"
gh issue comment ISSUE --body "🔄 Triage → Research\n\nReason"
```

**Handover to implementation** (Copilot agents):
```python
issue_write(method="update", owner="uniun-technology", repo="lib-dataflow", issue_number=ISSUE, labels=["workflow:implementation"])
add_issue_comment(owner="uniun-technology", repo="lib-dataflow", issue_number=ISSUE, body="🔄 Triage → Implementation\n\nReason")
```

**Handover to implementation** (manual):
```bash
gh issue edit ISSUE --remove-label "workflow:triage" --add-label "workflow:implementation"
gh issue comment ISSUE --body "🔄 Triage → Implementation\n\nReason"
```
