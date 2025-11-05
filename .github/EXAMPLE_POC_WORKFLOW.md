# Example: Using Instructions with GitHub Copilot

This document demonstrates how to use the research and POC instructions when working on issues.

## Two Workflows

The repository supports two distinct workflows:

1. **Research-to-Implementation**: Research validates approach, produces implementation-ready GitHub issue (code reverted at reviewer approval)
   - Use for: Researching any code (POC or production) where outcome is specification for implementation
   - Output: Comprehensive documentation + implementation-ready issue
   - Code reversion: Only when PR reviewer approves and requests it
   - See: `/research/RESEARCH_WORKFLOW.md`

2. **Direct Integration**: Research and implementation together, code integrated into codebase
   - Use for: POC-contained changes, iterative refinement
   - Output: Code + documentation in POC

Choose based on issue type (see `.github/copilot-instructions-poc.md` for guidance).

## Scenario 1: Research-to-Implementation Workflow

Let's say you want to research a new architectural approach for any part of the codebase.

### Step 1: Create the GitHub Issue

Use the "Research Issue" template or:

```markdown
# Research: Distributed Transaction Coordination

## Research Context

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/research/RESEARCH_WORKFLOW.md`.

**Research Objective**: Validate approaches for coordinating transactions across distributed DataFlow nodes

**Target Codebase**: [POC / Production / Both]

**Key Requirements**:
- Create research folder structure in `/research/distributed-transactions/`
- Freely prototype with code (POC or production) to validate approaches
- Document findings in `/research/distributed-transactions/`
- Create implementation-ready issue using template
- **Code reversion happens only when PR reviewer approves and requests it**

**Expected Deliverables**:
- [ ] Research folder with plan, documentation, and benchmarks
- [ ] Supporting design docs and ADRs in `/research/[topic]/`
- [ ] Implementation-ready issue in handover folder
- [ ] After reviewer approval: Code changes reverted, only docs remain

## Research Questions

1. Can we use 2PC for transaction coordination?
2. What is the performance overhead?
3. How do we handle network partitions?
```

### Step 2: GitHub Copilot Executes Research

Copilot will:
1. Create research folder structure in `/research/distributed-transactions/`
2. Create research plan in `/research/distributed-transactions/research-plan.md`
3. Write prototype code (POC or production) to validate approaches
4. Run benchmarks and tests to measure performance
5. Document findings in `/research/distributed-transactions/README.md`
6. Save benchmark data in `/research/distributed-transactions/benchmarks/`
7. Create supporting docs (design, ADRs) in `/research/distributed-transactions/design/` and `/research/distributed-transactions/adr/`
8. Create implementation-ready issue in `/research/distributed-transactions/handover/github-issue-implement-distributed-transactions.md`
9. Submit PR with research documentation AND exploratory code

### Step 3: Research PR Review

The PR reviewer evaluates:
- Research findings quality and completeness
- Documentation comprehensiveness
- Handover materials readiness

If approved, reviewer explicitly requests code reversion.

The PR then contains only documentation:
- Research findings with comparative analysis
- Benchmark data
- Design documentation and ADRs
- ADRs for decisions made
- Implementation-ready issue with complete context
- NO code changes (all reverted)

### Step 4: Implementation Handoff

Engineering team receives:
- Implementation issue in `/research/distributed-transactions/handover/github-issue-implement-distributed-transactions.md`
- All research documentation in `/research/distributed-transactions/`
- Design docs and ADRs to copy as needed to target codebase folders
- Test scenarios to implement
- Performance targets from benchmarks
- Clear guidance on approach

They can implement in appropriate codebase (POC or production `src/`) following the issue.

## Scenario 2: Direct Integration Workflow

Let's say you want to implement a new feature directly in the POC. Here's how you would set up the GitHub issue to ensure Copilot follows the POC workflow.

### Step 1: Create the GitHub Issue

Use the "POC Implementation Issue" template or from `POC_ISSUE_TEMPLATE.md`:

```markdown
# Implement Broadcast Block Performance Optimization

## POC Context

⚠️ This is a POC issue. @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`.

**Key Requirements**:
- Read `/poc/README.md` and broadcast-related documentation in `/poc/docs/design/`
- Review `/poc/docs/POC_GLOSSARY.md` for relevant terminology
- Create a plan in `/poc/docs/plans/optimize-broadcast-performance.md`
- Document research and benchmark results in `/research/`
- Update glossary if new concepts are introduced

## Problem Statement

The current BroadcastBlock implementation may have performance issues when broadcasting
to many downstream consumers. We need to investigate and optimize if necessary.

## Requirements

1. Benchmark current broadcast performance with varying numbers of consumers (1, 5, 10, 50)
2. Identify bottlenecks
3. Propose and implement optimizations
4. Validate improvements with benchmarks

## Expected Deliverables

- [ ] Plan document in `/poc/docs/plans/optimize-broadcast-performance.md`
- [ ] Baseline benchmark results in `/research/broadcast-baseline-benchmark.md`
- [ ] Investigation findings in `/research/broadcast-performance-investigation.md`
- [ ] Optimized implementation with tests
- [ ] Post-optimization benchmark in `/research/broadcast-optimization-results.md`
- [ ] ADR if significant design changes (in `/poc/docs/adr/`)

## Success Criteria

- Benchmark shows measurable improvement
- No regression in correctness tests
- Documentation is complete
```

