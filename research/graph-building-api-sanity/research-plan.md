# Research Plan: Graph Building API Sanity Check

**Research Issue**: #143  
**Created**: 2026-03-18  
**Duty**: Research

## Research Objective

Assess the API used to build data flows by reviewing a real application-level implementation. Identify any correctness issues and opportunities to improve ergonomics.

## Research Questions

1. **Fan-in correctness**: The sample code uses 3 separate `.Connect()` calls to merge ErpPoster1, ErpPoster2, and ErpPoster3 into `TmsBatch`. Is this the correct way to express fan-in? Does it align with the supported topology?

2. **UseBlock / Connect redundancy**: The sample declares every block via `UseBlock()` and then references the same name in `.Connect()`. Can `Connect()` automatically resolve blocks from DI so that `UseBlock()` is not required, or is there a better API design?

## Validation Approach

- Read the `DataFlowGraphBuilder` source to understand how `UseBlock()`, `Connect()`, and `ConnectCompeting()` interact during `Build()`
- Trace the execution path through `DataFlowGraph` to confirm how multiple incoming edges are handled (fan-in)
- Review existing topology guides and tests for confirmed fan-in support
- Design an improved API surface and assess its backward-compatibility impact

## Success Metrics

- **Correctness**: Confirm whether fan-in via 3 separate `Connect()` calls actually works at runtime
- **API clarity**: Identify whether the `UseBlock()` + `Connect()` two-step can be collapsed
- **Improvement proposal**: Produce a concrete, implementable API proposal that eliminates the redundancy while remaining backward-compatible

## Expected Outcomes

- Research documentation in `/research/graph-building-api-sanity/`
- Implementation-ready work item with full API design
- ADR-level notes captured if architectural decisions needed

## Timeline

Short research (1 day): questions are well-scoped and primarily require code reading + design work.
