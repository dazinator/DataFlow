---
name: Workflow Improvement Suggestion
about: Suggest a specific improvement to team workflows or processes
title: '[Workflow] '
labels: ['workflow:process-modeling']
assignees: ''
---

## ⚠️ IMPORTANT: Process Modeling Workflow

**This is a PROCESS MODELING issue for suggesting a workflow improvement.**

@copilot **MUST** follow the Process Modeling workflow in `/.team/prompts/PROCESS_MODELING_WORKFLOW.md`.

---

## Which Workflow Does This Affect?

Check all that apply:

- [ ] Research Workflow
- [ ] Implementation Workflow
- [ ] Tech Debt Workflow
- [ ] Product Prioritization Workflow
- [ ] Process Modeling Workflow
- [ ] Triage Workflow
- [ ] Any/All Workflows (systemic change affecting multiple workflows)
- [ ] Copilot Instructions
- [ ] Issue Templates

---

## Problem Statement

**What issue or pain point does this improvement address?**

[Describe the specific problem, confusion, or inefficiency in the current workflow]

**Who is affected?**

- [ ] Copilot agents working on issues
- [ ] Human reviewers
- [ ] Contributors
- [ ] All of the above

---

## Proposed Improvement

[Describe your proposed solution. Be as specific as possible about what should change and why.]

---

## Expected Benefits

[How would this improvement help? What would be better?]

---

## Additional Context (Optional)

[Any examples, references to past issues, or additional context that would be helpful]

---

## For @copilot

**Investigation Steps:**
1. Read `.team/prompts/PROCESS_MODELING_WORKFLOW.md` for complete workflow
2. Update `/research/workflow-modeling/plan.md` to track this work
3. Create test scenarios in `/research/workflow-modeling/scenarios/[workflow-name]/`
4. Validate changes through tabletop simulation
5. Update relevant documentation based on test results
6. Archive successful scenarios (or revert if appropriate)
7. Complete self-improvement evaluation

**Documentation Convention:** Workflow documentation follows the naming pattern `[Name]_WORKFLOW.md` in the `.team/prompts/` folder.
