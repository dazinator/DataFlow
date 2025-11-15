---
name: Tech Debt Discovery
about: Systematic analysis to identify and prioritize technical debt and improvement opportunities
title: '[Tech Debt] '
labels: ['workflow:tech-debt']
assignees: ''
---

## ⚠️ IMPORTANT: Tech Debt Discovery Workflow

**This is a TECH DEBT DISCOVERY issue, a specialized research workflow.**

@copilot **MUST** follow the Tech Debt duty in `.team/duties/TECH_DEBT_DUTY.md`.

### Checklist for @copilot

Before starting work on this tech debt discovery:
- [ ] I have read `.team/duties/TECH_DEBT_DUTY.md`
- [ ] I have read `docs/DOCUMENT_HYGIENE.md` for documentation standards
- [ ] I will create `/research/tech-debt-[date]/` folder structure
- [ ] I will **review existing backlog** (`/research/backlog/`) before new exploration
- [ ] I understand exploratory code in `/poc/` or `/src/` will be REVERTED after reviewer approval
- [ ] I will create findings report for reviewer selection
- [ ] I will create handover issues for **selected** findings
- [ ] I will move **non-selected** findings to `/research/backlog/` with date-based naming
- [ ] **Before PR review: I will complete self-improvement evaluation**

### Key Points for @copilot:
- **Review backlog first**: Check `/research/backlog/` for existing items to prioritize
- **Research folder**: Create `/research/tech-debt-[date]/` with findings report
- **Exploratory code**: You MAY write code to validate issues and solutions
- **Code reversion**: After reviewer approval, exploratory code will be REVERTED (not merged)
- **Findings report**: Present findings with priority/effort for reviewer selection
- **Selective handover**: Reviewer chooses which findings to implement
- **Backlog non-selected items**: Move deferred items to `/research/backlog/YYYY-MM-DD-[name].md`
- **Outcome**: Findings report + selective handovers + backlog items

## Tech Debt Analysis Context

**Analysis Date**: [YYYY-MM-DD]

**Target Codebase**: [ ] POC (`/poc`) [ ] Production (`/src`) [ ] Both

**Time Box**: [Recommended: 6-12 hours based on codebase size]

**Analysis Scope Decision**:
- Analyze as single codebase if POC and Production share patterns/issues
- Separate analysis if codebases are distinct with different priorities

## Exploration Areas

Select areas to explore (check all that apply):

- [ ] **Backlog Review** - Review `/research/backlog/` for existing items to prioritize
- [ ] New developer onboarding simulation
- [ ] Build and test health (compiler warnings, test failures)
- [ ] Code quality and modern practices
- [ ] Developer experience (tooling, boilerplate, friction points)
- [ ] Documentation quality
- [ ] Performance and efficiency
- [ ] Tooling and automation
- [ ] [Custom area]: ___________

## Expected Deliverables

- [ ] Backlog review completed (existing items validated or selected)
- [ ] Research folder created: `/research/tech-debt-[date]/`
- [ ] Findings report created with priority/effort for each finding
- [ ] Handover issues created for reviewer-selected findings
- [ ] Non-selected findings moved to `/research/backlog/YYYY-MM-DD-[name].md`
- [ ] Exploratory code changes reverted (after reviewer approval)
- [ ] **Self-improvement evaluation completed**

## Success Criteria

- [ ] Systematic exploration of selected areas completed
- [ ] Findings report presented for reviewer selection
- [ ] Selected findings have handover issues ready for implementation
- [ ] Non-selected findings preserved in backlog
- [ ] Code changes reverted, only documentation remains
- [ ] **Self-improvement evaluation completed**

---

**Note**: This workflow prioritizes existing backlog items before discovering new ones. Always review and validate backlog first, then proceed with new exploration if needed.
