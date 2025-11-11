---
name: Design Document
about: Create a solution design or implementation plan
title: '[Design] '
labels: ['documentation']
assignees: ''

---

## Design Topic

**Brief Description**: [One sentence description of what you're designing]

## Objective

What are you trying to achieve with this design?

[Describe the objective]

## Problem Statement

What problem does this design solve?

[Describe the problem]

## Constraints

What limitations or requirements must this design meet?

- Technical constraints:
- Business constraints:
- Timeline constraints:
- Resource constraints:

## Proposed Solution

### High-Level Overview

[Describe the proposed solution at a high level]

### Architecture

[Describe the architecture - consider using diagrams]

### Impacted Components

What parts of the system will be affected?

- [ ] Component 1
- [ ] Component 2
- [ ] Component 3

## Design Deliverables

- [ ] Main design document: `/docs/design/<topic>/README.md`
- [ ] Architecture diagrams
- [ ] Prototypes (if applicable)
- [ ] Alternative approaches considered

## Related Documentation

- Based on analysis: [link if applicable]
- Related ADRs: [link if applicable]
- Related issues: #[issue number]

## Implementation Plan

How will this design be implemented?

- [ ] Phase 1: [description]
- [ ] Phase 2: [description]
- [ ] Phase 3: [description]

## Success Criteria

How will we know this design is successful?

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

---

## For @copilot

When creating the design document:

1. Create folder: `/docs/design/<topic>/`
2. Create main document: `/docs/design/<topic>/README.md`
3. Include:
   - Objective
   - Constraints
   - Proposed architecture (with diagrams if helpful)
   - Impacted components
   - Linked analysis or ADRs
4. Cross-link with related documentation
5. Follow the design documentation structure in `/docs/design/README.md`
