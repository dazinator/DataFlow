# Research and Exploration Workflow

---

## ⚠️ CRITICAL: This is NOT Direct Implementation

**If you're a Copilot agent working on a research issue:**

### Required Reading (Before Starting)

**📖 Read these guides before tackling any research work:**

**Workflow System** (Global - ALL workflows):
- [Workflow Topology Guide](../../.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) - Querying issues, handover patterns, workflow states

**Coding Standards & Patterns** (Required for POC/exploratory code):
- [Getting Started Guide](../../.team/GETTING_STARTED.md) - C# style, async/await patterns, testing standards, common patterns

**Package Management** (When exploring dependencies):
- [Central Package Management Guide](../../.team/CENTRAL_PACKAGE_MANAGEMENT.md) - Adding new NuGet packages, resolving version conflicts
- [NuGet Dependency Updates Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md) - Updating packages, security fixes

---

### DO (During Research):
- ✅ Create `/research/[topic]/` for temporary research work (findings, experiments, prototypes)
- ✅ **Create formal documentation** (analysis, design, ADRs) - see [Documentation Standards](/.github/copilot-instructions.md#documentation-standards)
- ✅ Write exploratory code in `/poc/` or `/src/` to validate approaches
- ✅ Create tests to validate concepts
- ✅ Run benchmarks to measure performance
- ✅ Document everything you try and learn

### DO (Before PR Merge - After Reviewer Approval):
- ✅ Save important prototype code to `/research/[topic]/handover/prototype/`
- ✅ Create implementation-ready issue in `/research/[topic]/handover/`
- ✅ **REVERT all exploratory code changes** from `/poc/` and `/src/`
- ✅ Keep temporary research artifacts in `/research/[topic]/`
- ✅ **Keep formal documentation** (analysis docs, design docs, ADRs) - these are NOT reverted
- ✅ **Complete self-improvement evaluation** (see below)

### DO (Self-Improvement Loop - Required Before PR Review):
- ✅ **Evaluate research workflow effectiveness** for this task
- ✅ **Document what worked well** and what didn't in the research process
- ✅ **Propose specific improvements** to the research workflow documentation
- ✅ **Add feedback comment** to the `[Workflow Feedback] Tracker` issue (see section below)

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

**⚠️ Comment Prefix Convention:**
- Prefix ALL comments with `[Copilot-Workflow: Research]` to confirm you're following this workflow
- Example: `[Copilot-Workflow: Research] I've validated the approach and documented findings in /research/...`

## Workflow Queue

**Query issues designated to this workflow:**

**For Copilot Agents** (use MCP tools):
```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:research"],
    state="OPEN"
)
```

**For Manual/CI Use** (GitHub CLI):
```bash
gh issue list \
  --label "workflow:research" \
  --state open \
  --json number,title,url
```

**Entry Points:**
- From Triage workflow (needs validation)
- From Tech Debt workflow (needs research)
- From Implementation workflow (uncovered unknowns)

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

---

## Label Cleanup on Entry

**⚠️ IMPORTANT**: Before starting research work, check for and clean up conflicting workflow labels.

### Workflow Label Validation

When you start work on an issue in this workflow:

1. **Check the issue's labels** for any workflow labels
2. **Identify conflicts**: If the issue has MULTIPLE workflow labels (e.g., both `workflow:research` AND `workflow:triage`)
3. **Determine correct label**: 
   - If you were assigned to this issue via the research queue, `workflow:research` is correct
   - If the issue has another workflow label in addition to `workflow:research`, that's label pollution
4. **Remove conflicting labels**: Remove any workflow label that is NOT `workflow:research`
5. **Add cleanup comment** noting what was corrected

### Label Cleanup Example

**For Copilot Agents** (use MCP tools):

```python
# Example: Issue has both workflow:research and workflow:triage labels
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    labels=["workflow:research"]  # Only keep the correct label
)

add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    body="[Copilot-Workflow: Research] 🏷️ Label cleanup: Removed conflicting `workflow:triage` label. This issue is correctly in the research workflow."
)
```

**Why this matters**: Issues should have exactly ONE workflow label at a time. Multiple labels create confusion about which workflow owns the issue.

**When to skip**: If the issue only has `workflow:research` label (no conflicts), proceed directly to research work.

---

## Multi-Phase Issue Check

**⚠️ ALWAYS**: Check if this issue is part of a multi-phase plan before starting research.

### Quick Check

```python
# Get current issue details
issue = issue_read(
    method="get",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=CURRENT_ISSUE_NUMBER
)

# Check if parent exists
if issue.parent:
    # This is a sub-issue - read parent context
    parent = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue.parent.number
    )
    # Review parent to understand overall research plan
else:
    # Standalone research - proceed with normal workflow
```

### If This is a Sub-Issue

**DO:**
1. ✅ Read parent issue to understand overall research plan
2. ✅ Note which research phase this represents
3. ✅ Review completed phases for context and findings
4. ✅ Update parent description as research progresses
5. ✅ Check if this is the last sub-issue before creating PR

**Parent Update Pattern:**

When documenting findings:
```python
# Update parent issue description to reflect research status
# Example: Change "Phase 2 - Exploration" to "Phase 2 - Validation complete, handover ready"
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number,
    body=updated_description
)
```

**Closing Parent (Last Sub-Issue Only):**

If this is the last open sub-issue of the research plan, include parent in PR description:
```markdown
Fixes #CURRENT_ISSUE
Fixes #PARENT_ISSUE
```

This ensures both issues close when PR merges.

**Research Handover Context:**

When creating implementation handover, include reference to parent issue to provide full context of the multi-phase research effort.

**See:** [Multi-Phase Issue Procedures](/.team/MULTI_PHASE_ISSUES.md) for complete guidance including examples and troubleshooting.

---

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

### Documentation: Research vs Formal Documentation

**⚠️ Important Distinction**:

The repository has TWO documentation systems that serve different purposes:

1. **Temporary Research Work** (`/research/[topic]/`):
   - Working notes, experiments, prototypes
   - Temporary investigation artifacts
   - Research-specific findings
   - Gets created and potentially archived after research completes

2. **Formal Documentation** (`/docs/`):
   - **Analysis** (`/docs/analysis/`) - Formal investigations, benchmarks, studies
   - **Design** (`/docs/design/`) - Solution proposals and plans
   - **ADRs** (`/docs/adr/`) - Architectural decisions
   - Permanent documentation supporting GitHub issues
   - NOT reverted - stays in the repository

**When to use which**:
- Use `/research/[topic]/` for **temporary research work** specific to this exploration
- Use `/docs/` for **formal documentation** that supports long-term understanding

**📖 See**: [Documentation Standards](/.github/copilot-instructions.md#documentation-standards) for complete guidance on creating formal documentation (analysis, design, ADRs).

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
- **Formal documentation** (analysis, design, ADRs) - see [Documentation Standards](/.github/copilot-instructions.md#documentation-standards)
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

#### Approach Analysis Guidance

When evaluating multiple approaches during research, use one of these documentation patterns:

**Option A: Separate Documents + Comparison Matrix** (Recommended for 3+ approaches)
- Create separate document for each approach in `/research/[topic]/design/`
  - `approach-A-[name].md` - Detailed analysis of approach A
  - `approach-B-[name].md` - Detailed analysis of approach B  
  - `approach-C-[name].md` - Detailed analysis of approach C
- Create comparison matrix in main README or separate `comparison.md`:

```markdown
| Criterion | Approach A | Approach B | Approach C |
|-----------|-----------|-----------|-----------|
| Performance | Fast (10ms) | Slow (100ms) | Medium (30ms) |
| Complexity | Low | High | Medium |
| Maintainability | High | Low | Medium |
| **Recommendation** | ✅ **Recommended** | ❌ Not viable | ⚠️ Fallback option |
```

**Benefits**:
- Thorough analysis of each approach
- Easy to compare side-by-side
- Works well for complex evaluations
- Clear recommendation emerges from data

**Option B: Single Comparison Document** (Recommended for 2 approaches)
- Create one document with embedded analysis: `approach-comparison.md`
- Include sections for each approach with inline comparison

```markdown
# Approach Comparison: [Topic]

## Approach A: [Name]
[Detailed analysis]

**Pros**: ...
**Cons**: ...

## Approach B: [Name]
[Detailed analysis]

**Pros**: ...
**Cons**: ...

## Recommendation
Based on analysis: **Approach A** because...
```

**Benefits**:
- Simpler for binary choices
- Less overhead for straightforward comparisons
- Easier to read linearly

**Choosing the Right Pattern**:
- **3+ approaches** → Option A (separate docs + matrix)
- **2 approaches** → Option B (single comparison doc)
- **When in doubt** → Start with Option A, can always consolidate later

#### Prototyping Scope Guidance

Match prototype scope to your research questions. Choose the appropriate level based on what you need to validate:

**1. Minimal POC (Proof of Concept)**
- **Purpose**: Feasibility validation - "Is this even possible?"
- **Scope**: Bare minimum code to prove approach works
- **Time**: Hours to 1-2 days
- **Example**: Quick script showing API integration works
- **When to use**:
  - Evaluating technical feasibility
  - Testing if library/framework supports needed features
  - Validating architectural assumption

**2. Working Prototype**
- **Purpose**: Performance and integration validation - "Is it fast enough? Does it integrate well?"
- **Scope**: Functional implementation with realistic data/scenarios
- **Time**: 2-5 days
- **Example**: Functional component with benchmarks and integration tests
- **When to use**:
  - Performance is a key concern
  - Testing integration with existing systems
  - Validating end-to-end workflows
  - Need concrete data for decision-making

**3. Production-Ready**
- **Purpose**: Adoption validation - "Can users actually use this?"
- **Scope**: Polished, documented, tested implementation
- **Time**: 1-2 weeks
- **Example**: Complete feature with error handling, tests, documentation
- **When to use**:
  - Research validates that implementation can proceed immediately
  - Code can be kept (not reverted) and merged directly
  - Provides immediate value (e.g., test helpers, utilities)

**Decision Framework**:

| Research Question | Prototype Scope |
|-------------------|----------------|
| "Can we do X with library Y?" | Minimal POC |
| "Is approach A faster than B?" | Working Prototype |
| "Should we add this test helper?" | Production-Ready |
| "How complex is integration?" | Working Prototype |
| "Is this technically feasible?" | Minimal POC |

**Default**: Start with **Minimal POC**. Upgrade to Working or Production-Ready only if research questions require it. Avoid over-engineering prototypes that will be reverted.

#### Benchmark Documentation Template

When performance validation is part of research, document benchmarks systematically:

**Template** (save in `/research/[topic]/benchmarks/[scenario-name].md`):

```markdown
# Benchmark: [Scenario Name]

## Objective
[What are you measuring? Why does it matter?]

## Test Environment
- **Machine**: [CPU, RAM, OS details]
- **Runtime**: [.NET version, JVM version, etc.]
- **Test Date**: YYYY-MM-DD
- **Conditions**: [Warm/cold start, concurrent load, etc.]

## Patterns Tested

### Pattern A: [Name]
```[language]
// Code snippet showing what was tested
```

### Pattern B: [Name]
```[language]
// Code snippet showing what was tested
```

## Results

| Pattern | Metric 1 | Metric 2 | Metric 3 |
|---------|----------|----------|----------|
| Pattern A | 15ms | 1.2MB | 95% |
| Pattern B | 45ms | 0.8MB | 87% |

## Analysis
[Interpretation of results. What do the numbers mean? Which pattern wins? Under what conditions?]

**Winner**: Pattern A because...

**Trade-offs**: Pattern A uses more memory but is 3x faster...

## Assessment
- ✅ **Recommendation**: Use Pattern A for high-throughput scenarios
- ⚠️ **Caveat**: Pattern B may be better for memory-constrained environments
- 📊 **Data Quality**: High confidence (1000 iterations, <5% variance)

## References
- Code: `/research/[topic]/handover/prototype/benchmark-code.cs`
- Raw data: `results.csv`
```

**Benchmark Types**:
- **Microbenchmarks**: Isolated operations (use BenchmarkDotNet for .NET)
- **Integration benchmarks**: End-to-end workflows with realistic data
- **Stress tests**: Performance under load/concurrency
- **Memory profiling**: Allocation patterns and GC pressure

**When to Skip Benchmarks**:
- ❌ Not performance-sensitive (developer tools, configuration, etc.)
- ❌ Performance is obviously acceptable (milliseconds for rare operations)
- ❌ Prototype is Minimal POC only (feasibility, not performance)

**When Benchmarks Are Required**:
- ✅ Core library performance (data processing, algorithms)
- ✅ Comparing multiple approaches where performance differs
- ✅ Validating performance requirements (e.g., "must process 10k items/sec")

#### Hybrid Approach Analysis Pattern

When research reveals that combining multiple approaches works best, document the hybrid strategy:

**Hybrid Approach Template**:

```markdown
# Hybrid Approach: [Name]

## Strategy Overview
[Brief explanation of how approaches are combined]

Example: "Use simple polling (Approach A) for low-frequency scenarios (<10/min) and event-driven coordination (Approach B) for high-frequency scenarios (>100/min)."

## Component Approaches

### Approach A: [Name] - [When Used]
- **Use case**: [Specific conditions]
- **Benefits**: [Why it's good for this case]
- **Limitations**: [Why it doesn't work for all cases]

### Approach B: [Name] - [When Used]
- **Use case**: [Specific conditions]
- **Benefits**: [Why it's good for this case]
- **Limitations**: [Why it doesn't work for all cases]

## Decision Criteria

**When to use Approach A**:
- Condition 1: [e.g., load < threshold]
- Condition 2: [e.g., simplicity required]

**When to use Approach B**:
- Condition 1: [e.g., load > threshold]
- Condition 2: [e.g., performance critical]

**How to decide**: [Runtime decision logic or configuration-based selection]

## Phased Adoption (If Applicable)

**Phase 1: Foundation (Simple)**
- Implement Approach A only
- Covers 80% of use cases
- Lower risk, faster delivery
- **Deliverable**: Working solution for common scenarios

**Phase 2: Optimization (Advanced)**
- Add Approach B for high-performance cases
- Covers remaining 20% edge cases
- Higher complexity but necessary for scale
- **Deliverable**: Full hybrid implementation

**Phase Transition Criteria**:
- Move to Phase 2 when: [e.g., "user reports performance issues" or "load exceeds 100/min"]
- Can operate indefinitely on Phase 1 if: [e.g., "performance requirements met"]

## Implementation Complexity

| Aspect | Approach A Only | Approach B Only | Hybrid |
|--------|----------------|----------------|--------|
| Lines of Code | 200 | 500 | 400 |
| Test Complexity | Low | High | Medium |
| Maintenance | Easy | Hard | Medium |
| Performance | Good | Excellent | Excellent |

**Recommendation**: [Hybrid | Phase 1 Only | Phase 2 after validation]

**Rationale**: [Why hybrid is worth the added complexity]

## References
- Approach A prototype: `/research/[topic]/handover/prototype/approach-a/`
- Approach B prototype: `/research/[topic]/handover/prototype/approach-b/`
- Benchmark comparison: `/research/[topic]/benchmarks/hybrid-comparison.md`
```

**When to Use Hybrid**:
- ✅ Different approaches excel in different scenarios
- ✅ Single approach has unacceptable trade-offs
- ✅ Phased adoption reduces risk
- ✅ Runtime conditions can inform approach selection

**When to Avoid Hybrid**:
- ❌ Adds complexity without clear benefit
- ❌ Single approach meets all requirements
- ❌ Decision criteria are unclear or hard to determine

### Phase 5: Create Product Backlog Item

The primary deliverable: a comprehensive product backlog item that enables implementation team to work from the product backlog system.

**See `/product/README.md` for complete product backlog system documentation.**

#### Creating the Backlog Issue

**1. Create GitHub issue with `workflow:product-backlog` label**:

Using MCP tools (for Copilot agents):

```python
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="[Research] [Title - brief description]",
    body="""## Summary
[Brief 1-2 sentence description]

## Context
[Background from research - why this work is needed]
- Research folder: `/research/[topic]/`
- Key findings summary

## Implementation Guidance
[High-level guidance based on research]
- Recommended approach from research
- Key requirements
- Design considerations

### External Dependencies (if applicable)
**IMPORTANT**: If research involves sample code or implementations requiring external services:
- List each external dependency with connection details
- State whether runtime validation required or build-only sufficient
- Provide alternative validation approaches if service optional

## Success Criteria
- [ ] Objectives from research
- [ ] Performance requirements (if applicable)
- [ ] Test coverage specified

## Handover Assets
- **Location**: `/research/[topic]/handover/`
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
""",
    labels=["workflow:product-backlog", "research"]
)
```

**2. Reference handover assets** in research folder (not separate backlog folder):

```bash
# Handover assets stay in research folder
ls /research/[topic]/handover/prototype/
ls /research/[topic]/handover/design/
ls /research/[topic]/handover/benchmarks/
```

#### Creating Handover Folder (if needed)

If you have supporting assets (prototype code, design docs, benchmarks):

**1. Create handover folder within research directory**:
```bash
mkdir -p research/[topic]/handover/{prototype,design,benchmarks}
```

**2. Copy assets to handover folder within research directory**:
```bash
# Example: Copy prototype code
cp poc/DataFlow.POC/FlowComposability.cs \
   research/[topic]/handover/prototype/

# Example: Copy design documents
cp research/[topic]/design/api-design.md \
   research/[topic]/handover/design/

# Example: Copy benchmarks
cp research/[topic]/benchmarks/performance-comparison.md \
   research/[topic]/handover/benchmarks/

# Create README in handover folder
cat > research/[topic]/handover/prototype/README.md << 'EOF'

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
# Create handover folder within research
mkdir -p research/flow-composability/handover/prototype

# Copy key prototype files
cp research/flow-composability/prototype/*.cs \
   research/flow-composability/handover/prototype/

# Create README for prototypes
cat > research/flow-composability/handover/prototype/README.md << 'EOF'
# Prototype Code

## UnifiedFlowBuilder.cs
Reference implementation showing recommended API design.
- Demonstrates fluent builder pattern
- Validates type safety approach
- Performance: 1000+ ops/sec in benchmarks
EOF
```

**Note**: All handover assets remain in `/research/[topic]/handover/`. Reference this path in the GitHub issue body.

#### Notify Product Team

**1. GitHub issue is created with `workflow:product-backlog` label**

The issue contains:
- Implementation guidance from research
- Success criteria
- Reference to handover assets in `/research/[topic]/handover/`

**2. Product team will**:
- Review backlog issue
- Prioritize using Product Prioritization workflow
- Implementation team will select based on priorities

### Phase 6: Create Implementation-Ready GitHub Issue (DEPRECATED)

⚠️ **This phase is deprecated**. Use Phase 5 (Create Product Backlog Issue) instead.

**Old workflow**: Created handover issues in `/research/[topic]/handover/github-issue-*.md`
**New workflow**: Create GitHub issues with `workflow:product-backlog` label

**Supporting artifacts**: If research produces designs, diagrams, or other assets, store them in `/research/[topic]/` and reference the path in the GitHub issue description.

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
- ✅ **Product backlog GitHub issue** created with `workflow:product-backlog` label
- ✅ **Handover assets** in `/research/[topic]/handover/` (prototype code, designs, benchmarks)
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

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/.team/prompts/RESEARCH_WORKFLOW.md`.

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

See: \`.team/prompts/IMPLEMENTATION_WORKFLOW.md\`"
```

**Or use the handover script**:
```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE research implementation "Research validated approach. See /research/[topic]/ for details."
```

### Handover to Product Prioritization

**When**: Research is complete but implementation needs prioritization

```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE research product-backlog "Research complete, needs prioritization for implementation"
```

### Handover to Triage

**When**: Research shows approach is not feasible or requirements unclear

```bash
./.github/scripts/workflow/handover-issue.sh \
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
4. Create a feedback issue (see instructions below)

This continuous feedback loop helps evolve our research processes based on real experiences. Your insights directly improve the workflow for future research work.

### Creating Feedback Comments

**Find the tracker:**
```python
from datetime import datetime

# Search for the feedback tracker issue (create if not found)
results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

if not results or len(results) == 0:
    # Create new tracker if none exists
    tracker = issue_write(
        method="create",
        owner="uniun-technology",
        repo="lib-dataflow",
        title="[Workflow Feedback] Tracker",
        labels=["workflow:process-modeling"],
        body="""# Workflow Feedback Tracker

This issue tracks feedback and improvement suggestions for all workflows.

**⚠️ IMPORTANT**: Feedback is submitted as **comments on this issue**, not as sub-issues.

## How to Submit Feedback

When completing work on an issue:

1. **Find this tracker issue**: Search for `[Workflow Feedback] Tracker`
2. **Add a comment** with your feedback using the template below

### Feedback Comment Template

\```markdown
## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #XXX or branch-name
**Workflow**: [Workflow Name]

### What Worked Well
[List specific positives]

### What Didn't Work Well
[List specific issues]

### Suggested Improvement
[Specific, actionable improvements]
\```

## For Process Modeling Workflow

When assigned to address feedback:

1. Read through recent feedback comments on this issue
2. Group related feedback
3. Address improvements using standard Process Modeling workflow
4. Mark feedback as addressed by adding a reply comment
"""
    )
    tracker_number = tracker.number
else:
    tracker_number = results[0].number
```

**Add feedback comment:**
```python
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=tracker_number,
    body=f"""## Workflow Feedback Entry

**Date**: {datetime.now().strftime("%Y-%m-%d")}
**Issue/PR**: #XXX - Brief description
**Workflow**: Research Workflow

### What Worked Well

- [List things that worked well]

### What Didn't Work Well

- [List pain points or confusion]

### Suggested Improvement

[Specific, actionable improvement with rationale]
"""
)
```
