# POC Research Glossary

This glossary documents concepts that were explored during POC research but were ultimately **not adopted** into the main codebase. These terms are preserved for historical reference and to document the exploration journey.

For **adopted** terms that are part of the current POC architecture, see [POC_GLOSSARY.md](POC_GLOSSARY.md).

---

## Legend

- ❌ **Not Adopted**: Explored and dismissed
- 🔄 **Superseded**: Replaced by alternative approach
- 📚 **Reference Only**: Documented for educational purposes

---

## Phase 7: Lifecycle Event Visibility

### EventChannelNode ❌
**Explored**: 2025-11-03  
**Phase**: 7  
**Status**: Not Adopted (Dismissed in favor of EpochLifecycleNode)  
**Reason**: Ordering problems, implementation complexity, over-engineering  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Channel-based graph node for event distribution using typed channels per event type (`EventChannelNode<TEvent>`). Events would flow through channels similar to data items, with full graph execution integration.

**Why Dismissed**:
1. **Ordering Problem**: Separate channels per event type couldn't guarantee strict ordering across event types (EpochCreated → EpochCompleted → GlobalAlignment)
2. **Implementation Blocker**: Required 6-10 hours of graph execution refactoring to wire channels into execution pipeline
3. **Over-Engineering**: Event use cases are source→consumers only (1 level deep), didn't need full channel composability
4. **Complexity**: 450 LOC channel infrastructure vs 100 LOC hybrid approach

**Superseded By**: EpochLifecycleNode (hybrid coordinator-wrapped node)

**Related Concepts**:
- Sequential Event Delivery
- Broadcast Event Delivery  
- Event Edge Strategies

---

### SequentialEventEdgeStrategy ❌
**Explored**: 2025-11-03  
**Phase**: 7  
**Status**: Not Adopted  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Proposed edge strategy for delivering events to multiple consumers one at a time in strict order. Part of the dismissed EventChannelNode approach.

**Why Dismissed**: Channel-based event strategies deemed unnecessary when coordinator already provides sequential delivery semantics.

**Concept Validity**: The sequential delivery pattern itself is valid and important for lifecycle events, but the channel-based implementation approach was over-engineered.

---

### BroadcastEventEdgeStrategy ❌
**Explored**: 2025-11-03  
**Phase**: 7  
**Status**: Not Adopted  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Proposed edge strategy for delivering events to multiple consumers simultaneously in parallel. Part of the dismissed EventChannelNode approach.

**Why Dismissed**: Channel-based event strategies deemed unnecessary; coordinator can handle broadcast patterns if needed in future.

---

### Separate Channels Per Event Type (Pattern) ❌
**Explored**: 2025-11-03  
**Phase**: 7  
**Status**: Not Adopted  
**ADR**: [ADR (Dismissed)](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/adr-event-type-handling-dismissed.md)

Architectural pattern using distinct `EventChannelNode<TEvent>` instances for each event type (e.g., separate channels for EpochCreatedEvent, EpochCompletedEvent, GlobalAlignmentEvent).

**Benefits (Explored)**:
- Compile-time type safety
- Zero filtering overhead
- Clear subscription semantics

**Critical Flaw**:
Cannot guarantee ordering across event types - consumers could see EpochCompletedEvent before EpochCreatedEvent due to independent channel buffer states and async timing.

**Alternative Pattern Adopted**: Single coordinator with sequential method calls maintaining natural event ordering.

---

## Usage Guidelines

When a new term is explored during research:
1. Document it here with ❌ status during exploration
2. If adopted → Move to main POC_GLOSSARY.md with ✅ status
3. If dismissed → Update reason and keep in RESEARCH_GLOSSARY.md
4. Reference the specific phase/plan where it was explored

This preserves institutional knowledge while keeping the main glossary focused on current architecture.
