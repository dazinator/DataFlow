# Research: Tech Debt Discovery Workflow

**Research Period**: November 2025  
**Target**: Workflow design + POC codebase validation  
**Status**: Complete - Ready for Implementation Handover

## Executive Summary

This research designed and validated a systematic workflow for discovering and managing technical debt. The workflow was then applied to the DataFlow POC/production codebase, identifying 8 concrete findings across security, code quality, and developer experience areas.

### Key Outcomes

1. **Tech Debt Workflow Created** - Complete workflow documentation in `.team/prompts/TECH_DEBT_WORKFLOW.md`
2. **Backlog Management System** - Established `/research/backlog/` with date-based naming and management guidelines
3. **Workflow Validated** - Applied to real codebase, generated actionable findings
4. **Templates Created** - Findings report, handover issue, and backlog item templates

### Research Artifacts

- **Workflow Guide**: `.team/prompts/TECH_DEBT_WORKFLOW.md` (19KB)
- **Backlog System**: `/research/backlog/README.md` with usage guide
- **Example Analysis**: 8 findings report from POC analysis
- **Example Handovers**: 1 selected finding with implementation issue
- **Example Backlog**: 2 deferred findings demonstrating backlog format

## Research Organization

### Documentation Structure

```
/research/tech-debt-workflow/
├── README.md (this file)
├── research-plan.md
├── notes/
│   ├── phase1-workflow-analysis.md
│   └── exploration-notes.md
├── findings-report.md          # Example findings from POC analysis
└── handover/
    └── selected/
        └── github-issue-fix-otel-vulnerability.md

/.github/workflows/
└── TECH_DEBT_WORKFLOW.md       # Main workflow documentation

/research/backlog/              # New folder for deferred tech debt
├── README.md                   # Backlog usage guide
├── 2025-11-07-modernize-file-scoped-namespaces.md
└── 2025-11-07-add-enumerator-cancellation-attributes.md
```

## Research Questions Answered

### 1. Workflow Structure ✅

**Decision**: Tech debt workflow is a specialized variant of the research workflow.

**Rationale**:
- Reuses established infrastructure (folder structure, reversion process, handover pattern)
- Agents already familiar with research workflow mechanics
- Reduces cognitive load vs entirely new workflow

**Key Differences**:
- Focus: Breadth of issues vs depth on one topic
- Output: Multiple findings vs single solution
- Selection: Reviewer chooses which to implement
- Backlog: Non-selected items preserved separately

### 2. Discovery Process ✅

**Defined 7 Standard Exploration Areas**:

1. New Developer Onboarding Simulation
2. Build and Test Health
3. Code Quality and Modern Practices
4. Developer Experience
5. Documentation Quality
6. Performance and Efficiency
7. Tooling and Automation

**Optional Deep-Dive Scenarios**: Real-world usage, maintenance scenarios, cross-cutting concerns.

### 3. Reporting and Review ✅

**Findings Report Format**:
- Each finding gets: Category, Priority, Effort, Value, Implementation approach
- Reviewer checkboxes for easy decision-making
- Numbered findings (TD-001, TD-002...) for reference
- Summary statistics for overview

### 4. Handover Process ✅

**Template Created**: Adapted from implementation issue template
- Includes tech debt context
- Links to findings report
- Clear implementation guidance
- Success criteria

### 5. Backlog Management ✅

**Structure**: `/research/backlog/`  
**Naming**: `YYYY-MM-DD-[short-kebab-case-name].md`  
**Content**: Lightweight format with problem, solution, value, status  
**Maintenance**: Guidance for browsing, searching, promoting, and archiving

### 6. Exploration Scenarios ✅

Systematized the suggested approaches:
- "New developer" scenario formalized as standard exploration area
- Build/test analysis as structured activity
- Compiler warnings analysis with categorization
- Modern practices checklist created

## Workflow Design Decisions

### Reuse Research Infrastructure

- Same `/research/[topic]/` folder structure
- Code reversion after reviewer approval
- Handover to implementation teams
- Self-improvement evaluation

### Multiple Findings Structure

- `findings-report.md` as primary deliverable
- Reviewer selection via checkboxes
- Selected → `handover/selected/`
- Non-selected → `/research/backlog/`

### Backlog System

- Date-prefixed naming for chronological browsing
- Lightweight format (vs full implementation issues)
- Searchable by category, priority, effort
- Actionable for future promotion

## POC Codebase Analysis Results

### Findings Summary

