# Triage Decision Examples

This document provides real-world examples of triage decisions to help agents understand how to route issues to appropriate workflows.

## Table of Contents

- [How to Use This Guide](#how-to-use-this-guide)
- [Example 1: Feature Request with Clear Requirements](#example-1-feature-request-with-clear-requirements)
- [Example 2: Performance Issue Requiring Investigation](#example-2-performance-issue-requiring-investigation)
- [Example 3: Code Quality Improvement](#example-3-code-quality-improvement)
- [Example 4: Multiple Feature Requests Needing Prioritization](#example-4-multiple-feature-requests-needing-prioritization)
- [Example 5: Workflow Process Improvement](#example-5-workflow-process-improvement)
- [Example 6: Bug with Unclear Reproduction Steps](#example-6-bug-with-unclear-reproduction-steps)
- [Example 7: Duplicate Issue](#example-7-duplicate-issue)
- [Example 8: Architecture Change Requiring Validation](#example-8-architecture-change-requiring-validation)
- [Example 9: Security Vulnerability](#example-9-security-vulnerability)
- [Example 10: Feature Request Needing Design Exploration](#example-10-feature-request-needing-design-exploration)
- [Quick Reference Decision Matrix](#quick-reference-decision-matrix)
- [Common Triage Patterns](#common-triage-patterns)
- [Edge Cases](#edge-cases)
- [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
- [Summary](#summary)

---

## How to Use This Guide

1. **Read the scenario** - Understand the issue context
2. **Consider the decision factors** - Think through clarity, complexity, unknowns
3. **See the triage decision** - Learn which workflow was chosen and why
4. **Review the rationale** - Understand the reasoning

### Quick Reference Decision Matrix

For rapid lookup, use this matrix to match issue characteristics to workflows:

| Factor | Research | Implementation | Tech Debt | Product Backlog | Process Modeling | Close |
|--------|----------|----------------|-----------|-----------------|------------------|-------|
| **Clear Requirements** | May lack specifics | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | N/A |
| **Technical Unknowns** | ✅ Yes (many) | ❌ None/minimal | ❌ None | May have | ❌ None | N/A |
| **Needs Validation** | ✅ Yes | ❌ No | ❌ No | Maybe | ❌ No | N/A |
| **Multiple Approaches** | ✅ Yes | ❌ No | ❌ No | N/A | ❌ No | N/A |
| **Issue Type** | Any | Feature/Bug | Refactor/Quality | Any | Meta/Process | Duplicate/Invalid |
| **Scope** | May be undefined | Well-defined | Well-defined | Well-defined | Well-defined | N/A |
| **Complexity** | High | Low-Medium | Low-Medium | Any | Low-Medium | N/A |
| **Prioritization Needed** | No | No | No | ✅ Yes | No | N/A |

---

## Example 1: Feature Request with Clear Requirements

### Scenario

**Issue Title**: "Add support for cancellation tokens in BatchBlock"

**Issue Description**:
```
Currently BatchBlock doesn't support cancellation tokens for batch window timeouts.
This makes it impossible to cancel long-running batch operations gracefully.

Expected behavior:
- BatchBlock constructor accepts CancellationToken
- Batch window respects cancellation
- Pending batches are flushed on cancellation

Acceptance criteria:
- Unit tests for cancellation scenarios
- Documentation updated
- No breaking changes to existing API
```

### Decision Factors

- **Requirements**: ✅ Clear and specific
- **Scope**: ✅ Well-defined (single component, clear behavior)
- **Success criteria**: ✅ Explicitly stated
- **Technical unknowns**: ✅ None (standard .NET pattern)
- **Complexity**: Low - straightforward enhancement

### Triage Decision

**Workflow**: `workflow:implementation`

### Rationale

This issue has clear requirements, well-defined scope, and no technical unknowns. The implementation approach is standard (CancellationToken pattern in .NET). No research or prototyping needed - ready for direct implementation.

---

## Example 2: Performance Issue Requiring Investigation

### Scenario

**Issue Title**: "BatchBlock performance degrades with large batch sizes"

**Issue Description**:
```
When using BatchBlock with maxBatchSize > 10000, memory usage spikes and throughput drops.
Need to investigate why this happens and find optimal solution.

Observed behavior:
- Batch sizes < 5000: ~100k items/sec
- Batch sizes > 10000: ~20k items/sec
- Memory usage increases non-linearly

Questions:
- Is this due to array allocation patterns?
- Should we use object pooling?
- Are there better data structures?
```

### Decision Factors

- **Requirements**: ⚠️ Clear problem, but solution unclear
- **Scope**: ⚠️ Needs investigation to define scope
- **Technical unknowns**: ❌ Multiple unknowns (root cause, optimal approach)
- **Complexity**: High - requires profiling, testing multiple approaches
- **Multiple approaches**: Yes - pooling vs data structures vs algorithm changes

### Triage Decision

**Workflow**: `workflow:research`

### Rationale

While the problem is clear, the solution requires investigation. Multiple technical unknowns exist (root cause, best approach). This needs profiling, prototyping different solutions, and benchmarking before implementation. Research workflow will validate the approach, then hand over to implementation.

---

## Example 3: Code Quality Improvement

### Scenario

**Issue Title**: "Refactor ActorPool to use modern C# patterns"

**Issue Description**:
```
The ActorPool class was written early in the project and doesn't follow current coding standards:
- Uses old-style constructors instead of primary constructors
- Verbose property declarations
- No use of modern C# 12 features

This is working code but needs modernization for maintainability.
```

### Decision Factors

- **Issue Type**: Tech debt (code modernization)
- **Requirements**: ✅ Clear (apply modern patterns)
- **Functional changes**: None (refactoring only)
- **Impact**: Code quality and maintainability
- **Technical unknowns**: None

### Triage Decision

**Workflow**: `workflow:tech-debt`

### Rationale

This is a code quality issue, not a feature or bug. The work is refactoring existing code to modern standards without changing functionality. Tech Debt workflow handles discovery, analysis, and creates backlog items for such improvements.

---

## Example 4: Multiple Feature Requests Needing Prioritization

### Scenario

**Issue Title**: "Add metrics collection to all block types"

**Issue Description**:
```
We should add metrics (throughput, latency, queue depth) to all block types.
This is one of several observability improvements requested:
- Metrics collection (this issue)
- Distributed tracing support (#145)
- Structured logging (#167)
- Health check endpoints (#201)

All are valuable but resource-constrained. Need to prioritize.
```

### Decision Factors

- **Requirements**: ✅ Clear request
- **Scope**: ✅ Well-defined
- **Context**: Part of larger observability initiative
- **Prioritization needed**: ✅ Multiple competing requests
- **Business value assessment**: Needed to decide order

### Triage Decision

**Workflow**: `workflow:product-backlog`

### Rationale

While the requirement is clear and could be implemented, it's part of multiple competing priorities. Product Prioritization workflow will assess business value, dependencies, and resource allocation to determine implementation order. After prioritization, it will hand over to implementation workflow.

---

## Example 5: Workflow Process Improvement

### Scenario

**Issue Title**: "Add visual decision tree to research workflow documentation"

**Issue Description**:
```
The research workflow documentation is comprehensive but lacks visual aids.
A decision tree would help agents quickly understand when to use the workflow.

Proposed improvement:
- Create Mermaid flowchart showing research phases
- Add quick reference section
- Include common decision points

This is about improving workflow documentation, not code.
```

### Decision Factors

- **Issue Type**: Process improvement (meta-issue about workflows)
- **Target**: Team processes and documentation
- **Code changes**: None (documentation only)
- **Impact**: Workflow effectiveness and clarity

### Triage Decision

**Workflow**: `workflow:process-modeling`

### Rationale

This issue is about improving team workflows and processes, not implementing features. Process Modeling workflow handles systematic improvement of workflows through tabletop simulation and testing. It produces refined workflow documentation.

---

## Example 6: Bug with Unclear Reproduction Steps

### Scenario

**Issue Title**: "TransformBlock occasionally drops items"

**Issue Description**:
```
Sometimes items seem to disappear when using TransformBlock with high concurrency.
Not sure if this is a bug or my code issue.

What I'm doing:
- TransformBlock with maxConcurrency=10
- Processing ~100k items
- Sometimes output count < input count

I can't reproduce it consistently.
```

### Decision Factors

- **Requirements**: ❌ Unclear - no reproduction steps
- **Scope**: ❌ Undefined - could be user error or actual bug
- **Success criteria**: ❌ Not stated
- **Information needed**: Minimal reproduction example, expected vs actual behavior

### Triage Decision

**Workflow**: Keep in `workflow:triage`

**Action**: Request clarification

### Rationale

The issue lacks sufficient information to route to any workflow. Triage agent should ask for:
- Minimal reproducible example
- Expected behavior
- Actual behavior
- Environment details

After clarification, re-triage based on new information. If it's a reproducible bug → implementation. If it requires investigation → research. If it's user error → close with explanation.

---

## Example 7: Duplicate Issue

### Scenario

**Issue Title**: "Add support for async error handling in ProcessorBlock"

**Issue Description**:
```
ProcessorBlock should support async error handling callbacks.
Currently errors just cause the block to stop processing.
```

### Decision Factors

- **Duplicate check**: ✅ Issue #156 already tracks this
- **Status**: Original issue (#156) is in implementation workflow
- **PR**: #178 is already implementing this feature

### Triage Decision

**Action**: Close as duplicate

### Rationale

This is a duplicate of existing issue #156, which is already in progress. Triage agent should:
1. Close this issue
2. Link to original issue
3. Mention PR if relevant
4. Thank contributor for the suggestion

No workflow designation needed for duplicates.

---

## Example 8: Architecture Change Requiring Validation

### Scenario

**Issue Title**: "Support push-based blocks in addition to pull-based"

**Issue Description**:
```
Current architecture is entirely pull-based. Some scenarios would benefit from push-based blocks.

Questions:
- Can we support both models?
- How do they interoperate?
- What's the performance impact?
- Does this break existing architecture assumptions?

This is a significant architectural change.
```

### Decision Factors

- **Architectural impact**: ✅ Major - changes core design principles
- **Technical unknowns**: ✅ Many (interop, performance, compatibility)
- **Feasibility**: Unknown - needs validation
- **Multiple approaches**: Yes - different integration strategies possible
- **Complexity**: Very high

### Triage Decision

**Workflow**: `workflow:research`

### Rationale

This is a major architectural change with significant unknowns. Research workflow should:
- Investigate feasibility
- Prototype different approaches
- Evaluate impact on existing architecture
- Benchmark performance
- Create design document
- Recommend proceed/reject with rationale

After research validates approach, hand over to implementation or product backlog based on findings.

---

## Example 9: Security Vulnerability

### Scenario

**Issue Title**: "Potential thread safety issue in shared channel access"

**Issue Description**:
```
SECURITY: Found potential race condition in channel access that could cause data corruption.

Affected code: ActorPool.cs lines 45-67
Impact: High - data integrity at risk
Reproducibility: Race condition - intermittent

Needs immediate attention.
```

### Decision Factors

- **Severity**: ✅ High (security/data integrity)
- **Requirements**: ✅ Clear (fix race condition)
- **Technical approach**: ✅ Clear (add proper locking/synchronization)
- **Urgency**: ✅ High priority
- **Complexity**: Medium - known solution patterns

### Triage Decision

**Workflow**: `workflow:implementation`

**Additional labels**: `priority:high`, `security`

### Rationale

Despite high severity, this is clear to implement - proper thread synchronization is a known pattern. No research needed. Route to implementation with high priority labels to ensure prompt attention. Implementation workflow can handle urgent fixes.

---

## Example 10: Feature Request Needing Design Exploration

### Scenario

**Issue Title**: "Add retry policies with exponential backoff"

**Issue Description**:
```
Would be great to have built-in retry policies for failed operations.

Ideas:
- Exponential backoff
- Circuit breaker pattern
- Dead letter queue
- Configurable retry limits

Not sure which approach is best or how they'd integrate with existing architecture.
```

### Decision Factors

- **Requirements**: ⚠️ Multiple ideas, no specific requirement
- **Scope**: ⚠️ Undefined - several different patterns mentioned
- **Technical approach**: ❌ Unknown - multiple options
- **Integration**: ❌ Unclear how it fits existing architecture
- **Complexity**: High - needs design work

### Triage Decision

**Workflow**: `workflow:research`

### Rationale

While the general idea is clear, the specific requirements are not. Multiple different patterns are mentioned (retry, circuit breaker, DLQ), each with different use cases and complexities. Research workflow should:
- Define specific use cases
- Evaluate which patterns apply
- Design integration with existing architecture
- Create focused specification
- Hand over to implementation or product backlog

This prevents implementation from building the wrong thing or over-engineering a solution.

---

## Quick Reference Decision Matrix

| Factor | Research | Implementation | Tech Debt | Product Backlog | Process Modeling | Close |
|--------|----------|----------------|-----------|-----------------|------------------|-------|
| **Clear Requirements** | May lack specifics | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | N/A |
| **Technical Unknowns** | ✅ Yes (many) | ❌ None/minimal | ❌ None | May have | ❌ None | N/A |
| **Needs Validation** | ✅ Yes | ❌ No | ❌ No | Maybe | ❌ No | N/A |
| **Multiple Approaches** | ✅ Yes | ❌ No | ❌ No | N/A | ❌ No | N/A |
| **Issue Type** | Any | Feature/Bug | Refactor/Quality | Any | Meta/Process | Duplicate/Invalid |
| **Scope** | May be undefined | Well-defined | Well-defined | Well-defined | Well-defined | N/A |
| **Complexity** | High | Low-Medium | Low-Medium | Any | Low-Medium | N/A |
| **Prioritization Needed** | No | No | No | ✅ Yes | No | N/A |

---

## Common Triage Patterns

### Pattern: "Feature Request → Research → Product Backlog → Implementation"

**When**: Feature idea needs validation, then prioritization, then build

**Example**: "Add distributed caching" → Research validates approach → Product prioritizes against other features → Implementation builds it

### Pattern: "Bug Report → Implementation (if clear) OR Research (if unclear)"

**When**: Bug report received

**Decision**:
- Clear reproduction + known fix → Implementation
- Unclear root cause → Research to investigate

### Pattern: "Code Quality → Tech Debt → Product Backlog → Implementation"

**When**: Refactoring or quality improvement identified

**Flow**: Tech Debt analyzes and creates backlog items → Product prioritizes → Implementation executes

### Pattern: "Process Improvement → Process Modeling → [Workflow Updates]"

**When**: Workflow or process change proposed

**Flow**: Process Modeling tests and refines → Updates workflow documentation → Closes issue

### Pattern: "Needs Clarification → Stay in Triage"

**When**: Insufficient information to make routing decision

**Action**: Request clarification, keep in triage queue until information provided

---

## Edge Cases

### Re-triage Scenarios

Issues can be sent **back** to triage if:

1. **Requirements change significantly** during implementation
2. **Initial assessment was incorrect** (complexity misjudged)
3. **Blocker discovered** that changes scope
4. **Need re-evaluation** after external changes

**Example**: Implementation starts on "Add caching" but discovers it requires distributed architecture changes → Send back to triage to re-assess as research vs implementation

### Multiple Workflows Applicable

When multiple workflows could apply:

1. **Choose the FIRST necessary workflow**
2. **Note in handover** that additional workflows may follow
3. **Trust the workflow** to hand over appropriately

**Example**: "Optimize ActorPool performance"
- Could go to Research (investigate approaches) OR Implementation (apply known optimizations)
- If approach is unclear → Research first
- Research will validate approach, then hand to Implementation

---

## Anti-Patterns to Avoid

### ❌ Sending Unclear Issues to Implementation

**Problem**: Implementation gets stuck asking for clarification

**Solution**: Clarify in triage OR send to research if investigation needed

### ❌ Sending Clear Bugs to Research

**Problem**: Wastes time on unnecessary investigation

**Solution**: If bug is reproducible and fix is clear → Implementation

### ❌ Not Checking for Duplicates

**Problem**: Wasted effort on duplicate work

**Solution**: Always search existing issues before routing

### ❌ Over-thinking Simple Issues

**Problem**: Analysis paralysis on straightforward requests

**Solution**: If it's clearly a simple bug/feature → Implementation (don't overthink)

### ❌ Under-thinking Complex Issues

**Problem**: Implementation struggles with undefined requirements

**Solution**: If there are unknowns or multiple approaches → Research

---

## Summary

**Key Triage Questions**:

1. **Are requirements clear?** → If no, ask for clarification or send to research
2. **Are there technical unknowns?** → If yes, send to research
3. **What type of issue is it?** → Feature/bug → implementation, refactor → tech debt, process → process modeling
4. **Does it need prioritization?** → If yes, send to product backlog
5. **Is it valid and unique?** → If no, close with explanation

**When in doubt**:
- Unknowns or multiple approaches → Research
- Clear and simple → Implementation
- Quality/refactor → Tech Debt
- Competing priorities → Product Backlog
- Workflow improvement → Process Modeling
- Invalid/duplicate → Close

**Remember**: It's okay to re-triage if the initial assessment was wrong. The workflow system is flexible.
