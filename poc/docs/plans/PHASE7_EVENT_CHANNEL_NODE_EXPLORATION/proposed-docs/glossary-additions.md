# Phase 7 Glossary Additions

## Terms to Add to Main Glossary

### EpochLifecycleNode ✅ (RECOMMENDED FOR ADOPTION)
**Category**: Graph Node  
**Phase**: 7  
**Status**: Recommended

A hybrid graph node that wraps `EpochLifecycleCoordinator` to make lifecycle event subscriptions visible in the graph topology while preserving proven coordinator semantics. The node is **automatically created and initialized** when the first block implementing `IEpochLifecycleParticipant` is added to the graph (lazy initialization). All subsequent participant blocks are automatically registered.

**Benefits**:
- **Zero-boilerplate**: No explicit initialization required - node created automatically when needed
- Graph-visible event dependencies (appears in diagrams)
- Automatic participant registration when blocks are added
- Preserves strict event ordering
- No graph execution refactoring required
- Instance-scoped per graph
- Minimal overhead (single object allocation per graph, only when participants exist)

**Usage**:
```csharp
// No explicit LifecycleNode() call needed!
builder.AddBlock(trackingBlock);    // If implements IEpochLifecycleParticipant, node is auto-created
builder.AddBlock(processorBlock);   // Regular block, no lifecycle events
builder.AddBlock(checkpointBlock);  // If implements IEpochLifecycleParticipant, auto-registered to existing node
```

**Implementation Details**:
- Lazy initialization: Node created on first `IEpochLifecycleParticipant` block
- Automatic registration: All participant blocks auto-subscribe when added
- Silent when unused: If no participant blocks exist, no node is created
- No exceptions: Missing initialization cannot occur

**See Also**: EpochLifecycleCoordinator, IEpochLifecycleParticipant

---

## Terms to Add to Research Glossary

### EventChannelNode ❌ (Phase 7 - Not Adopted)
**Explored**: 2025-11-03  
**Status**: Dismissed in favor of EpochLifecycleNode  
**Reason**: Ordering problems, implementation complexity (~10h refactoring), over-engineering  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Channel-based graph node for event distribution using typed channels per event type. Events would flow through channels like data items.

**Why Dismissed**:
1. **Ordering**: Separate channels couldn't guarantee strict event ordering across types
2. **Complexity**: Required graph execution refactoring, 450 LOC vs 100 LOC hybrid approach
3. **Over-Engineering**: Event use cases are source→consumers only (1 level deep)

**Alternative**: EpochLifecycleNode (hybrid coordinator-wrapped approach)

---

### Sequential Event Delivery ❌ (Phase 7 - Concept Explored)
**Explored**: 2025-11-03  
**Status**: Concept valid but implementation approach dismissed  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Pattern for delivering events to multiple consumers one at a time in order. Explored via `SequentialEventEdgeStrategy` but ultimately coordinator-based delivery was retained.

**Why Dismissed**: Channel-based event strategies deemed unnecessary when coordinator already provides sequential delivery.

---

### Broadcast Event Delivery ❌ (Phase 7 - Concept Explored)
**Explored**: 2025-11-03  
**Status**: Concept valid but implementation approach dismissed  
**Reference**: [Phase 7 Archived](/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/)

Pattern for delivering events to multiple consumers simultaneously in parallel. Explored via `BroadcastEventEdgeStrategy` but ultimately coordinator-based delivery was retained.

**Why Dismissed**: Channel-based event strategies deemed unnecessary; coordinator can handle both sequential and broadcast patterns if needed.
