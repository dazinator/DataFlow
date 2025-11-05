---
name: Implementation Issue
about: Implementation work based on research team handover or direct requirements
title: '[Implementation] '
labels: ['implementation']
assignees: ''
---

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

### Handover Document (if from research team)

**Handover Document Path**: [e.g., `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md`]

If this implementation is based on research team handover, the handover document contains:
- Complete problem context and background
- Implementation guidance and recommended approach
- Test scenarios and performance requirements
- Design references and supporting documentation

**@copilot**: Read the handover document first to understand the complete scope and context.

### Target Codebase

**Target**: [ ] POC (`/poc`) or [ ] Production (`/src`) or [ ] Both

The handover document or context below should clearly indicate which codebase this implementation targets.

**@copilot**: If the target codebase is unclear from the handover document and context, **STOP** and ask the user to clarify before proceeding.

## Problem Statement

[If NOT from research handover: Describe the specific problem or feature to implement]

[If from research handover: Reference the handover document - most context is there]

## Implementation Checklist

Based on the handover document (or requirements below), the implementation should include:

- [ ] Implementation with tests
- [ ] Performance validation (if benchmarks specified)
- [ ] Documentation updates
- [ ] Edge cases handled (as specified in handover)
- [ ] Code review and validation

## Additional Context (if needed)

[Any additional context not in the handover document]

[If this is direct implementation without research handover, provide full requirements here]

## Success Criteria

- [ ] All objectives from handover document met (or requirements met if direct implementation)
- [ ] Tests passing
- [ ] Performance requirements met (if applicable)
- [ ] Documentation updated
