# Phase 1: Workflow Analysis Notes

## Existing Research Workflow Analysis

### Structure Overview

The research workflow in `.github/workflows/RESEARCH_WORKFLOW.md` has:
- 6 phases: Planning, Research & Exploration, Document Findings, Create Supporting Docs, Create Implementation Issue, Code Reversion
- Uses `/research/[topic]/` folder structure
- Exploratory code goes in `/poc/` or `/src/` and is reverted after reviewer approval
- ADRs go with codebase (not research folder)
- Produces implementation-ready GitHub issues

### Key Characteristics

**Strengths for Reuse:**
1. Clear phase structure that can guide agents
2. Well-defined folder structure (see FOLDER_STRUCTURE.md)
3. Code reversion process is established
4. Handover template exists
5. Self-improvement loop is integrated

**Adaptations Needed for Tech Debt:**
1. Tech debt analysis is more systematic/comprehensive than focused research
2. Multiple findings vs single focused research topic
3. Selective handover (some findings implemented, others backlogged)
4. Less deep investigation, more breadth of discovery

## Tech Debt Workflow Design Decisions

### Decision 1: Reuse Research Infrastructure

**Choice**: Tech debt workflow should be a specialized variant of research workflow

**Rationale:**
- Same core mechanics (explore, document, revert code, handover)
- Leverages existing folder structure
- Agents already understand research workflow
- Reduces cognitive load vs creating entirely new workflow

**Implementation:**
- Tech debt analyses use `/research/tech-debt-[date]/` folders
- Follow same phase structure but with tech debt specific steps
- Reuse handover templates with minor adaptations

### Decision 2: Multiple Findings Structure

**Challenge**: Tech debt analysis produces many findings, not one focused solution

**Solution**: 
```
/research/tech-debt-[date]/
├── research-plan.md                    # Discovery areas to explore
├── notes/
│   ├── exploration-notes.md            # Running notes during analysis
│   └── findings-by-category.md         # Organized findings
├── README.md                           # Summary of analysis
├── findings-report.md                  # PR-ready findings for reviewer
└── handover/
    ├── selected/                       # Items reviewer selected
    │   └── github-issue-[item].md
    └── deferred/                       # Items not selected yet
        └── [prepared for backlog move]
```

**Key**: `findings-report.md` is the main deliverable for review, structured for easy selection decisions.

### Decision 3: Findings Report Format

For reviewers to make selection decisions, they need:
- Category (e.g., compiler warnings, modern practices, DX)
- Severity (Low/Medium/High impact)
- Effort estimate (Small/Medium/Large)
- Description of the issue
- Proposed improvement
- Value proposition

**Format:**
```markdown
## Finding: [Short Title]

**Category**: [Compiler Warnings | Modern Practices | Code Quality | DX | Performance | Documentation]
**Severity**: [Low | Medium | High]  
**Effort**: [Small | Medium | Large]  
**Files Affected**: [Number or list]

### Current State
[What's the problem]

### Proposed Improvement
[What should be done]

### Value Proposition
[Why this matters - user impact, maintainability, performance]

### Implementation Approach
[High-level how-to]

### Decision
- [ ] Implement Now (create handover issue)
- [ ] Backlog (defer for future)
- [ ] Won't Fix (explain reason)
```

### Decision 4: Backlog Structure

**Location**: `/research/backlog/`

**Naming Convention**: `YYYY-MM-DD-[short-kebab-case-name].md`
- Date enables chronological browsing
- Short name enables quick scanning
- Examples: `2025-11-07-reduce-compiler-warnings.md`, `2025-11-07-modernize-namespace-declarations.md`

**Content Template**:
```markdown
# [Finding Title]

**Category**: [Category]
**Identified**: YYYY-MM-DD
**Source**: [Link to tech debt analysis PR/issue]
**Severity**: [Low/Medium/High]
**Effort**: [Small/Medium/Large]

## Problem Description
[What's the issue]

## Proposed Solution
[How to address it]

## Value
[Why this would be valuable]

## Context
[Any relevant context from discovery]

## References
- Original analysis: /research/tech-debt-[date]/findings-report.md
- Related files: [List]

## Status
- [ ] Not started
- [ ] In progress
- [ ] Completed (link to implementation PR)
```

