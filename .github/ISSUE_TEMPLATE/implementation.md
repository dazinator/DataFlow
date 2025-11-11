---
name: Implementation Issue
about: Implementation work from product backlog
title: 'Implementation: [Brief Description]'
labels: ['workflow:implementation']
assignees: ''
---

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

⚠️ **Note to issue creator**: Replace all `[placeholders]` with actual values. Remove options you're not using.

### Product Backlog Item

**Choose ONE of the following options:**

**Option 1 - Specific Backlog Issue** (preferred):  
**Backlog Issue**: #[NUMBER]

**Option 2 - Next from Prioritization**:  
**Backlog Item**: Next from prioritization list

---

**@copilot**: 
- If backlog issue number specified: Read the GitHub issue first
- If "Next from prioritization": Query issues with `workflow:product-backlog` label, select highest priority, PAUSE for confirmation
- See Step 2 in `.team/prompts/IMPLEMENTATION_WORKFLOW.md` for complete guidance

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

---

## For @copilot

**Workflow**: Follow `.team/prompts/IMPLEMENTATION_WORKFLOW.md` for complete implementation process.

**Quick Reference:**
- Label `workflow:implementation` ensures correct workflow routing
- Check `/implementation/plan.md` for ongoing work before starting
- See [Getting Started Guide](../.team/GETTING_STARTED.md) for coding standards
- Complete self-improvement evaluation before PR review

---

## Implementation Checklist

Based on the backlog item, the implementation should include:

- [ ] Read backlog item completely
- [ ] Review handover assets (if folder exists)
- [ ] Implementation with tests
- [ ] Performance validation (if specified in backlog item)
- [ ] Documentation updates
- [ ] Code review and validation
- [ ] Self-improvement evaluation completed

## Additional Context (if needed)

[Any additional context not in the backlog item]

## Success Criteria

- [ ] All objectives from backlog item met
- [ ] Tests passing
- [ ] Performance requirements met (if applicable)
- [ ] Documentation updated
- [ ] Self-improvement evaluation completed


