---
name: Implementation Issue
about: Implementation work from product backlog
title: '[Implementation] '
labels: ['workflow:implementation']
assignees: ''
---

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

### Product Backlog Item

**Backlog Issue**: [e.g., #123]

**OR**

**Backlog Item**: Next from prioritization list

---

**@copilot**: 
- If backlog issue number specified: Read the GitHub issue first
- If "Next from prioritization": Query issues with `workflow:product-backlog` label, select highest priority, PAUSE for confirmation
- See Step 2 in `.team/workflows/IMPLEMENTATION_WORKFLOW.md` for complete guidance

### Product Backlog System

Backlog items are tracked as GitHub issues with the `workflow:product-backlog` label. The issue contains:
- Complete problem context and background
- Implementation guidance and recommended approach
- Success criteria
- References to design docs, ADRs, and research findings

**Handover Assets**: Check issue body for references to handover folders (typically in `/research/[topic]/handover/` or similar):
- Prototype code in `prototype/`
- Design documents in `design/`
- Performance benchmarks in `benchmarks/`

### Target Codebase

**Target**: [ ] POC (`/poc`) or [ ] Production (`/src`) or [ ] Both

The backlog item or context below should clearly indicate which codebase this implementation targets.

**@copilot**: If the target codebase is unclear from the backlog item and context, **STOP** and ask the user to clarify before proceeding.

## Implementation Checklist

Based on the backlog item, the implementation should include:

- [ ] Read backlog item completely
- [ ] Review handover assets (if folder exists)
- [ ] Update backlog item status to "In Progress"
- [ ] Implementation with tests
- [ ] Performance validation (if specified in backlog item)
- [ ] Documentation updates
- [ ] Edge cases handled (as specified in backlog item)
- [ ] Code review and validation
- [ ] Update backlog item status to "Completed"
- [ ] Archive backlog item to `/product/resolved/YYYY-MM/`
- [ ] **Before PR review: Complete self-improvement evaluation in `.github/workflow-improvements.md`**

## Additional Context (if needed)

[Any additional context not in the backlog item]

## Success Criteria

- [ ] All objectives from backlog item met
- [ ] Tests passing
- [ ] Performance requirements met (if applicable)
- [ ] Documentation updated
- [ ] Backlog item status updated and archived
- [ ] **Self-improvement evaluation completed in `.github/workflow-improvements.md`**