### Step 2: GitHub Copilot Reads the Instructions

When you assign this issue to GitHub Copilot (or start working on it with Copilot):

1. Copilot automatically loads `.github/copilot-instructions.md`
2. Copilot sees the reference to `.github/copilot-instructions-poc.md` and loads POC guidelines
3. Copilot understands the POC documentation structure and workflow

### Step 3: Copilot Follows the POC Workflow

Based on the instructions, Copilot would:

1. **Read Context**:
   - `/poc/README.md` for POC overview
   - `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md` for structure
   - `/poc/docs/POC_GLOSSARY.md` for terminology
   - Relevant design docs about broadcast blocks

2. **Create Plan** (`/poc/docs/plans/optimize-broadcast-performance.md`):
   ```markdown
   # Plan: Optimize Broadcast Block Performance
   
   ## Objective
   Investigate and optimize BroadcastBlock performance for many consumers
   
   ## Approach
   1. Establish baseline with benchmarks (1, 5, 10, 50 consumers)
   2. Profile to identify bottlenecks
   3. Implement optimizations
   4. Validate with post-optimization benchmarks
   
   ## Key Assumptions
   - Current implementation is channel-based
   - Performance degrades linearly with consumer count
   
   ## Alternative Approaches
   - Option A: Parallel broadcast (trade CPU for latency)
   - Option B: Batched notification (trade latency for throughput)
   - Option C: Shared buffer (trade memory for speed)
   
   ## Milestones
   - [ ] Baseline benchmarks completed
     - Validation: Results documented in /research/
   - [ ] Bottleneck identified
     - Validation: Investigation doc with profiling results
   - [ ] Optimization implemented
     - Validation: All tests pass
   - [ ] Performance validated
     - Validation: Post-opt benchmark shows improvement
   
   ## Testing Strategy
   - Correctness: All existing tests must pass
   - Performance: Benchmark with 1, 5, 10, 50 consumers
   - Load: Test with high message throughput
   ```

3. **Create Baseline Benchmark** (in `DataFlow.POC.Benchmarks`):
   - Implement benchmark code
   - Run benchmarks
   - Document results in `/research/broadcast-baseline-benchmark.md`

4. **Investigate** and document findings in `/research/broadcast-performance-investigation.md`

5. **Implement** optimizations with tests

6. **Validate** with post-optimization benchmarks

7. **Document** the approach (possibly create an ADR if significant decision)

8. **Update** glossary if new terms introduced

### Step 4: Using Comments to Redirect Copilot

If Copilot starts working on the issue but doesn't follow POC guidelines, add a comment:

```markdown
@copilot This is a POC issue - please follow the workflow in `.github/copilot-instructions-poc.md`:
1. Create a plan document first
2. Run baseline benchmarks and document results
3. Update the plan with findings
```

## Alternative: Minimal Issue Format

For simpler POC work, you can use a minimal format:

```markdown
# Fix Epoch Vector Comparison Bug

⚠️ **POC Issue**: @copilot Follow POC guidelines (`.github/copilot-instructions-poc.md`)

Found a bug in epoch vector comparison logic. When comparing vectors with 
different source sets, the comparison is incorrect.

Expected: Should handle partial ordering correctly
Actual: Throws exception

**Deliverables**:
- [ ] Plan in `/poc/docs/plans/` (minimal plan acceptable for bug fixes)
- [ ] Fix with tests
- [ ] Update glossary if comparison semantics change
```

## What Makes This Work

The system works because:

1. **Automatic Loading**: GitHub Copilot loads `.github/copilot-instructions.md` automatically
2. **Clear References**: Issues explicitly reference the POC instructions file
3. **Comprehensive Guidelines**: POC instructions provide complete workflow
4. **Templates Available**: Easy-to-use templates reduce friction
5. **Examples Provided**: Clear examples show expected behavior

## Tips for Success

1. **Be Explicit**: Always mention it's a POC issue and reference the instructions
2. **List Deliverables**: Clear checklist helps Copilot plan the work
3. **Reference Docs**: Point to specific POC docs that are relevant
4. **Set Expectations**: Mention if benchmarking, research, or ADRs are needed
5. **Follow Up**: Use comments if Copilot deviates from POC workflow

## See Also

- **Research Workflow**: `/research/RESEARCH_WORKFLOW.md` - Complete research-to-implementation workflow
- **POC Guidelines**: `.github/copilot-instructions-poc.md` - Complete POC workflow for both approaches
- **Issue Templates**: 
  - `.github/ISSUE_TEMPLATE/poc-research.md` - Research-to-implementation template
  - `.github/ISSUE_TEMPLATE/poc-implementation.md` - Direct integration template
  - `.github/POC_ISSUE_TEMPLATE.md` - Detailed template documentation
- **Implementation Issue Template**: `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md` - Template for creating handoff issues
- **Implementation Issue Example**: `/poc/docs/plans/IMPLEMENTATION_ISSUE_EXAMPLE.md` - Complete example
- **GitHub Folder README**: `.github/README.md` - Overview of GitHub config files
- **POC Structure**: `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md` - POC documentation organization
