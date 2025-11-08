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

Propose improvements to our team workflows and processes.

**@copilot**: Your goal is to understand the problem and proposed solution, then implement the required workflow changes following the Process Modeling workflow.

---

## Which Workflow Does This Affect?

Check all that apply:

- [ ] Research Workflow
- [ ] Implementation Workflow
- [ ] Tech Debt Workflow
- [ ] Product Prioritization Workflow
- [ ] Process Modeling Workflow
- [ ] Any/All Workflows (systemic change affecting multiple workflows)
- [ ] Copilot Instructions
- [ ] Issue Templates

## Improvement Mode

Choose one:

- [ ] **Issue-Driven**: I'm proposing a specific improvement (fill out sections below)
- [ ] **Backlog-Driven**: Process the next unaddressed entry from `.github/workflow-improvements.md` (you can skip the sections below - @copilot will extract context from the backlog)

---

## Problem Statement

**What issue or pain point does this improvement address?**

[Describe the specific problem, confusion, or inefficiency in the current workflow]

**Who is affected?**

- [ ] Copilot agents working on issues
- [ ] Human reviewers
- [ ] Contributors
- [ ] All of the above

## Proposed Improvement

[Describe your proposed solution. Be as specific as possible about what should change and why.]

## Expected Benefits

[How would this improvement help? What would be better?]

## Additional Context (Optional)

[Any examples, references to past issues, or additional context that would be helpful]

---

## For @copilot

**Investigation Steps:**
1. Read `.team/workflows/PROCESS_MODELING_WORKFLOW.md` for complete workflow
2. Update `/research/workflow-modeling/plan.md` to track this work
3. Discover affected files during workflow investigation
4. Create test scenarios in `/research/workflow-modeling/scenarios/`
5. Validate changes through tabletop simulation
6. Update relevant documentation based on test results
7. Archive successful scenarios as regression tests
8. Complete self-improvement evaluation

**Documentation Convention:** Workflow documentation follows the naming pattern `[Name]_WORKFLOW.md` in the `.team/workflows/` folder.
