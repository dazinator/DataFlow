---
name: Architecture Decision Record (ADR)
about: Document an important architectural decision
title: '[ADR] '
labels: ['documentation', 'architecture']
assignees: ''

---

## Decision Title

**Brief Description**: [One sentence description of the decision]

## Context

What architectural decision needs to be made? What is the background?

[Describe the context]

## Problem Statement

What problem or question does this decision address?

[Describe the problem]

## Options Considered

### Option 1: [Name]

**Pros:**
- [List advantages]

**Cons:**
- [List disadvantages]

### Option 2: [Name]

**Pros:**
- [List advantages]

**Cons:**
- [List disadvantages]

### Option 3: [Name] (if applicable)

**Pros:**
- [List advantages]

**Cons:**
- [List disadvantages]

## Recommendation

Which option do you recommend and why?

[Provide recommendation with rationale]

## Consequences

### Positive Consequences

- [What benefits will this decision bring?]

### Negative Consequences

- [What drawbacks or trade-offs do we accept?]

### Neutral Consequences

- [What other impacts will this have?]

## ADR Deliverable

- [ ] ADR document: `/docs/adr/YYYY-MM-DD-<title>.md`
  - Use date format: `YYYY-MM-DD`
  - Use lowercase with hyphens for title
  - If POC-specific: `/docs/adr/poc/YYYY-MM-DD-<title>.md`

## Related Documentation

- Related analysis: [link if applicable]
- Related design: [link if applicable]
- Related issues: #[issue number]
- Supersedes: [link to previous ADR if applicable]

---

## For @copilot

When creating the ADR:

1. Create file: `/docs/adr/YYYY-MM-DD-<title>.md`
   - For POC decisions: `/docs/adr/poc/YYYY-MM-DD-<title>.md`
2. Use the template from `/docs/adr/TEMPLATE.md`
3. Include:
   - Date and status
   - Context
   - Options considered
   - Decision and rationale
   - Consequences
   - Related documentation links
4. Set status to "Proposed" initially
5. Update to "Accepted" after approval
6. Follow ADR guidelines in `/docs/adr/README.md`
