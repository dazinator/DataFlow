# Research and Exploration Workflow

This document describes the recommended workflow for conducting research and exploration work within the repository. It addresses how to handle pivots, preserve exploration history, and deliver implementation-ready outcomes to engineering teams.

**Scope**: This workflow applies to research on any code in the repository - whether POC code (evolving architecture) or existing production code. The process is the same regardless of which codebase you're researching.

**📁 Folder Structure**: For the canonical research folder structure and path conventions, see [/research/FOLDER_STRUCTURE.md](/research/FOLDER_STRUCTURE.md). That document is the single source of truth for folder layout.

## Overview

Research in this repository supports two distinct outcomes:

1. **Direct Integration**: Research leads to code and documentation that can be directly integrated into the codebase
2. **Research-to-Implementation**: Research validates viability and produces a comprehensive GitHub issue for implementation assignment (this is the focus of this workflow)

## Problem Statement

During research, we need to:
1. **Enable free exploration** - Researchers can freely branch, prototype, and validate approaches without commitment to merge
2. **Preserve exploration knowledge** - Don't lose what was tried and why
3. **Deliver actionable outcomes** - Research culminates in implementation-ready GitHub issues with full context
4. **Maintain clean separation** - Research artifacts (docs, issues, tests) are kept; exploratory code changes are reverted at the appropriate time
5. **Support handoff excellence** - Developers should be able to implement solutions from the outcome issue and supporting docs alone

## Research-to-Implementation Workflow

This workflow is for research that will produce an **implementation-ready GitHub issue** rather than direct code changes.

**What is "Implementation-Ready"?** An issue that contains comprehensive specifications, test scenarios, performance requirements, design references, and all context needed for an engineering team to independently implement the solution in the main codebase without consulting the researcher.

### Phase 1: Research Planning and Setup

When starting research that will result in an implementation handoff:

1. **Create Research Folder Structure**:

Create the standard research folder for your topic. See [Research Folder Structure](/research/FOLDER_STRUCTURE.md) for the complete canonical definition.

Quick reference - create these folders and files:
```bash
/research/[topic]/
├── research-plan.md      # Start here
├── notes/                # Working notes
└── (other folders created in later phases)
```

The complete structure includes `README.md`, `design/`, `adr/`, `benchmarks/`, and `handover/` folders that you'll create as the research progresses. See the [folder structure reference](/research/FOLDER_STRUCTURE.md#standard-research-folder-structure) for the full layout.

2. **Create Research Plan**:

Create `/research/[topic]/research-plan.md` with your research objectives:
```markdown
# Research Plan: [Topic]

## Research Objective
[What needs to be validated/explored]

## Research Questions
- Question 1
- Question 2

## Validation Approach
[How will we validate/test approaches]

## Expected Outcomes
- Research documentation in /research/[topic]/
- Implementation-ready GitHub issue in /research/[topic]/handover/
- Supporting design/ADR documentation in /research/[topic]/

## Timeline
[Estimated research duration]
```

### Phase 2: Research and Exploration

During research, freely explore and validate:

**Code Exploration**:
- Write exploratory code in any codebase (POC or existing) to validate feasibility
- Create prototypes to test approaches
- Validate performance through benchmarks
- **Important**: This code is temporary and will be reverted when the PR reviewer approves - its purpose is validation and learning

**Documentation During Research**:
- Keep working notes in `/research/[topic]/notes/`
- Document what you try, what works, what doesn't
- Capture insights, performance data, trade-offs
- Reference useful tests or benchmarks conceptually

**Tests and Benchmarks**:
- Create tests to validate concepts
- Run benchmarks to measure performance
- Save benchmark data in `/research/[topic]/benchmarks/`
- **Important**: Test/benchmark code will be reverted, but document:
  - Test scenarios that would be valuable for implementation
  - Benchmark methodologies and findings
  - Edge cases discovered through testing

### Phase 3: Document Research Findings

Create comprehensive research documentation. See [Research Folder Structure](/research/FOLDER_STRUCTURE.md) for path conventions.

