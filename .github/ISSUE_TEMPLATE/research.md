---
name: Research Issue
about: Research to validate approach and produce implementation-ready issue (for POC or production code)
title: '[Research] '
labels: ['workflow:research']
assignees: ''
---

## ⚠️ IMPORTANT: Research Workflow

**This is a RESEARCH issue, not a direct implementation issue.**

@copilot **MUST** follow the Research-to-Implementation workflow in `/.team/workflows/RESEARCH_WORKFLOW.md`.

### Checklist for @copilot

Before starting work on this research issue:
- [ ] I have read `/.team/workflows/RESEARCH_WORKFLOW.md` 
- [ ] I will create `/research/[topic-name]/` folder structure
- [ ] I understand exploratory code in `/poc/` or `/src/` will be REVERTED after reviewer approval
- [ ] I will create implementation-ready issue in `/research/[topic]/handover/` folder
- [ ] I will save important prototype code in `/research/[topic]/handover/prototype/` before reversion
- [ ] **Before PR review: I will complete self-improvement evaluation in `.github/workflow-improvements.md`**

### Key Points for @copilot:
- **Research folder**: Create `/research/[topic-name]/` with research plan, findings, design docs, ADRs
- **Exploratory code**: You MAY write code in `/poc/` or `/src/` to validate approaches
- **Code reversion**: After reviewer approval, exploratory code will be REVERTED (not merged)
- **Prototype preservation**: Save reference implementations in `/research/[topic]/handover/prototype/`
- **Outcome**: Documentation + implementation-ready issue for engineering team

## Research Context

**Research Objective**: [What needs to be validated/explored]

**Target Codebase**: [POC / Production / Both]

**Key Requirements**:
- Read `/poc/README.md` and relevant documentation in `/poc/docs/`
- Create research folder structure - see `/research/FOLDER_STRUCTURE.md`
- Freely explore and validate approaches with code (POC or production)
- Document research findings following the folder structure reference
- Create supporting docs (design, ADRs) in research folder
- Create implementation-ready issue using `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md`
- Update `/poc/docs/POC_GLOSSARY.md` with new terminology
- **Code reversion happens only when PR reviewer approves and requests it**

**Expected Deliverables**:
- [ ] Research folder (see `/research/FOLDER_STRUCTURE.md` for structure)
- [ ] Research plan, documentation, design docs, and ADRs
- [ ] Implementation-ready issue in handover folder
- [ ] Updated glossary (if new concepts)
- [ ] Prototype code in handover folder (if applicable)
- [ ] After reviewer approval: All exploratory code changes reverted

**Note**: This research will produce a comprehensive implementation issue for handoff to engineering. Code reversion happens at a specific point when the PR reviewer approves the research findings and explicitly requests it - NOT automatically.

## Research Questions

[What specific questions does this research need to answer?]

1. Question 1
2. Question 2

## Validation Approach

[How will you validate/test different approaches?]

## Success Criteria

- [ ] Approach validated through prototyping
- [ ] Research comprehensively documented
- [ ] Implementation issue contains complete context
- [ ] All supporting documentation created
- [ ] Prototype code captured (if needed)
- [ ] Code changes reverted, only docs remain
- [ ] **Self-improvement evaluation completed in `.github/workflow-improvements.md`**