**8 Total Findings**:
- 2 High Priority (security vulnerability, nullable warnings)
- 5 Medium Priority (modern C#, async patterns, documentation, DX)
- 1 Low Priority (tooling)

**Effort Distribution**:
- 5 Small effort
- 2 Medium effort
- 1 Large effort (but automatable)

**Categories**:
- Security: 1
- Code Quality: 4
- Modern C# Practices: 1
- Documentation: 1
- Developer Experience: 1 (duplicate of existing research)

### Example Findings

**TD-001**: Security vulnerability in OpenTelemetry package (High/Small)
- Clear actionable fix
- Example handover issue created

**TD-002**: 56 nullable reference type warnings (High/Medium)
- Requires careful analysis
- Significant safety improvement

**TD-003**: Missing EnumeratorCancellation attributes (Medium/Small)
- Quick win
- Example backlog item created

**TD-004**: 283 files using non-file-scoped namespaces (Medium/Large)
- Large scope but fully automatable
- Example backlog item created

### Workflow Validation

The workflow successfully:
✅ Guided systematic exploration  
✅ Produced actionable findings  
✅ Created clear reviewer decision points  
✅ Demonstrated handover and backlog patterns  
✅ Took reasonable time (~6 hours for full analysis)

## Integration with Existing Workflows

### With Research Workflow
- Tech debt analysis follows same phase structure
- Uses same reversion process
- ADRs go with codebase (not research folder)
- Self-improvement evaluation required

### With Implementation Workflow
- Selected findings → standard implementation issues
- Follow existing implementation workflow
- No special handling needed

### Preventing Duplication
- TD-007 identified duplicate of existing testing approaches research
- Workflow includes cross-referencing step to avoid duplication

## Templates Created

### 1. Tech Debt Workflow Guide
Location: `.team/prompts/TECH_DEBT_WORKFLOW.md`
- Complete 6-phase workflow
- Standard exploration areas
- Findings report format
- Handover and backlog guidance

### 2. Findings Report Template
Embedded in workflow guide
- Per-finding format with decision checkboxes
- Summary statistics
- Categorization scheme

### 3. Handover Issue Template
Example: `handover/selected/github-issue-fix-otel-vulnerability.md`
- Tech debt context
- Problem statement
- Implementation guidance
- Success criteria

### 4. Backlog Item Template
Examples in `/research/backlog/`
- Lightweight format
- Problem, solution, value, status
- References to original analysis

## Recommendations

### Workflow Usage

**When to Use**:
- Periodic codebase health reviews (quarterly?)
- Before major architectural changes
- During onboarding (document friction points)
- After major milestones (reflect on improvements)

**Not For**:
- Immediate bug fixes (use standard PR)
- Feature development (use implementation workflow)
- Deep investigation of specific approaches (use research workflow)

### Implementation Priority

If implementing findings from POC analysis:

1. **TD-001** - Security vulnerability (High/Small) - immediate
2. **TD-003** - EnumeratorCancellation (Medium/Small) - quick win
3. **TD-006** - Documentation gaps (Medium/Small) - onboarding value
4. **TD-008** - .editorconfig (Low/Small) - prevents regression
5. **TD-002** - Nullable warnings (High/Medium) - safety improvement
6. **TD-004** - File-scoped namespaces (Medium/Large) - automatable

### Backlog Maintenance

- **Review quarterly** - Priorities change over time
- **Remove obsolete** - Code changes may address issues
- **Celebrate completion** - Update status when implemented
- **Keep actionable** - Be ruthless about quality

## Success Metrics

### Quantitative ✅
- Workflow documentation: Complete (19KB guide)
- Exploration areas: 7 defined + 3 optional
- Finding categories: 6 categories established
- Templates: 4 created (workflow, report, handover, backlog)

### Qualitative ✅
- **Ease of use**: Workflow is clear, agents can follow independently
- **Actionability**: Findings format enables straightforward decisions
- **Reusability**: Workflow applicable to any codebase
- **Integration**: Fits naturally with existing workflows
- **Value**: Produced genuinely useful findings (validated through POC analysis)

### Validation ✅
- Applied workflow to POC codebase
- Generated 8 real findings
- Created example handover (TD-001)
- Created example backlog items (TD-003, TD-004)
- Demonstrated complete cycle

## Learnings

### What Worked Well

1. **Reusing Research Infrastructure** - Reduced complexity significantly
2. **Structured Exploration Areas** - Ensured comprehensive coverage
3. **Findings Report Format** - Made reviewer decisions straightforward
4. **Date-Based Backlog Naming** - Easy to browse and search
5. **Lightweight Backlog Format** - Low friction to create items

### What Could Be Improved

1. **Time Estimation** - Hard to predict analysis time beforehand
2. **Duplication Detection** - Need explicit step to check existing research
3. **Automation Guidance** - Could provide more tool recommendations
4. **Prioritization Criteria** - Could be more specific about High/Medium/Low

### Suggested Enhancements

See self-improvement evaluation in `.github/workflow-improvements.md` for detailed suggestions.

## Next Steps

### For This Research

1. ✅ Workflow documented
2. ✅ Backlog system established
3. ✅ Templates created
4. ✅ Validated on POC
5. ⏳ Awaiting reviewer feedback on findings
6. ⏳ Create selected handovers based on reviewer decisions
7. ⏳ Complete self-improvement evaluation
8. ⏳ Revert exploratory code (after approval)

### For Future Tech Debt Analyses

1. Use `.team/prompts/TECH_DEBT_WORKFLOW.md` as guide
2. Create `/research/tech-debt-[date]/` folder
3. Follow 6-phase process
4. Submit findings report for review
5. Create handovers and backlog items based on selection

## References

### Created Documentation
- Main workflow: `.team/prompts/TECH_DEBT_WORKFLOW.md`
- Backlog guide: `/research/backlog/README.md`
- Research plan: `research-plan.md`
- Design notes: `notes/phase1-workflow-analysis.md`

### Example Artifacts
- Findings report: `findings-report.md`
- Example handover: `handover/selected/github-issue-fix-otel-vulnerability.md`
- Example backlog: `/research/backlog/2025-11-07-*.md`

### Related Workflows
- Research workflow: `.team/prompts/RESEARCH_WORKFLOW.md`
- Implementation workflow: `.team/prompts/IMPLEMENTATION_WORKFLOW.md`

---

**Research Status**: Complete and validated  
**Next Action**: Reviewer evaluation of findings report  
**Estimated Time to Implement Workflow**: ~6-12 hours per tech debt analysis
