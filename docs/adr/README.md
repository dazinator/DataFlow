# Architecture Decision Records (ADR)

This folder contains **Architecture Decision Records** - lightweight documents that record important architectural decisions made in the project.

## Purpose

ADRs provide:

- A historical record of why decisions were made
- Context for future developers
- Traceability for architectural choices
- Version control for decisions

## When to Create an ADR

Create an ADR when you make a decision about:

- System architecture or structure
- Technology choices
- Design patterns to adopt or avoid
- Major refactoring approaches
- Cross-cutting concerns

**Note**: Not every decision needs an ADR. Focus on decisions that have lasting impact or require future reference.

## File Naming Convention

ADR files use a date-based naming convention:

```
YYYY-MM-DD-short-title.md
```

**Examples**:
- `2025-11-11-pull-based-architecture.md`
- `2025-11-11-dependency-injection-strategy.md`

## ADR Template

Use this template for new ADRs:

```markdown
# [Decision Title]

**Date**: YYYY-MM-DD
**Status**: Proposed | Accepted | Deprecated | Superseded

## Context

What is the issue we're facing? What factors are we considering?

## Options Considered

### Option 1: [Name]
- Pros: ...
- Cons: ...

### Option 2: [Name]
- Pros: ...
- Cons: ...

## Decision

What decision did we make and why?

## Consequences

### Positive
- What benefits does this decision bring?

### Negative
- What drawbacks or trade-offs do we accept?

### Neutral
- What other impacts does this have?

## Related

- Related issue: uniun-technology/lib-dataflow#XXX
- Related analysis: [Link if applicable]
- Related design: [Link if applicable]
- Supersedes: [Link to previous ADR if applicable]
```

## POC-Specific ADRs

ADRs related to POC (Proof of Concept) code are organized in a `poc/` subfolder:

```
/docs/adr/
  poc/
    YYYY-MM-DD-decision.md
```

This keeps POC-related decisions separate from production ADRs while maintaining chronological ordering within each category.

## Linking ADRs

Reference ADRs from issues, designs, or code:

```markdown
See ADR: [Decision Title](../../docs/adr/YYYY-MM-DD-title.md)
```

In your ADR, link back to related documentation:

```markdown
Related issue: uniun-technology/lib-dataflow#123
Related design: [Design Name](../design/<topic>/README.md)
```

## Updating ADRs

ADRs should be **immutable** once accepted. If a decision changes:

1. Create a new ADR with the updated decision
2. Update the old ADR's status to "Superseded"
3. Link between the old and new ADRs

## Status Definitions

- **Proposed**: Decision is under consideration
- **Accepted**: Decision has been made and is active
- **Deprecated**: Decision is no longer recommended but may still be in use
- **Superseded**: Decision has been replaced by a new ADR (include link)
