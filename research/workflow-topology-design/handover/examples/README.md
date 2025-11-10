# Workflow Documentation Examples with Topology Integration

This folder contains example versions of workflow documentation files that have been updated to include workflow topology system integration.

## Purpose

These examples demonstrate how the workflow topology system would be integrated into the existing workflow documentation. They are provided as reference for the process modeling team to:

1. Review the proposed changes
2. Conduct tabletop scenario tests
3. Identify gaps or improvements needed
4. Use as templates when deploying the topology system

## Files Included

### Updated Workflow Documents

1. **IMPLEMENTATION_WORKFLOW.md** - Implementation workflow with topology integration
2. **RESEARCH_WORKFLOW.md** - Research workflow with topology integration
3. **TECH_DEBT_WORKFLOW.md** - Tech Debt workflow with topology integration
4. **PRODUCT_PRIORITIZATION_WORKFLOW.md** - Product Prioritization workflow with topology integration
5. **PROCESS_MODELING_WORKFLOW.md** - Process Modeling workflow with topology integration

### New Workflow Document

6. **TRIAGE_WORKFLOW.md** - New Triage workflow for assessing and routing issues

## Changes Made to Existing Workflows

Each of the 5 existing workflow documents has been updated with:

### 1. Workflow Queue Section

Shows how to query issues designated to that workflow:

```markdown
## Workflow Queue

**Query issues designated to this workflow:**

\`\`\`bash
gh issue list \
  --label "workflow:WORKFLOW_NAME" \
  --state open \
  --json number,title,url
\`\`\`

**Or use the query script:**
\`\`\`bash
./research/workflow-topology-design/handover/prototype/query-workflow-queue.sh WORKFLOW_NAME
\`\`\`
```

### 2. Entry Points Documentation

Lists where issues come from to enter this workflow:

```markdown
**Entry Points:**
- From Triage workflow (...)
- From Research workflow (...)
- ...
```

### 3. Handover to Next Workflow Section

Provides patterns for transitioning issues to other workflows:

```markdown
## Handover to Next Workflow

### Handover to [Next Workflow]

**When**: [Condition for handover]

\`\`\`bash
gh issue edit $ISSUE \
  --remove-label "workflow:CURRENT" \
  --add-label "workflow:NEXT"

gh issue comment $ISSUE --body "🔄 Handover: CURRENT → NEXT. [Reason]"
\`\`\`

**Or use the handover script**:
\`\`\`bash
./research/workflow-topology-design/handover/prototype/handover-issue.sh \
  $ISSUE CURRENT NEXT "Reason"
\`\`\`
```

## New Triage Workflow

The **TRIAGE_WORKFLOW.md** is a completely new workflow that provides:

- Assessment criteria for new issues
- Decision tree for workflow designation
- Handover patterns to all 6 workflows
- Common patterns and examples
- Edge case handling
- 466 lines of comprehensive guidance

This workflow serves as the "front door" for issue assessment and routing.

## How to Use These Examples

### For Tabletop Testing

1. **Read through each example** to understand the proposed changes
2. **Walk through test scenarios** using these examples as reference
3. **Identify gaps** - Are there missing handover patterns? Unclear steps?
4. **Document improvements** - What needs to be added or changed?

### For Deployment

When ready to deploy:

1. **Copy these examples** to `.team/prompts/` folder
2. **Make any refinements** based on tabletop testing feedback
3. **Update references** if folder structures changed
4. **Commit and deploy** as part of topology system activation

## Differences from Original Workflows

To see the exact differences between the original workflow files and these updated examples:

```bash
# Compare IMPLEMENTATION_WORKFLOW.md
diff .team/prompts/IMPLEMENTATION_WORKFLOW.md \
     research/workflow-topology-design/handover/examples/IMPLEMENTATION_WORKFLOW.md

# Or for all workflows
for file in IMPLEMENTATION_WORKFLOW.md RESEARCH_WORKFLOW.md TECH_DEBT_WORKFLOW.md \
            PRODUCT_PRIORITIZATION_WORKFLOW.md PROCESS_MODELING_WORKFLOW.md; do
  echo "=== Comparing $file ==="
  diff .team/prompts/$file research/workflow-topology-design/handover/examples/$file
done
```

## Integration with Handover

These examples are referenced in the main handover document:
`../archive/2025-11-09-workflow-topology-implementation-handover.md`

See that document for:
- Complete context on what was implemented
- Tabletop testing scenarios
- Deployment plan
- Gap analysis templates

## Statistics

**Lines Added per Workflow**:
- IMPLEMENTATION_WORKFLOW.md: ~96 lines
- RESEARCH_WORKFLOW.md: ~84 lines
- TECH_DEBT_WORKFLOW.md: ~64 lines
- PRODUCT_PRIORITIZATION_WORKFLOW.md: ~85 lines
- PROCESS_MODELING_WORKFLOW.md: ~60 lines
- TRIAGE_WORKFLOW.md: 466 lines (new file)

**Total**: ~855 lines of documentation added

## Notes

- These examples maintain backward compatibility with existing entry points
- The topology system is additive, not replacing existing mechanisms
- All changes are non-breaking during transition period
- Labels must be created before the system becomes active

## Questions or Feedback

When reviewing these examples, consider:

1. **Clarity**: Are the query patterns clear?
2. **Completeness**: Are all handover scenarios covered?
3. **Usability**: Can agents easily find and use the information?
4. **Consistency**: Is terminology consistent across workflows?
5. **Integration**: Do the workflows connect logically?

Document any gaps or improvements needed using the gap analysis template in the main handover document.

---

**Status**: Ready for Process Modeling Team Review  
**Created**: 2025-11-09  
**Purpose**: Reference examples for workflow topology integration
