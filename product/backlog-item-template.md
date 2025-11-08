# [Title]

**Backlog ID**: [filename-without-md-extension]
**Source**: [Research/Tech Debt/Ad-hoc]
**Category**: [Feature/Bug Fix/Tech Debt/Performance/Documentation/etc.]
**Status**: Active / In Progress / Completed
**Created**: YYYY-MM-DD
**Updated**: YYYY-MM-DD

## Summary

[Brief 1-2 sentence description of what needs to be implemented]

## Context

[Background and rationale - why this work is needed]
- What problem does this solve?
- What value does it provide?
- Who is affected?

## Implementation Guidance

[High-level guidance for implementation team]
- Key requirements
- Constraints or considerations
- Suggested approach (if any)
- Files affected or areas of code

## Success Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3
- [ ] Tests passing
- [ ] Documentation updated (if applicable)

### Documentation Deliverables (if applicable)

Use this checklist when implementation requires new or updated documentation:

- [ ] Usage guide for new features/utilities
  - Scope: [ ] Comprehensive (>10KB) or [ ] Quick start (<3KB)
  - Target audience: [ ] End users [ ] Contributors [ ] Both
- [ ] Pattern/best practices guide (if introducing new patterns)
- [ ] README for new directories/modules
- [ ] Update relevant index/navigation files
  - Examples: `/poc/docs/INDEX.md`, main project README, module READMEs

*If no new documentation is needed, state why in Success Criteria (e.g., "Existing documentation covers this functionality").*

### Example Tests Guidance (if applicable)

When handover includes prototype code or new patterns, specify example test expectations:

- **Minimum**: 3-5 example/demo tests demonstrating key usage patterns
- **Focus**: Common patterns rather than exhaustive feature coverage
- **Comparison tests**: Include "before/after" tests if demonstrating improvements
  - Example: Show OLD pattern vs NEW pattern side-by-side
- **Location**: Where example tests should be created (e.g., in test project, POC tests, etc.)

*If no example tests are needed beyond standard unit tests, state this explicitly in Success Criteria.*

## Handover Assets

[If handover folder exists]
- **Location**: `/product/backlog/[backlog-item-id]/`
- **Contents**: [List what's in the handover folder]
  - Prototype code
  - Design documents
  - Benchmarks
  - Test scenarios
  - etc.

[If no handover folder]
- No additional assets

## References

- Source handover/analysis: [path to research folder or tech debt analysis]
- Related issues: #[issue numbers]
- Related PRs: #[PR numbers]
- Related ADRs: [links to architecture decision records]
- External links: [documentation, articles, etc.]

## Notes

[Any additional context or considerations]
- Edge cases to consider
- Known limitations
- Future enhancements
- etc.

---

## Status History

- **YYYY-MM-DD**: Created
- **YYYY-MM-DD**: Updated [what changed]
- **YYYY-MM-DD**: Completed (PR: #[N])