**Research Document** (create as `/research/[topic]/README.md`):
```markdown
# Research: [Topic]

## Research Objective
[What was being investigated]

## Approaches Explored
### Approach 1: [Name]
**Description**: [What was tried]
**Findings**: [What was learned]
**Performance**: [If measured]
**Pros/Cons**: 
- ✅ Advantage 1
- ❌ Disadvantage 1

### Approach 2: [Name]
[Same structure]

## Comparative Analysis
[Compare approaches against each other]

## Recommendations
**Recommended Approach**: [Which approach to implement]
**Rationale**: [Why this approach]

## Implementation Considerations
[Things implementation team should know]

## Test Scenarios to Implement
[Document valuable tests discovered during research]

## Benchmark Guidance
[Document benchmarks that would validate the implementation]

## References
- Links to design docs created
- Links to ADRs created
- Links to prototype code (before reversion)
```

### Phase 4: Create Supporting Documentation

Based on research findings, create supporting documentation alongside the research for implementation. See [Research Folder Structure](/research/FOLDER_STRUCTURE.md#path-reference-guide) for path conventions.

**Design Documentation** (create in `/research/[topic]/design/` folder):
- Architectural approach recommended
- API/interface sketches
- System interactions and data flows
- Design principles to follow

**Architecture Decision Records** (create in `/research/[topic]/adr/` folder with format `YYYY-MM-DD-[topic].md`):
```markdown
# ADR-[N]: [Decision Title]

**Date**: YYYY-MM-DD
**Status**: Accepted
**Context**: Research Issue #[N]

## Context
[Why this decision was needed]

## Decision
[What was decided based on research]

## Alternatives Considered
[What other options were evaluated during research]

## Consequences
### Positive
- Benefit 1
- Benefit 2

### Negative
- Trade-off 1
- Trade-off 2

## Implementation Guidance
[Specific guidance for implementation team]
```

**Test Implementation Guide** (in research doc or separate):
```markdown
# Test Implementation Guide: [Topic]

## Critical Test Scenarios
[Test cases discovered during research that must be implemented]

## Edge Cases
[Edge cases found during prototyping]

## Performance Validation
[Benchmarks that should be included in implementation]
```

### Phase 5: Create Implementation-Ready GitHub Issue

The primary deliverable: a comprehensive GitHub issue that enables implementation.

**Location**: Create in the `handover/` subfolder of your research. See [Research Folder Structure](/research/FOLDER_STRUCTURE.md#standard-research-folder-structure) for the complete layout.

Path: `/research/[topic]/handover/github-issue-[description].md`

For example structure, see the [example in the folder structure reference](/research/FOLDER_STRUCTURE.md#example-distributed-epochs-research).

**Template** (see `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md` for full template):

```markdown
# Implementation Issue: [Feature/Component Name]

## Context and Objectives

### Problem Statement
[What problem does this solve]

### Research Background
Research was conducted to validate approach and inform implementation.
See: `/research/[topic]/README.md`

### Objectives
- [ ] Objective 1
- [ ] Objective 2

## Implementation Guidance

### Recommended Approach
[High-level architectural approach based on research]

### Design References
- **Design Doc**: `/research/[topic]/design/[component].md`
- **ADR**: `/research/[topic]/adr/YYYY-MM-DD-[decision].md`
- **Research Findings**: `/research/[topic]/README.md`

### API/Interface Design
[Sketch of proposed APIs/interfaces discovered during research]

### Key Implementation Considerations
1. [Consideration 1 - from research]
2. [Consideration 2 - from research]

### Reusable Patterns
[Code patterns or approaches that proved valuable during prototyping]

## Testing and Validation

### Test Coverage Required
[Test scenarios identified during research]

### Performance Validation
[Benchmark requirements from research]

### Edge Cases
[Edge cases discovered during prototyping]

## Constraints and Requirements

### Technical Constraints
- [Constraint 1]
- [Constraint 2]

### Performance Requirements
[Based on research benchmarks]

### Compatibility Requirements
[Integration points, backward compatibility]

## Alternatives Explored

### Alternative 1: [Name]
**Pros**: [Benefits]
**Cons**: [Drawbacks]
**Why Not Chosen**: [Rationale]

### Alternative 2: [Name]
[Same structure]

## References and Resources

### Documentation
- Research: `/research/[topic]/README.md`
- Design: `/research/[topic]/design/[component].md`
- ADR: `/research/[topic]/adr/YYYY-MM-DD-[topic].md`
- Test Guide: [If created]

### Prior Work
- Related Issues: #[N]
- Related PRs: #[N]

## Success Criteria

- [ ] All objectives met
- [ ] Test coverage as specified
- [ ] Performance requirements met
- [ ] Documentation updated
```

**This markdown file serves as the blueprint for creating the actual GitHub issue when ready for implementation assignment.**

### Phase 6: Code Reversion and PR Finalization

**When to Revert**: Code reversion happens at a specific point in the workflow - when the PR reviewer approves the research findings and explicitly requests that code changes be reverted. This is NOT automatic and should only occur when the reviewer confirms the research documentation and handover materials are complete and ready for implementation team handoff.

**PR Review and Approval Process**:
1. Submit PR with research documentation and exploratory code changes
2. PR reviewer evaluates research findings, documentation quality, and handover materials
3. Reviewer approves research and explicitly requests code reversion
4. At that point, revert all exploratory code changes while keeping documentation

Before the PR is merged (and only after reviewer approval), revert all exploratory code changes while keeping documentation:

**What to Keep**:
- ✅ Research documentation in `/research/[topic]/`
- ✅ Implementation-ready GitHub issue in `/research/[topic]/handover/`
- ✅ Design documentation in `/research/[topic]/design/`
- ✅ ADRs in `/research/[topic]/adr/`
- ✅ Benchmark data and analysis in `/research/[topic]/benchmarks/`
- ✅ Test implementation guides (as documentation)
- ✅ Updated glossary entries in `/poc/docs/POC_GLOSSARY.md` (if applicable)

**What to Revert** (only when reviewer approves):
- ❌ All exploratory code changes (POC or non-POC)
- ❌ All exploratory test files
- ❌ Benchmark code (keep benchmark results documentation)
- ❌ Prototype implementations
- ❌ Temporary helper code

**Reversion Process** (execute only after reviewer approval):
```bash
# 1. Commit all documentation first
git add research/
git add poc/docs/
git commit -m "Research documentation and implementation handover materials"

# 2. Revert exploratory code changes
# For POC code:
git checkout HEAD -- poc/DataFlow.POC/
git checkout HEAD -- poc/DataFlow.POC.Tests/
git checkout HEAD -- poc/DataFlow.POC.Benchmarks/

# For non-POC code (if applicable):
git checkout HEAD -- src/
# (adjust paths based on what was researched)

# 3. Verify only documentation remains
git status
# Should show only changes in research/ and poc/docs/
```

**PR Review Checklist** (for reviewer before requesting reversion):
- [ ] Research documentation is comprehensive
- [ ] Implementation issue contains all necessary context
- [ ] Design docs and ADRs are complete
- [ ] Handover materials are ready for implementation team
- [ ] Test scenarios are documented (not implemented)
- [ ] Benchmark findings are documented (benchmark code removed)

**After Reviewer Approval**:
- [ ] All exploratory code changes have been reverted
- [ ] Only documentation and handover files remain in changes
- [ ] Ready for merge and implementation team handoff

## Benefits of Research-to-Implementation Workflow

1. **Free Exploration**: Researchers can freely experiment without commitment to merge code
2. **Knowledge Preservation**: All research is documented comprehensively
3. **Clean Handoff**: Implementation teams receive complete, actionable specifications
4. **Separation of Concerns**: Research validation vs. production implementation are distinct
5. **Reduced Risk**: Approaches are validated before significant implementation effort
6. **Complete Context**: Implementation teams understand not just "what" but "why"
7. **Reusable Insights**: Research documents valuable test scenarios and benchmarks
8. **Clean POC**: POC codebase doesn't accumulate exploratory code

## Direct Integration Workflow (Alternative)

For research that will be directly integrated (not handed off), use the original workflow:

**Plan Folder Structure**:
```bash
/poc/docs/plans/PHASE_X_DESCRIPTION/
├── plan.md                     # The exploration plan itself
├── proposed-docs/              # Documentation intended for integration if adopted
│   ├── glossary-additions.md  # → Merge to POC_GLOSSARY.md (if adopted)
│   ├── adr-*.md               # → Move to /adr/ folder (if adopted)
│   ├── design-*.md            # → Move to /design/ folder (if adopted)
│   └── research-findings.md   # → Move to /research/ folder (always valuable)
└── archived/                   # Dismissed/pivoted artifacts
    ├── README.md              # Explains what was dismissed and why
    ├── adr-*-dismissed.md     # ADRs for approaches not adopted
    └── design-*-dismissed.md  # Design docs for approaches not adopted
```

**Code Organization**:
```
/poc/DataFlow.POC/
├── Core/                           # Production-track code
├── Exploratory/                    # All exploratory implementations
│   └── Phase7EventChannel/         # Phase-specific exploratory work
│       ├── EventChannelNode.cs
│       └── EventEdgeStrategies.cs
```

**After PR Approval** (Direct Integration):
- Promote adopted docs from `proposed-docs/` to main locations
- Promote adopted code from `Exploratory/` to production
- Keep archived artifacts for history

See the original sections below for details on this workflow.

## Example: Research-to-Implementation Flow

**Research Issue**: "Investigate and validate approach for distributed epoch coordination"

### During Research (Steps 1-5)

**1. Create Research Plan** (`/poc/docs/plans/research-distributed-epochs.md`):
- Define research questions about distributed coordination
- List validation approaches
- Estimate timeline

**2. Exploration**:
- Write prototype code in POC projects to test coordination strategies
- Create tests to validate correctness of different approaches
- Run benchmarks to measure coordination overhead
- Document findings in working notes

**3. Research Documentation** (`/research/distributed-epoch-coordination-research.md`):
```markdown
# Research: Distributed Epoch Coordination

## Approaches Explored
### Approach 1: Centralized Coordinator
- Tested with prototype
- Benchmark: 1000 ops/sec with 5ms latency
- ✅ Simple, predictable
- ❌ Single point of coordination

### Approach 2: Consensus-Based
- Tested with prototype
- Benchmark: 800 ops/sec with 12ms latency
- ✅ No single point
- ❌ Higher coordination cost

## Recommendation: Hybrid Approach
Centralized for common case, fallback to consensus
```

**4. Supporting Documentation**:
- Design doc: `/research/distributed-epochs/design/distributed-epoch-architecture.md`
- ADR: `/research/distributed-epochs/adr/2025-11-04-hybrid-epoch-coordination.md`
- Test guide: Documented in research README

**5. Implementation Issue** (`/research/distributed-epochs/handover/github-issue-implement-distributed-epochs.md`):
```markdown
# Implementation Issue: Distributed Epoch Coordination

## Context
Research validated hybrid approach for distributed epoch coordination.
See: /research/distributed-epochs/README.md

## Implementation Guidance
### Recommended Approach
Implement hybrid coordination: centralized for common case, consensus fallback

### Design References
- Design: /research/distributed-epochs/design/distributed-epoch-architecture.md
- ADR: /research/distributed-epochs/adr/2025-11-04-hybrid-epoch-coordination.md

### API Design
[Interfaces and classes to implement]

### Test Coverage Required
[Test scenarios from research]

## Performance Requirements
- Target: 900+ ops/sec
- Max latency: 8ms p99
[Based on research benchmarks]
```

### Before PR Merge (Step 6)

**After Reviewer Approval**:

Only after the PR reviewer approves the research findings and explicitly requests code reversion, execute:

```bash
# Keep all docs (research and poc/docs)
git add research/
git add poc/docs/

# Revert all code changes (adjust paths based on what was researched)
# If researching POC code:
git checkout HEAD -- poc/DataFlow.POC/
git checkout HEAD -- poc/DataFlow.POC.Tests/
git checkout HEAD -- poc/DataFlow.POC.Benchmarks/

# If researching non-POC code:
# git checkout HEAD -- src/
```

**Final PR Contains** (after reviewer approval and code reversion):
- ✅ `/research/distributed-epochs/README.md`
- ✅ `/research/distributed-epochs/design/distributed-epoch-architecture.md`
- ✅ `/research/distributed-epochs/adr/2025-11-04-hybrid-epoch-coordination.md`
- ✅ `/research/distributed-epochs/handover/github-issue-implement-distributed-epochs.md`
- ✅ Updated `/poc/docs/POC_GLOSSARY.md` (if applicable)
- ❌ No exploratory code changes (reverted after approval)

### After PR Merge

**Implementation Team**:
1. Reads implementation issue in `/research/distributed-epochs/handover/github-issue-implement-distributed-epochs.md`
2. Reviews all referenced documentation in `/research/distributed-epochs/`
3. Copies design docs and ADRs to appropriate codebase folders as needed (`/poc/docs/` or `/src/docs/`)
4. Implements in appropriate codebase (POC or `src/`) following guidance
5. Implements tests based on scenarios in research README
6. Validates performance against research benchmarks

## Integration with GitHub Issues

When creating GitHub issues for POC research work:

### For Research-to-Implementation Issues

```markdown
## POC Research Context

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/research/RESEARCH_WORKFLOW.md`.

**Research Objective**: [What needs to be validated/explored]

**Expected Deliverables**:
1. Research documentation in `/research/`
2. Implementation-ready GitHub issue in `/poc/docs/plans/`
3. Supporting design/ADR documentation in `/poc/docs/`
4. All POC code changes reverted before PR merge

**Note**: This research will produce a comprehensive implementation issue for handoff to engineering, not direct code changes.
```

### For Direct Integration Issues

```markdown
## POC Context

⚠️ This is a POC issue. @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`.

**During Research**:
1. Create plan folder: `/poc/docs/plans/PHASE_X_DESCRIPTION/`
2. Draft docs in `proposed-docs/` as work evolves
3. Keep exploratory code in `/Exploratory/` folders
4. On pivots: move dismissed docs to `archived/` with reasons

**On PR Approval**:
1. Promote adopted docs from `proposed-docs/` to main doc locations
2. Promote adopted code from `/Exploratory/` to production locations
3. Keep archived materials for historical reference
```

## Choosing Between Workflows

**Use Research-to-Implementation When**:
- Exploring new architectural approaches
- Need to validate multiple alternatives
- Outcome requires main codebase changes (in `src/`)
- Want to separate research validation from implementation
- Implementation will be significant effort
- Need comprehensive specification for implementation team

**Use Direct Integration When**:
- Changes are contained to POC codebase
- Approach is fairly certain, just needs execution
- Iterative refinement during implementation is expected
- Research and implementation are tightly coupled
- Team doing research will do implementation

## Summary

### Research-to-Implementation Workflow
This workflow enables free research exploration while delivering comprehensive implementation specifications:
- **Research freely** in POC without guarantee of merging code
- **Document comprehensively** in `/research/` and supporting docs
- **Create actionable handoff** via implementation-ready GitHub issue
- **Revert code changes** while keeping all documentation
- **Enable excellent handoff** so implementation teams have complete context

### Direct Integration Workflow  
This workflow supports POC work that will be directly integrated:
- **Stage docs** in `proposed-docs/` near the plan
- **Keep exploratory code** in `/Exploratory/` folders
- **Archive dismissed approaches** with clear explanations
- **Promote on approval** to production locations

**Result**: Clean POC codebase, comprehensive research documentation, and excellent handoff to implementation teams.
