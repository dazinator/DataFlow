# Phase 7 EventChannelNode Exploration - Reference Code

**Status:** EXPLORATORY ONLY - NOT USED IN PRODUCTION CODE

This folder contains reference code from the initial channel-based event propagation approach that was explored during Phase 7 POC.

## Background

During Phase 7, we explored two approaches for making epoch lifecycle event subscriptions explicit in the DataFlow graph:

1. **Channel-Based Approach** (this folder) - EventChannelNode with typed channels and edge strategies
2. **Hybrid Approach** (adopted) - EpochLifecycleNode wrapping the existing coordinator

## Why We Pivoted

The channel-based approach was abandoned in favor of the simpler hybrid approach for these reasons:

1. **Ordering Problem**: Separate channels per event type couldn't guarantee strict ordering across event types (EpochCreated → EpochCompleted → GlobalAlignment)
2. **Implementation Blocker**: Required 6-10 hours of graph execution refactoring to wire EventChannelNodes into the execution pipeline
3. **Over-Engineering**: Event use cases are source→consumers only (1 level deep), so full channel composability wasn't needed
4. **Complexity**: ~450 LOC vs ~100 LOC for the hybrid approach

## Reference Files

- **EventChannelNode.cs** - Channel-backed graph node for event distribution
- **EpochLifecycleEvents.cs** - Typed event definitions (EpochCreatedEvent, EpochCompletedEvent, GlobalAlignmentEvent)
- **EventEdgeStrategies.cs** - Sequential and broadcast delivery strategies
- **EventChannelNodeTests.cs** - Tests validating channel-based approach

## Key Learnings

1. Strict event ordering is critical for epoch lifecycle events (transaction boundaries)
2. Graph-native visibility doesn't require channel routing - metadata connections are sufficient
3. Reusing proven abstractions (EpochLifecycleCoordinator) is simpler than building new infrastructure
4. Convention over configuration (implicit auto-registration) provides better developer experience

## Related Documentation

- [Research Findings](/poc/docs/research/event-channel-node-poc-findings.md) - Full analysis comparing both approaches
- [ADR: Event Type Handling](/poc/docs/adr/2025-11-03-event-type-handling.md) - Documents the pivot decision
- [Phase 7 Plan](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION.md) - Complete POC plan and outcomes

## Note

**These files are kept for reference only** to document the exploration process and architectural trade-offs. They do not compile with the current codebase and should not be used in production code.
