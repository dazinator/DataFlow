# Research and Exploration Workflow

---

## ⚠️ CRITICAL: This is NOT Direct Implementation

**If you're a Copilot agent working on a research issue:**

### DO (During Research):
- ✅ **Read [Document Hygiene Guide](/.github/DOCUMENT_HYGIENE.md)** before creating/updating documentation
- ✅ Create `/research/[topic]/` with research plan, findings, design docs
- ✅ **Place ADRs in `/poc/docs/adr/` or `/src/docs/adr/`** (with the codebase they govern, NOT in research folder)
- ✅ Write exploratory code in `/poc/` or `/src/` to validate approaches
- ✅ Create tests to validate concepts
- ✅ Run benchmarks to measure performance
- ✅ Document everything you try and learn

### DO (Before PR Merge - After Reviewer Approval):
- ✅ Save important prototype code to `/research/[topic]/handover/prototype/`
- ✅ Create implementation-ready issue in `/research/[topic]/handover/`
- ✅ **REVERT all exploratory code changes** from `/poc/` and `/src/`
- ✅ Keep all documentation in `/research/[topic]/`
- ✅ **Keep ADRs in `/poc/docs/adr/` or `/src/docs/adr/`** (ADRs are NOT reverted - they belong with the codebase)
- ✅ **Complete self-improvement evaluation** (see below)

### DO (Self-Improvement Loop - Required Before PR Review):
- ✅ **Evaluate research workflow effectiveness** for this task
- ✅ **Document what worked well** and what didn't in the research process
- ✅ **Propose specific improvements** to the research workflow documentation
- ✅ **Add suggestions to `.github/workflow-improvements.md`** in the Research Workflow section
- ✅ Check if your suggestion already exists before adding

**Example**: "The handover template was clear, but lacked guidance on when to create benchmarks. Suggest adding decision criteria: performance-sensitive (required), API validation (optional), prototypes (skip)."

See `.github/copilot-instructions.md` for detailed guidance on the self-improvement loop.

### DON'T:
- ❌ Merge exploratory code into `/poc/` or `/src/` (it will be reverted)
- ❌ Skip creating the research folder structure
- ❌ Skip documenting your findings and rationale
- ❌ Revert code before reviewer approval

### OUTCOME:
Research produces **documentation + implementation issue**, not merged code. The exploratory code is for validation and learning, then gets reverted. The engineering team implements based on your specifications.

---

This document describes the recommended workflow for conducting research and exploration work within the repository. It addresses how to handle pivots, preserve exploration history, and deliver implementation-ready outcomes to engineering teams.

**Scope**: This workflow applies to research on any code in the repository - whether POC code (evolving architecture) or existing production code. The process is the same regardless of which codebase you're researching.

**📁 Folder Structure**: For the canonical research folder structure and path conventions, see [/research/FOLDER_STRUCTURE.md](/research/FOLDER_STRUCTURE.md). That document is the single source of truth for folder layout.

## Overview

Research in this repository supports two distinct outcomes:

1. **Direct Integration**: Research leads to code and documentation that can be directly integrated into the codebase
2. **Research-to-Implementation**: Research validates viability and produces a comprehensive GitHub issue for implementation assignment (this is the focus of this workflow)

## Workflow Queue

**Query issues designated to this workflow:**

```bash
gh issue list \
  --label "workflow:research" \
  --state open \
  --json number,title,url
```

**Or use the query script:**
```bash
./.team/scripts/workflow/query-workflow-queue.sh research
```

**Entry Points:**
- From Triage workflow (needs validation)
- From Tech Debt workflow (needs research)
- From Implementation workflow (uncovered unknowns)

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

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

## Success Metrics
Define how success will be measured:
- **Quantitative**: Concrete measurements (e.g., "reduce code by 40%", "improve throughput by 2x")
- **Qualitative**: Subjective improvements (e.g., "improved readability", "clearer intent")
- **Baseline**: Current state to measure against (capture before starting)
- **Validation**: How improvements will be demonstrated (tests, benchmarks, examples)

