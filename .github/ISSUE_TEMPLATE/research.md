---
name: Research Issue
about: Research to validate approach and produce implementation-ready issue (for POC or production code)
title: '[Research] '
labels: ['workflow:research']
assignees: ''
---

## ⚠️ IMPORTANT: Research Workflow

**This is a RESEARCH issue, not a direct implementation issue.**

@copilot **MUST** follow the Research-to-Implementation workflow in `/.team/prompts/RESEARCH_WORKFLOW.md`.

**Workflow Routing:**
- Label: `workflow:research` ensures correct workflow routing
- See: `.github/copilot-instructions.md` for workflow system
- **Before PR review**: Complete self-improvement evaluation

---

## Research Context

**Research Objective**: [What needs to be validated/explored]

**Target Codebase**: [POC / Production / Both]

---

## Research Questions

[What specific questions does this research need to answer?]

1. Question 1
2. Question 2

---

## Validation Approach

[How will you validate/test different approaches?]

---

## Success Criteria

- [ ] Approach validated through prototyping
- [ ] Research comprehensively documented
- [ ] Implementation issue contains complete context
- [ ] All supporting documentation created
- [ ] Prototype code captured (if needed)
- [ ] Code changes reverted, only docs remain
- [ ] Self-improvement evaluation completed

---

## For @copilot

**Workflow**: Follow `/.team/prompts/RESEARCH_WORKFLOW.md` for complete research process.

**Key Points:**
- Create `/research/[topic-name]/` folder structure
- Exploratory code in `/poc/` or `/src/` will be REVERTED after reviewer approval
- Outcome: Documentation + implementation-ready issue for engineering team
- See [Research Folder Structure](/research/FOLDER_STRUCTURE.md) for conventions
