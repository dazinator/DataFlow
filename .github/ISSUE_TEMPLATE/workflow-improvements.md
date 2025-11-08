---
name: Workflow Improvements
about: Discuss and propose improvements to team workflows (Research, Implementation, Tech Debt, etc.)
title: '[Workflow] '
labels: ['workflow-improvement', 'research']
assignees: ''
---

## ⚠️ IMPORTANT: Process Modeling Workflow

**This is a PROCESS MODELING issue, a specialized research workflow.**

@copilot **MUST** follow the Process Modeling workflow in `/.team/workflows/PROCESS_MODELING_WORKFLOW.md`.

### Key Points for @copilot:

- **Long-lived folder**: Use `/research/workflow-modeling/` for all process modeling work
- **Plan tracking**: Create or update `/research/workflow-modeling/plan.md`
- **Test scenarios**: Create scenarios in `/research/workflow-modeling/scenarios/[workflow-name]/`
- **Tabletop simulation**: Test all workflow changes through realistic scenarios
- **Regression tests**: Archive successful scenarios for future regression testing
- **Refinement**: Test for verbosity and redundancy - simplify where possible
- **Revert assets**: Test assets are created, used, then reverted (only workflow docs remain)

## Workflow Improvement Proposal

This issue is for discussing improvements to our team workflows and processes.

**@copilot**: Your goal is to understand the problem or task being described and the motivations for it, then assist in making the required workflow changes following the Process Modeling workflow.

### Which Workflow Does This Affect?

**Convention**: Workflow documentation follows the naming pattern `[Name]_WORKFLOW.md` in the `.team/workflows/` folder.

Please check all that apply:

- [ ] **Research Workflow** (`.team/workflows/RESEARCH_WORKFLOW.md`)
- [ ] **Implementation Workflow** (`.team/workflows/IMPLEMENTATION_WORKFLOW.md`)
- [ ] **Tech Debt Workflow** (`.team/workflows/TECH_DEBT_WORKFLOW.md`)
- [ ] **Any/All Workflows** (systemic change affecting multiple workflows)
- [ ] **Copilot Instructions** (`.github/copilot-instructions.md`)
- [ ] **Issue Templates** (`.github/ISSUE_TEMPLATE/`)

### Current State

**What is the current process/workflow?**

[Describe the current state of the workflow or process that needs improvement]

**Where is this documented?**

[Provide links or paths to relevant documentation files, e.g., `.team/workflows/RESEARCH_WORKFLOW.md`, `.github/copilot-instructions.md`, etc.]

### Problem Statement

**What issue or pain point does this improvement address?**

[Describe the specific problem, confusion, or inefficiency in the current workflow]

**Who is affected by this issue?**

- [ ] Copilot agents working on issues
- [ ] Human reviewers
- [ ] Contributors
- [ ] All of the above

### Proposed Improvement

**What specific changes are you proposing?**

[Describe the proposed improvement in detail. Be as specific as possible.]

**Which files/documents would need to be updated?**

- [ ] `.team/workflows/RESEARCH_WORKFLOW.md`
- [ ] `.team/workflows/IMPLEMENTATION_WORKFLOW.md`
- [ ] `.team/workflows/TECH_DEBT_WORKFLOW.md`
- [ ] `.github/copilot-instructions.md`
- [ ] `.github/ISSUE_TEMPLATE/research.md`
- [ ] `.github/ISSUE_TEMPLATE/implementation.md`
- [ ] `.github/ISSUE_TEMPLATE/tech-debt.md`
- [ ] `.github/workflow-improvements.md`
- [ ] Other: _______________

### Expected Benefits

**How would this improvement help?**

[Describe the expected benefits: reduced confusion, faster onboarding, better quality, etc.]

**Success metrics (if applicable):**

[How would we know if this improvement is successful?]

### Examples or References

**Are there examples from past work that illustrate this need?**

[Reference specific issues, PRs, or workflow-improvements.md entries that demonstrate the problem]

**Related workflow improvement suggestions:**

[Check `.github/workflow-improvements.md` for related suggestions]

### Implementation Considerations

**Breaking changes:**

- [ ] This would change existing workflow processes
- [ ] This is backward compatible / additive only
- [ ] Not applicable

**Effort estimate:**

- [ ] Small (documentation updates only)
- [ ] Medium (template changes, workflow restructuring)
- [ ] Large (significant process changes)

### Additional Context

[Any additional information, examples, or context that would be helpful]

---

## Process Modeling Workflow Requirements

**@copilot**: Follow the complete Process Modeling workflow as documented in `/.team/workflows/PROCESS_MODELING_WORKFLOW.md`.

### Required Steps

1. **Setup**: Use `/research/workflow-modeling/` folder with `plan.md` tracking
2. **Create Test Scenarios**: Design realistic scenarios in `/research/workflow-modeling/scenarios/[workflow-name]/`
3. **Tabletop Simulation**: Start from `.github/copilot-instructions.md` and follow workflow steps
4. **Document Results**: Record PASS/FAIL with detailed feedback in scenario files
5. **Refine Workflows**: Update workflow documentation based on test feedback
6. **Regression Testing**: Run existing regression tests to ensure no regressions
7. **Verbosity Check**: Test if sections can be simplified without losing effectiveness
8. **Revert Test Assets**: Remove all test assets created during simulation
9. **Archive Scenarios**: Move successful scenarios to `/research/workflow-modeling/regression-tests/`

**Success Criteria**: All test scenarios must PASS before workflow changes are considered complete.

---

## Relevant Documentation

### Finding Workflow Documentation

**Convention**: Workflow documentation follows the naming pattern `[Name]_WORKFLOW.md` and is located in the `.team/workflows/` folder.

**Current Team Workflow Documentation:**
- Research Workflow: `.team/workflows/RESEARCH_WORKFLOW.md`
- Implementation Workflow: `.team/workflows/IMPLEMENTATION_WORKFLOW.md`
- Tech Debt Workflow: `.team/workflows/TECH_DEBT_WORKFLOW.md`
- Process Modeling Workflow: `.team/workflows/PROCESS_MODELING_WORKFLOW.md`

**Copilot Agent Guidance:**
- Main Instructions: `.github/copilot-instructions.md`
- Workflow Improvements Tracking: `.github/workflow-improvements.md`

**Issue Templates:**
- Research: `.github/ISSUE_TEMPLATE/research.md`
- Implementation: `.github/ISSUE_TEMPLATE/implementation.md`
- Tech Debt: `.github/ISSUE_TEMPLATE/tech-debt.md`
- Workflow Improvements: `.github/ISSUE_TEMPLATE/workflow-improvements.md`
- Config: `.github/ISSUE_TEMPLATE/config.yml`

**Supporting Documentation:**
- Document Hygiene Guide: `.team/DOCUMENT_HYGIENE.md`
- Example Workflows: `.team/EXAMPLE_WORKFLOWS.md`
- Quick Start: `.github/QUICK_START.md`
