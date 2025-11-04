# Archived Phase 7 Documents

This folder contains documentation for approaches that were explored but ultimately not adopted during Phase 7 research.

## Why Archived?

During the Phase 7 exploration, we initially pursued a channel-based `EventChannelNode` approach with separate typed channels per event type. Through discussion and analysis, we identified critical issues that led to a pivot:

1. **Ordering Problem**: Separate channels couldn't guarantee strict event ordering across types (EpochCreated → EpochCompleted → GlobalAlignment)
2. **Implementation Blocker**: Required 6-10 hours of graph execution refactoring to wire channels into execution pipeline
3. **Over-Engineering**: Event use cases are source→consumers only (1 level deep), full channel composability unnecessary
4. **Complexity**: 450 LOC channel infrastructure vs 100 LOC hybrid coordinator-wrapped approach

## Pivot Decision

We pivoted to a **Hybrid Coordinator-Wrapped Approach**:
- `EpochLifecycleNode` wrapping existing `EpochLifecycleCoordinator`
- Implicit auto-registration via `IEpochLifecycleParticipant` interface
- No graph execution changes needed
- Preserves strict event ordering and proven abstractions

## Documents in This Folder

- **adr-event-type-handling-dismissed.md**: ADR proposing separate channels per event type (dismissed due to ordering concerns)
- **design-event-channel-dismissed.md**: Design document for channel-based EventChannelNode (dismissed due to complexity)

## Reference Code

Functional exploratory code for the channel-based approach is preserved in:
- `/poc/docs/research/phase7-event-channel-exploration/` (non-compilable reference)

For compilable exploratory tests and code, see:
- `/poc/DataFlow.POC.Tests/Exploratory/Phase7EventChannel/` (if created)

## Related Documents

- **Adopted Research Findings**: `/poc/docs/research/event-channel-node-poc-findings.md` (contains analysis of both approaches)
- **Plan**: `../plan.md` (Phase 7 exploration plan with outcomes)
