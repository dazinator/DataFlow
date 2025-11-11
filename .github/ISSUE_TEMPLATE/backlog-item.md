---
name: Product Backlog Item
about: Create a backlog item for prioritization
title: '[Backlog] '
labels: ['workflow:product-backlog']
assignees: ''
---

## Summary

Brief 1-2 sentence description of what needs to be implemented.

---

## Metadata

**Source**: [Research / Tech Debt / Ad-hoc / User Request]
**Category**: [Feature / Bug Fix / Tech Debt / Performance / Documentation / Security]
**Effort Estimate**: [Small (< 1 day) / Medium (1-3 days) / Large (> 3 days)]
**Created Date**: YYYY-MM-DD

### Priority Override (Optional - Product Team Only)

**Priority Override**: [1-5] (Set by: [Name], Date: YYYY-MM-DD, Reason: [Brief rationale])

*Leave blank unless product team explicitly sets a priority override*

### Security Information (If Applicable)

**CVE Reference**: [CVE-YYYY-NNNNN] or [N/A]
**CVE Criticality**: [Critical / High / Medium / Low] or [N/A]
**Affected Code Location**: [Core (/src/) / Non-Core (/sample/, /tools/, /poc/)]

*Only fill in if this is a security vulnerability*

---

## Context

Background and rationale - why this work is needed:
- What problem does this solve?
- What value does it provide?
- Who is affected?

---

## Implementation Guidance

High-level guidance for implementation team:
- Key requirements
- Constraints or considerations
- Suggested approach (if any)
- Files affected or areas of code

### External Dependencies (if applicable)

If implementation requires external services or infrastructure:

- [ ] List all external dependencies (databases, endpoints, APIs, etc.)
  - Service/dependency name
  - Connection details or requirements
  - Availability expectations (e.g., must have, nice-to-have)
- [ ] State whether runtime validation is required or optional
- [ ] Provide alternative validation approach if external service is optional

*If no external dependencies, write "No external dependencies required"*

---

## Success Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3
- [ ] Tests passing
- [ ] Documentation updated (if applicable)

---

## Handover Assets

**Research Handover**: [Link to /research/[topic]/ folder] or [N/A]
**Tech Debt Analysis**: [Link to analysis] or [N/A]
**Prototype Code**: [Link to prototype] or [N/A]
**Design Documents**: [Link to design docs] or [N/A]
**Benchmarks**: [Link to benchmarks] or [N/A]

---

## References

- Related issues: #[issue numbers]
- Related PRs: #[PR numbers]
- Related ADRs: [links]
- External links: [documentation, articles, etc.]

---

## Notes

Additional context or considerations:
- Edge cases to consider
- Known limitations
- Future enhancements