## Validation Approach
[How will we validate/test approaches]
- Create comparative tests showing "before" vs "after" (see Comparative Testing below)
- Run benchmarks to measure performance impact
- Validate edge cases and error handling

## Expected Outcomes
- Research documentation in /research/[topic]/
- Implementation-ready GitHub issue in /research/[topic]/handover/
- Supporting design documentation in /research/[topic]/design/
- **ADRs in /poc/docs/adr/ or /src/docs/adr/** (with the codebase they govern)
- Prototype code in /research/[topic]/handover/prototype/ (if applicable)

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

**Comparative Testing (Recommended)**:

Create "before/after" demo tests to validate improvements:

```csharp
// Example: TestHelpersDemoTests.cs
[Fact]
public async Task OLD_Pattern_VerboseServiceProviderSetup()
{
    // 8-10 lines of manual DI setup
    var services = new ServiceCollection();
    services.AddScoped<MyActor>();
    services.AddScoped<IDatabase>(_ => mockDb);
    var provider = services.BuildServiceProvider();
    var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    // ... test logic
}

[Fact]
public async Task NEW_Pattern_TestServiceBuilder()
{
    // 3 lines with helper
    var scopeFactory = TestServiceBuilder.Create()
        .WithActor<MyActor>()
        .WithScoped<IDatabase>(mockDb)
        .BuildScopeFactory();
    // ... test logic
}
```

**Benefits**:
- Proves prototype actually works
- Demonstrates concrete improvement with working code
- Validates success metrics (e.g., 60% code reduction)
- Provides clear value demonstration for stakeholders
- Save in `/research/[topic]/handover/prototype/` for implementation reference

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

**Architecture Decision Records** (create in `/poc/docs/adr/` or `/src/docs/adr/` based on which codebase, with format `YYYY-MM-DD-[topic].md`):
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

### Phase 5: Create Product Backlog Item

The primary deliverable: a comprehensive product backlog item that enables implementation team to work from the product backlog system.

**See `/product/README.md` for complete product backlog system documentation.**

#### Creating the Backlog Item

**1. Create backlog item file** in `/product/backlog/`:

- **Naming convention**: `research-YYYY-MM-DD-[short-name].md`
- **Template**: Use `/product/backlog-item-template.md` as starting point
- **Example**: `research-2025-11-08-flow-composability-unification.md`

**2. Fill in all sections** with research findings:

```markdown
# [Title]

**Backlog ID**: research-2025-11-08-flow-composability-unification
**Source**: Research
**Category**: [Feature/Enhancement/etc.]
**Status**: Active
**Created**: YYYY-MM-DD
**Updated**: YYYY-MM-DD

## Summary
[Brief 1-2 sentence description]

## Context
[Background from research - why this work is needed]
- Link to research folder: `/research/[topic]/`
- Key findings summary

## Implementation Guidance
[High-level guidance based on research]
- Recommended approach from research
- Key requirements
- Design considerations

### External Dependencies (if applicable)
**IMPORTANT**: If your research involves sample code or implementations that require external services (databases, message queues, OTLP endpoints, etc.), document them here:
- List each external dependency with connection details
- State whether runtime validation required or build-only sufficient
- Provide alternative validation approaches if service optional
- See template in `/product/backlog-item-template.md` for format

## Success Criteria
- [ ] Objectives from research
- [ ] Performance requirements (if applicable)
- [ ] Test coverage specified

## Handover Assets
- **Location**: `/product/backlog/research-YYYY-MM-DD-[name]/`
- **Contents**:
  - Prototype code (if applicable)
  - Design documents
  - Benchmarks
  - Test scenarios

## References
- Research findings: `/research/[topic]/README.md`
- Design docs: `/research/[topic]/design/`
- ADRs: `/poc/docs/adr/` or `/src/docs/adr/`
- Related issues: #[N]

## Notes
[Additional context from research]
```

#### Creating Handover Folder (if needed)

If you have supporting assets (prototype code, design docs, benchmarks):

**1. Create handover folder** with same name as backlog item (minus `.md`):
```bash
mkdir -p product/backlog/research-YYYY-MM-DD-[name]/{prototype,design,benchmarks}
```

**2. Copy assets to handover folder**:

- **Prototype code** → `prototype/` subfolder
  - Only copy key reference implementations
  - Include README explaining what each file demonstrates
  - Document performance metrics achieved

- **Design documents** → `design/` subfolder
  - Copy critical design docs from research folder
  - Or reference existing docs in research folder (avoid duplication)

- **Benchmarks** → `benchmarks/` subfolder
  - Copy benchmark data and analysis
  - Include performance requirements

**Example**:
```bash
# Create handover folder
mkdir -p product/backlog/research-2025-11-08-flow-composability/prototype

# Copy key prototype files (not entire projects)
cp research/flow-composability-unification/handover/prototype/*.cs \
   product/backlog/research-2025-11-08-flow-composability/prototype/

# Create README for prototypes
cat > product/backlog/research-2025-11-08-flow-composability/prototype/README.md << 'EOF'
# Prototype Code

## UnifiedFlowBuilder.cs
Reference implementation showing recommended API design.
- Demonstrates fluent builder pattern
- Validates type safety approach
- Performance: 1000+ ops/sec in benchmarks
EOF
```

#### Linking from Research Folder

**1. Create handover reference** in research folder:

Option A - Create handover README pointing to backlog:
```bash
cat > research/[topic]/handover/README.md << 'EOF'
# Implementation Handover

Implementation handover is managed through the product backlog system.

**Backlog Item**: `/product/backlog/research-YYYY-MM-DD-[name].md`

See the backlog item for:
- Implementation guidance
- Success criteria  
- Handover assets (prototype code, designs, benchmarks)
- All implementation details

**Product Backlog System**: See `/product/README.md` for complete documentation.
EOF
```

Option B - Reference backlog item in research README:
```markdown
## Implementation Handover

This research is ready for implementation.

**Backlog Item**: `/product/backlog/research-YYYY-MM-DD-[name].md`

See backlog item for complete implementation guidance and handover assets.
```

#### Notify Product Team

**1. Comment on research PR**:
```markdown
Research complete. Product backlog item created:

**Backlog Item ID**: research-YYYY-MM-DD-[name]
**Path**: `/product/backlog/research-YYYY-MM-DD-[name].md`

Ready for product team prioritization.
```

**2. Product team will**:
- Review backlog item
- Prioritize in `/product/prioritization.md`
- Implementation team will select based on priorities

### Phase 6: Create Implementation-Ready GitHub Issue (DEPRECATED)

⚠️ **This phase is deprecated**. Use Phase 5 (Create Product Backlog Item) instead.

**Old workflow**: Created handover issues in `/research/[topic]/handover/github-issue-*.md`
**New workflow**: Create backlog items in `/product/backlog/research-YYYY-MM-DD-[name].md`

See Phase 5 above for current process.

### Phase 7: Code Reversion and PR Finalization

**When to Revert**: Code reversion happens at a specific point in the workflow - when the PR reviewer approves the research findings and explicitly requests that code changes be reverted. This is NOT automatic and should only occur when the reviewer confirms the research documentation, product backlog item, and handover materials are complete and ready for implementation team handoff.

**PR Review and Approval Process**:
1. Submit PR with research documentation and exploratory code changes
2. PR reviewer evaluates research findings, documentation quality, and handover materials
3. Reviewer approves research and explicitly requests code reversion
4. At that point, revert exploratory code changes while keeping documentation and production-ready artifacts

Before the PR is merged (and only after reviewer approval), revert exploratory code changes while keeping documentation and production-ready artifacts:

**What to Keep** (NOT reverted):
- ✅ Research documentation in `/research/[topic]/`
- ✅ **Product backlog item** in `/product/backlog/research-YYYY-MM-DD-[name].md`
- ✅ **Handover assets** in `/product/backlog/research-YYYY-MM-DD-[name]/` (prototype code, designs, benchmarks)
- ✅ Design documentation in `/research/[topic]/design/`
- ✅ ADRs in `/poc/docs/adr/` or `/src/docs/adr/` (ADRs belong with the codebase, NOT in research folder)
- ✅ Benchmark data and analysis in `/research/[topic]/benchmarks/`
- ✅ Test implementation guides (as documentation)
- ✅ Updated glossary entries in `/poc/docs/POC_GLOSSARY.md` (if applicable)
- ✅ **Production-ready test utilities** (e.g., test helpers, common test patterns) in test projects
- ✅ **Documentation updates** (e.g., README improvements, guides, examples)
- ✅ **Non-breaking additions** to test projects that provide immediate value

**What to Revert** (only when reviewer approves):
- ❌ **Exploratory code changes in core library/application code** (`/poc/DataFlow.POC/`, `/src/`)
- ❌ **Validation test files** (created purely for research validation, not ongoing value)
- ❌ **Benchmark code** (keep benchmark results documentation)
- ❌ **Prototype implementations in source tree** (copy to handover/prototype/ first if valuable)
- ❌ **Temporary helper code** (unless production-ready and valuable for tests)

**Key Distinction - Core Code vs Test/Doc Changes**:
- **Core library/application code** (in `/poc/DataFlow.POC/`, `/src/`) → Always revert after validation
- **Test code**: 
  - **Validation-only tests** → Revert (created for research, not ongoing value)
  - **Production-ready test utilities** → Keep if providing immediate value (e.g., test helpers that reduce boilerplate)
- **Documentation** → Keep if it improves the codebase (e.g., guides, ADRs, README improvements)

**Capturing Prototype Code for Handover**

If there is important reference code from your prototypes that would be valuable for the implementation team, capture it before reverting:

1. **Identify Key Prototype Code**: Select individual code files or code snippets that demonstrate critical patterns, algorithms, or approaches
2. **Copy to Handover Folder**: Copy these files to `/research/[topic]/handover/prototype/`
3. **Keep It Focused**: Only include files that provide clear reference value - don't copy entire projects
4. **Document Context**: Create a README in the prototype folder explaining:
   - What each prototype file demonstrates
   - Key patterns or approaches validated during research
   - Metrics achieved (performance, code reduction, etc.)
   - How implementation team should use these prototypes
5. **Reference in Handover Issue**: Point to `/research/[topic]/handover/prototype/` in your implementation issue

Example:
```bash
# Create prototype folder in handover
mkdir -p research/[topic]/handover/prototype/

# Copy key prototype files (not entire projects)
cp poc/DataFlow.POC.Tests/TestHelpers/*.cs research/[topic]/handover/prototype/TestHelpers/
cp poc/DataFlow.POC/Exploratory/CoordinatorPrototype.cs research/[topic]/handover/prototype/

# Create README explaining the prototypes
cat > research/[topic]/handover/prototype/README.md << 'EOF'
# Prototype Code

## TestHelpers/
Production-ready test utilities validated during research.
- 40-60% test code reduction achieved
- All tests passing with these helpers
- Ready for immediate adoption in test projects

## CoordinatorPrototype.cs
Reference implementation showing hybrid coordination approach.
- Demonstrates centralized fallback pattern
- 900+ ops/sec achieved in benchmarks
EOF
```

These prototype files serve as concrete reference implementations for the implementation team, showing proven approaches from your research.

**Referencing Prototypes in Handover Materials**:

In your implementation issue (in `/research/[topic]/handover/github-issue-*.md`), reference the prototype folder:

```markdown
## Implementation Resources

### Prototype Code
Production-ready prototypes available in: `/research/[topic]/handover/prototype/`

See prototype README for:
- Validated implementations ready for adoption
- Performance metrics achieved
- Usage guidance and patterns

### Production-Ready Artifacts
If research produced production-ready code (e.g., test helpers, utilities):
- **Location**: Documented in prototype folder README
- **Status**: Validated, all tests passing
- **Recommendation**: Can be adopted immediately by implementation team
```

**Reversion Process** (execute only after reviewer approval):
```bash
# 1. Commit all documentation and production-ready artifacts first
git add research/
git add poc/docs/  # ADRs and docs
# If keeping production-ready test utilities or documentation:
# git add poc/DataFlow.POC.Tests/[specific-production-ready-files]
git commit -m "Research documentation, handover materials, and production-ready artifacts"

# 2. Revert exploratory code changes in core library/application code
# For POC core code (always revert):
git checkout HEAD -- poc/DataFlow.POC/

# For validation tests (revert if not production-ready):
git checkout HEAD -- poc/DataFlow.POC.Tests/ValidationTests.cs
git checkout HEAD -- poc/DataFlow.POC.Tests/ExploratoryScenarios.cs

# For benchmarks (always revert, keep only documentation):
git checkout HEAD -- poc/DataFlow.POC.Benchmarks/

# For non-POC core code (if applicable, always revert):
git checkout HEAD -- src/Uniun.DataFlow/
# (adjust paths based on what was researched)

# 3. Verify only documentation and production-ready artifacts remain
git status
# Should show only changes in:
# - research/
# - poc/docs/ (ADRs, guides)
# - poc/DataFlow.POC.Tests/ (only production-ready test utilities, if kept)
```

**PR Review Checklist** (for reviewer before requesting reversion):
- [ ] Research documentation is comprehensive
- [ ] Implementation issue contains all necessary context
- [ ] Design docs and ADRs are complete
- [ ] Handover materials are ready for implementation team
- [ ] Prototype folder (if applicable) has README explaining code and metrics
- [ ] Test scenarios are documented (not implemented, unless production-ready utilities)
- [ ] Benchmark findings are documented (benchmark code removed)
- [ ] Clear distinction made between:
  - [ ] Core code changes (to be reverted)
  - [ ] Production-ready test utilities (can stay if valuable)
  - [ ] Documentation improvements (stay)

**After Reviewer Approval**:
- [ ] Important prototype code copied to handover folder (if applicable)
- [ ] Exploratory code changes in core library/application have been reverted
- [ ] Validation-only tests reverted
- [ ] Documentation and production-ready artifacts remain
- [ ] Handover issue references prototype folder for production-ready code
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
- ✅ `/research/distributed-epochs/handover/prototype/` (optional - key reference code files)
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

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/.team/workflows/RESEARCH_WORKFLOW.md`.

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

## Handover to Next Workflow

When research is complete, hand over to the appropriate next workflow using the workflow topology system.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Handover to Implementation

**When**: Research validates approach and creates implementation-ready specifications

```bash
gh issue edit $ISSUE \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE --body "🔬 **Handover: Research → Implementation**

Research validated approach. Ready for implementation.

**Research Deliverables**:
- Findings: \`/research/[topic]/README.md\`
- Design: \`/research/[topic]/design/[component].md\`
- Implementation issue: \`/research/[topic]/handover/github-issue-[feature].md\`
- Prototypes: \`/research/[topic]/handover/prototype/\`

**Next Steps**: Implement based on research specifications.

See: \`.team/workflows/IMPLEMENTATION_WORKFLOW.md\`"
```

**Or use the handover script**:
```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE research implementation "Research validated approach. See /research/[topic]/ for details."
```

### Handover to Product Prioritization

**When**: Research is complete but implementation needs prioritization

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE research product-backlog "Research complete, needs prioritization for implementation"
```

### Handover to Triage

**When**: Research shows approach is not feasible or requirements unclear

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE research triage "Approach not feasible, needs reassessment. See /research/[topic]/README.md for findings."
```

### Close Issue

**When**: Research shows solution is not viable and no further action needed

```bash
gh issue close $ISSUE --comment "✅ **Research Complete**

Research shows this approach is not viable.

**Findings**: [Summary of why not viable]
**Documentation**: \`/research/[topic]/README.md\`"
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

---

## Before PR Review: Self-Improvement Loop

**⚠️ REMINDER**: Before marking your PR ready for review, complete the self-improvement evaluation:

1. Reflect on the research workflow you followed
2. Document what worked well and what didn't
3. Propose specific, actionable improvements
4. Add your suggestions to `.github/workflow-improvements.md` in the Research Workflow section

This continuous feedback loop helps evolve our research processes based on real experiences. Your insights directly improve the workflow for future research work.
