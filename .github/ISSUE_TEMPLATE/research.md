---
name: Research Issue
about: Research to validate approach and produce implementation-ready issue (for POC or production code)
title: '[Research] '
labels: ['research']
assignees: ''
---

## Research Context

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/research/RESEARCH_WORKFLOW.md`.

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