### Decision 5: Exploration Areas & Scenarios

To ensure comprehensive tech debt discovery, define structured exploration areas:

#### Mandatory Exploration Areas

1. **New Developer Onboarding Simulation**
   - Clone repo fresh
   - Attempt to build without reading docs
   - Try to run tests
   - Document every friction point
   - Note unclear or missing documentation

2. **Build & Test Health**
   - Analyze all compiler warnings
   - Run all tests, note failures/flakiness
   - Check build time and optimization opportunities
   - Review test organization

3. **Code Quality & Modern Practices**
   - Survey language feature usage
   - Identify outdated patterns
   - Look for code duplication
   - Check naming conventions
   - Review public API design

4. **Developer Experience**
   - How easy is it to add a new block type?
   - How easy is it to write tests?
   - Are there helpful utilities/abstractions?
   - Is there code generation or boilerplate reduction?

5. **Documentation Quality**
   - README completeness
   - API documentation coverage
   - Example code quality
   - Migration guides for breaking changes
   - Architecture documentation

6. **Performance & Efficiency**
   - Obvious inefficiencies
   - Memory allocation patterns
   - Async/await anti-patterns
   - Benchmark coverage

7. **Tooling & Automation**
   - Build scripts
   - Linting/formatting
   - Code analyzers
   - CI/CD configuration

#### Optional Deep-Dive Scenarios

If time permits or if specific areas warrant investigation:

8. **Real-World Usage Scenario**
   - Pick a use case (e.g., "Process CSV files in batches")
   - Try to implement it as a new developer would
   - Document what was easy, what was hard
   - Identify missing abstractions or helpers

9. **Maintenance Scenarios**
   - What if we need to add a feature to an existing block?
   - What if we need to change the actor pattern?
   - What if we need to migrate tests?

10. **Cross-Cutting Concerns**
    - Error handling patterns
    - Logging consistency
    - Cancellation token usage
    - Resource disposal

### Decision 6: Handover vs Backlog Criteria

**Handover (Implement Now)** if:
- Reviewer explicitly selects it
- High value, reasonable effort
- Aligns with current priorities
- Blocks other work
- Security/correctness issue

**Backlog (Defer)** if:
- Nice to have but not urgent
- Large effort without immediate payoff
- Requires more design work
- Depends on other changes
- Unclear value proposition

**Won't Fix** if:
- By design (not actually tech debt)
- Cost exceeds benefit
- Would break compatibility unnecessarily
- Conflicts with architecture direction

## Workflow Integration with Existing Process

### Similarities to Research Workflow
- Same folder structure pattern
- Code reversion after approval
- Handover to implementation teams
- Self-improvement evaluation
- ADRs for significant decisions

### Differences from Research Workflow
- **Focus**: Breadth of issues vs depth on one topic
- **Output**: Multiple findings vs one solution
- **Selection**: Reviewer chooses which to implement
- **Backlog**: Non-selected items preserved separately
- **Investigation depth**: Survey vs deep dive

### Relationship to Implementation Workflow
- Selected findings → standard implementation issues
- Follow existing implementation workflow
- May be grouped if related
- No special handling needed

## Templates Needed

1. **Tech Debt Analysis Research Plan** (already covered in standard research plan)
2. **Findings Report** (new - for PR review)
3. **Tech Debt Handover Issue** (adaptation of IMPLEMENTATION_ISSUE_TEMPLATE.md)
4. **Backlog Item** (new - lightweight format)
5. **Tech Debt Workflow Guide** (new - .github/workflows/TECH_DEBT_WORKFLOW.md)

## Next Steps

1. Create workflow guide document
2. Create findings report template
3. Create backlog item template  
4. Create tech debt handover issue template
5. Apply workflow to POC codebase (Phase 2)
