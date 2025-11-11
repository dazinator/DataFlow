---
name: Analysis Document
about: Create an investigation, benchmark, or exploratory research document
title: '[Analysis] '
labels: ['documentation']
assignees: ''

---

## Analysis Topic

**Brief Description**: [One sentence description of what you're analyzing]

## Purpose

What are you trying to understand or investigate?

- [ ] Problem discovery / Root cause analysis
- [ ] Performance benchmark
- [ ] Technology comparison
- [ ] Exploratory research
- [ ] Tech debt investigation
- [ ] Other: [specify]

## Context and Motivation

Why is this analysis needed? What problem are you trying to understand?

[Provide context]

## Scope

What will this analysis cover?

- What's in scope:
- What's out of scope:

## Analysis Deliverables

- [ ] Main analysis document: `/docs/analysis/<topic>/README.md`
- [ ] Benchmark data (if applicable)
- [ ] Comparison tables (if applicable)
- [ ] Supporting documents

## Related Issues

- Related to: #[issue number if applicable]
- Blocking: #[issue number if applicable]

## Expected Outcome

What do you hope to learn from this analysis?

[Describe expected outcome]

## Next Steps

Will this analysis lead to:

- [ ] Design proposal (create Design document)
- [ ] ADR (create Architecture Decision Record)
- [ ] Implementation (create Implementation issue)
- [ ] No further action (analysis for information only)

---

## For @copilot

When creating the analysis document:

1. Create folder: `/docs/analysis/<topic>/`
2. Create main document: `/docs/analysis/<topic>/README.md`
3. Include:
   - Context and motivation
   - Observations and metrics
   - Alternative approaches
   - Outcome and conclusions
   - Link to related issues
4. Cross-link with related documentation
5. Follow the analysis documentation structure in `/docs/analysis/README.md`
