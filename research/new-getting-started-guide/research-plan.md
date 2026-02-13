# Research Plan: New Getting Started Guide

**Research Topic**: Create a streamlined, modern getting started guide based on test patterns
**Status**: In Progress
**Created**: 2025-02-13
**Researcher**: @copilot

---

## Research Objective

Create a better getting started guide that:
1. Is less verbose than the current 1051-line version
2. Leverages modern patterns from `RevisedDiRegistrationTests` (DI registration, block patterns)
3. Demonstrates graph reuse patterns from `BlockLifetimeAndGraphReuseTests`
4. Shows namespacing and keyed services clearly
5. Is easy to follow for a new developer creating their first console app

## Research Questions

1. What are the essential concepts a new developer needs to start?
2. How can we demonstrate DI registration patterns from `RevisedDiRegistrationTests`?
3. How can we show graph reuse and namespacing from `BlockLifetimeAndGraphReuseTests`?
4. What's the minimal path from "nothing" to "working dataflow"?
5. How do keyed services work and how should we explain them?

## Current State Analysis

### Current Getting Started Guide (`/poc/docs/guides/getting-started.md`)
- **Size**: 1051 lines
- **Pros**: Comprehensive, covers many scenarios
- **Cons**: 
  - Too verbose for a "getting started" guide
  - Includes advanced topics like trigger context
  - May overwhelm new users
  - Doesn't clearly demonstrate the test patterns

### Previous Research (`/research/library-usage-guides/`)
- Already created a 743-line getting started guide
- More concise but still comprehensive
- Doesn't specifically focus on the test patterns mentioned in the issue

### Key Test Patterns to Leverage

#### From `RevisedDiRegistrationTests`:
1. **Simple DI registration**: `services.AddDataFlows("global", df => { ... })`
2. **Block registration with names**: `df.AddBlock("producer", sp => new TestProducerBlock())`
3. **Graph registration**: `df.AddGraph("main", g => { ... })`
4. **UseBlock pattern**: `g.UseBlock("producer").UseBlock("transformer")`
5. **Keyed service resolution**: `serviceProvider.GetKeyedService<DataFlowGraph>("global:main")`

#### From `BlockLifetimeAndGraphReuseTests`:
1. **Multiple graphs using same blocks**: Block reuse pattern
2. **Namespacing**: Organizing multiple graphs
3. **Scoped block registration**: `df.AddScopedBlock(...)`
4. **Graph execution with scopes**: Creating execution contexts
5. **Concurrent execution**: Running multiple graphs

## Success Metrics

### Quantitative
- **Length**: Target 400-600 lines (vs current 1051)
- **Time to first working graph**: < 15 minutes
- **Code examples**: 3-5 complete examples (vs 10+ in current)
- **Sections**: 6-8 main sections (vs 12 in current)

### Qualitative
- **Clarity**: New developer can understand without prior knowledge
- **Modern patterns**: Uses patterns from the test files
- **Practical**: Every example is directly applicable
- **Progressive**: Each section builds on the previous

### Validation
- **Create a working console app**: Follow the guide step-by-step
- **Measure time**: How long does it take?
- **Note confusion points**: Where did I get stuck?
- **Verify keyed services**: Can I successfully resolve and execute graphs?

## Validation Approach

I will pretend to be a new developer and:
1. Create a fresh console app
2. Follow the guide step-by-step
3. Note any confusion or missing steps
4. Verify all code examples work
5. Measure time taken

## Expected Outcomes

1. **Research documentation** in `/research/new-getting-started-guide/`
2. **New streamlined guide** in `/research/new-getting-started-guide/handover/`
3. **Working prototype console app** demonstrating the patterns
4. **Implementation recommendation** to replace or update current guide
5. **Self-improvement feedback** on the research process

## Timeline

- **Phase 1**: Analysis (current) - 1 hour
- **Phase 2**: Design streamlined guide - 2 hours
- **Phase 3**: Create prototype and validate - 2 hours
- **Phase 4**: Document findings - 1 hour
- **Phase 5**: Self-improvement evaluation - 30 minutes

**Total estimated**: 6-7 hours

## Key Decisions

### What to Include
✅ Essential concepts (blocks, graphs, DI)
✅ First working example (minimal)
✅ DI registration patterns (from tests)
✅ Multiple graphs and namespacing (from tests)
✅ Keyed service resolution (from tests)
✅ Real-world console app example

### What to Defer to Other Guides
❌ Trigger context (advanced topic)
❌ Broadcast/competing consumers (topology guides)
❌ Epochs (advanced guide)
❌ EF Core integration (advanced guide)
❌ Testing patterns (testing guide)

## Notes

- Focus on the "happy path" - get users productive quickly
- Advanced topics should be linked but not covered in detail
- Every code example should be complete and runnable
- Use patterns from the tests as the authoritative source
