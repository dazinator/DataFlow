# Design Documentation

This folder contains solution designs and implementation plans, including:

- New feature designs
- Remediation plans for tech debt
- Refactoring proposals
- Architecture changes
- System improvements

## Purpose

Design documents describe **how we plan to solve problems**. They are created after analysis (if needed) and before implementation.

## When to Create a Design Document

Create a design document when you need to:

- Propose a new feature or capability
- Design a solution to a known problem
- Plan a significant refactoring
- Document a remediation plan for tech debt
- Outline an architecture change

## Structure

Each design topic should have its own subfolder:

```
/docs/design/
  /<topic>/
    README.md          # Main design document
    diagrams/          # Architecture diagrams (if applicable)
    prototypes/        # Code prototypes (if applicable)
    alternatives/      # Alternative approaches considered
```

## What to Include

Your design document should include:

1. **Objective**: What are you trying to achieve?
2. **Constraints**: What limitations or requirements must be met?
3. **Proposed Architecture**: How will the solution work? (Include diagrams)
4. **Impacted Components**: What parts of the system are affected?
5. **Linked Analysis or ADRs**: Reference related documentation

## Linking to Issues

Reference design documents from GitHub issues using relative paths:

```markdown
See design: [Feature Design](../../docs/design/<topic>/README.md)
```

In your design document, link back to the related issue:

```markdown
Related issue: uniun-technology/lib-dataflow#123
```

## Cross-Linking with Analysis and ADRs

When a design is based on analysis:

- Reference the analysis: "Based on analysis: [Analysis Name](../analysis/<topic>/README.md)"

When a design leads to architectural decisions:

- Create an ADR: See `/docs/adr/` folder
- Reference the ADR: "Decision recorded in ADR: [ADR Title](../adr/YYYY-MM-DD-title.md)"
