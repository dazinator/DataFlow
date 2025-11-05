# Example: Using Instructions with GitHub Copilot

This document demonstrates how to use the research and implementation instructions when working on issues.

## Two Workflows

The repository supports two distinct workflows:

1. **Research Workflow**: Research validates approach, produces implementation-ready GitHub issue (code reverted at reviewer approval)
   - Use for: Researching any code (POC or production) where outcome is specification for implementation
   - Output: Comprehensive documentation + implementation-ready issue
   - Code reversion: Only when PR reviewer approves and requests it
   - See: `/research/RESEARCH_WORKFLOW.md`

2. **Implementation Workflow**: Implementation based on research handover or direct requirements
   - Use for: Implementing features in POC or production code based on validated designs
   - Output: Working code integrated into appropriate codebase
   - See: `.github/copilot-instructions.md` (Implementation Team Workflow section)

Choose based on issue type (see `.github/copilot-instructions.md` for guidance).

## Scenario 1: Research Workflow

Let's say you want to research a new architectural approach for any part of the codebase.

### Step 1: Create the GitHub Issue

Use the "Research Issue" template (`.github/ISSUE_TEMPLATE/research.md`) or:

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

## Scenario 2: Implementation Workflow

Let's say you want to implement a feature based on research team handover or direct requirements.

### Step 1: Create the GitHub Issue

Use the "Implementation Issue" template (`.github/ISSUE_TEMPLATE/implementation.md`):

```markdown
# Implement Broadcast Block Optimization

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

### Handover Document (if from research team)

**Handover Document Path**: `/research/broadcast-optimization/handover/github-issue-implement-broadcast-optimization.md`

The handover document contains:
- Complete problem context and background
- Implementation guidance and recommended approach
- Test scenarios and performance requirements
- Design references and supporting documentation

### Target Codebase

**Target**: [x] POC (`/poc`) [ ] Production (`/src`) [ ] Both

## Problem Statement

Research team validated approach for optimizing BroadcastBlock performance. See handover document for complete context.

## Implementation Checklist

Based on the handover document, the implementation should include:

- [ ] Optimized BroadcastBlock implementation
- [ ] Performance validation with benchmarks
- [ ] Tests for all scenarios in handover
- [ ] Edge cases handled
- [ ] Documentation updated

## Success Criteria

- [ ] All objectives from handover document met
- [ ] Tests passing
- [ ] Performance requirements met (10x improvement validated)
- [ ] Documentation updated
```

### Step 2: GitHub Copilot Reads Context

When assigned, Copilot will:

1. **Read main instructions** (`.github/copilot-instructions.md`)
2. **Identify as implementation issue** (sees "⚠️ This is an implementation issue")
3. **Read handover document** (from `/research/broadcast-optimization/handover/...`)
4. **Determine target codebase** (POC in this case)
5. **Load POC context** (reads `/poc/README.md`, glossary, etc.)

### Step 3: Copilot Follows Implementation Workflow

Based on instructions, Copilot would:

1. **Read Handover Document** completely:
   - Problem context
   - Implementation guidance
   - Test scenarios
   - Performance requirements
   - Design references

2. **Read Referenced Documentation**:
   - Research findings
   - Design documents
   - ADRs
   - Prototype code (if available)

3. **Create Plan** (for POC target - in `/poc/docs/plans/implement-broadcast-optimization.md`):
   ```markdown
   # Plan: Implement Broadcast Block Optimization
   
   ## Objective
   Implement optimized BroadcastBlock based on research findings
   
   ## Handover Reference
   `/research/broadcast-optimization/handover/github-issue-implement-broadcast-optimization.md`
   
   ## Approach
   As per handover document: Use shared buffer pattern with lock-free reads
   
   ## Milestones
   - [ ] Core optimization implemented
     - Validation: Basic tests pass
   - [ ] All test scenarios from handover implemented
     - Validation: Test suite passes
   - [ ] Performance validated
     - Validation: Benchmarks meet 10x target
   - [ ] Documentation updated
     - Validation: Glossary and guides current
   ```

4. **Implement** following handover guidance
5. **Test** using scenarios from handover document
6. **Validate performance** against requirements
7. **Update documentation** (glossary, guides as needed)

### Step 4: Direct Implementation (No Research Handover)

For implementation without research handover, the issue would contain full requirements:

```markdown
# Fix Epoch Vector Comparison Bug

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Follow implementation guidelines.

### Target Codebase

**Target**: [x] POC (`/poc`) [ ] Production (`/src`)

## Problem Statement

Found a bug in epoch vector comparison logic. When comparing vectors with 
different source sets, the comparison is incorrect.

Expected: Should handle partial ordering correctly
Actual: Throws exception

## Requirements

1. Fix comparison logic to handle partial ordering
2. Add tests for edge cases
3. Update glossary if comparison semantics change

## Success Criteria

- [ ] Bug fixed with tests
- [ ] All edge cases covered
- [ ] Documentation updated if needed
```

## What Makes This Work

The system works because:

1. **Automatic Loading**: GitHub Copilot loads `.github/copilot-instructions.md` automatically
2. **Clear References**: Issues explicitly reference workflow type (research vs implementation)
3. **Comprehensive Guidelines**: Instructions provide complete workflow for each type
4. **Templates Available**: Easy-to-use templates reduce friction
5. **Examples Provided**: Clear examples show expected behavior

## Tips for Success

1. **Be Explicit**: Always mention workflow type (research vs implementation) in issue
2. **Link Handover**: For implementation from research, link handover document
3. **Specify Target**: Clearly state target codebase (POC vs production)
4. **List Deliverables**: Clear checklist helps Copilot plan the work
5. **Reference Docs**: Point to specific docs that are relevant

## See Also

- **Research Workflow**: `/research/RESEARCH_WORKFLOW.md` - Complete research workflow
- **Implementation Workflow**: See `.github/copilot-instructions.md` (Implementation Team Workflow section)
- **Issue Templates**: 
  - `.github/ISSUE_TEMPLATE/research.md` - Research workflow template
  - `.github/ISSUE_TEMPLATE/implementation.md` - Implementation workflow template
- **Implementation Issue Template**: `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md` - Template for creating handoff issues
- **GitHub Folder README**: `.github/README.md` - Overview of GitHub config files
- **POC Structure**: `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md` - POC documentation organization
