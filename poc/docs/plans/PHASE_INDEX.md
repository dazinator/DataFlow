# Phase Index

> **⚠️ HISTORICAL DOCUMENTATION**: This index documents the POC development phases (Phase 2-6). The patterns described here have evolved into the **formalized epoch system**. See [ADR: Formalized Epoch System](../../../docs/adr/poc/2025-11-16-formalized-epoch-system.md) for the current architecture.

> **Note on Terminology:** The "phases" indexed here (Phase 2-6) refer to **POC development milestones** on the path to production readiness. These document the technical evolution of the epoch-based coordination system and are distinct from any documentation reorganization phases.

## Overview

This index provides navigation through the historical phase documentation. Phases represent the chronological evolution of the epoch-based coordination system from initial investigation through to the composable lifecycle model toward production readiness.

## Phase Timeline

```
Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6
Control   Stream    Source    EF Core   Lifecycle
Signals   Epochs    Actors    Anchoring Events
```

## Phase 2: Control Signal Investigation

**Files:**
- [PHASE2_CONTROL_SIGNAL_INVESTIGATION.md](./PHASE2_CONTROL_SIGNAL_INVESTIGATION.md)
- [PHASE2_SUMMARY.md](./PHASE2_SUMMARY.md)

**Focus:** Investigation of out-of-band control signal propagation for epoch boundaries

**Key Findings:**
- Control signals (epoch markers) sent separately from data
- Identified **premature alignment bug** - epochs acknowledged before data fully processed
- Led to the realization that data and control must be synchronized

**Status:** ✅ Complete - Led to Phase 3's stream-per-epoch model

**What Evolved:**
- Control signals replaced by `IEpochStream<T>` (see [concepts/epochs.md](../concepts/epochs.md))
- Premature alignment problem solved by natural stream completion boundaries

## Phase 3: Epoch Stream Segmentation

**Files:**
- [PHASE3_EPOCH_STREAM_SEGMENTATION.md](./PHASE3_EPOCH_STREAM_SEGMENTATION.md)
- [PHASE3_SUMMARY.md](./PHASE3_SUMMARY.md)
- [PHASE3_PART1_PERFORMANCE_REPORT.md](./PHASE3_PART1_PERFORMANCE_REPORT.md)
- [PHASE3_PART1_SUMMARY.md](./PHASE3_PART1_SUMMARY.md)

**Focus:** Stream-per-epoch model and epoch vector coordination

**Key Contributions:**
- **EpochVector** - Multi-source epoch tracking with vector clock semantics
- **IEpochStream<T>** - Epoch-bounded substreams with natural completion
- **Vector operations** - Merge (element-wise max), comparison, subsumption
- **Correct alignment** - No more premature completion

**Status:** ✅ Complete - Foundation for all subsequent phases

**Core Abstractions:**
- `EpochVector` - Multi-source epoch identifier
- `IEpochStream<T>` - Stream segment belonging to an epoch
- `EpochSegmenter` - Converts continuous streams to epoch streams

**What Evolved:**
- Core concepts documented in [concepts/epochs.md](../concepts/epochs.md) and [concepts/epoch-vectors.md](../concepts/epoch-vectors.md)

## Phase 4: Source Actor and Streaming Buffers

**Files:**
- [PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md](./PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md)

**Focus:** Stateless source actor pattern and streaming buffer optimization

**Key Contributions:**
- **ISourceActor<T>** - Interface for epoch-emitting sources
- **SourceActorBase<T>** - Base implementation with epoch creation helpers
- **Streaming buffers** - Memory-efficient epoch buffering strategies
- **Stateless sources** - Sources emit data, don't manage transactions

**Status:** ✅ Complete - Enables flexible source implementations

**Design Principles:**
- Sources should be pure data producers
- Transaction management belongs downstream
- Separation of concerns for composability

**What Evolved:**
- Source actor pattern integrated into main library
- Stateless principle emphasized in [guides/creating-tracking-blocks.md](../guides/creating-tracking-blocks.md)

## Phase 5: EF Core Anchoring Demo

**Files:**
- [PHASE5_EFCORE_ANCHORING_DEMO.md](./PHASE5_EFCORE_ANCHORING_DEMO.md)

**Focus:** Global epoch alignment and checkpoint boundary calculation

**Key Contributions:**
- **GlobalEpochAlignment** - Tracks per-block completion progress
- **GetGlobalCompletionWatermark()** - Calculates safe checkpoint boundary (min across all blocks)
- **IsGloballyAligned(EpochVector)** - Checks if epoch completed by all blocks
- **CompletionBasedEpochProgress.cs** - Reference implementation

**Status:** ✅ Complete - Provides alignment infrastructure

**Core Infrastructure:**
- Per-block completion tracking
- Watermark calculation (element-wise minimum across blocks)
- Alignment queries for safe transaction boundaries

**What Evolved:**
- Global alignment concepts documented in [concepts/global-alignment.md](../concepts/global-alignment.md)
- Transaction boundary safety in [concepts/transaction-boundaries.md](../concepts/transaction-boundaries.md)

## Phase 6: Epoch Lifecycle Model

**Files:**
- [PHASE6_EPOCH_LIFECYCLE.md](./PHASE6_EPOCH_LIFECYCLE.md)

**Focus:** Composable lifecycle event interfaces for epoch coordination

**Key Contributions:**
- **IEpochLifecycleParticipant** - Interface for lifecycle event notifications
- **EpochLifecycleCoordinator** - Manages registration and event broadcast
- **EntityTrackingBlock pattern** - Composable per-epoch transaction management
- **Context promotion** - Handles epoch vector merging at fan-in points

**Status:** ✅ Complete - Enables event-driven coordination

**Lifecycle Events:**
- `OnEpochCreatedAsync` - Block starts processing epoch
- `OnEpochCompletedAsync` - Block finishes processing epoch (per-block event)
- `OnGlobalEpochAlignedAsync` - All blocks complete epoch (safe transaction boundary)

**Key Patterns:**
- **Stateless sources** - Pure data producers
- **Downstream tracking blocks** - Manage per-epoch DbContext, commit at alignment
- **Multi-sink coordination** - Multiple tracking blocks coordinate via global alignment
- **Context promotion** - Reuse ancestor contexts during merges to avoid fragmentation

**What Evolved:**
- Lifecycle concepts in [concepts/lifecycle-events.md](../concepts/lifecycle-events.md)
- Tracking block pattern in [guides/creating-tracking-blocks.md](../guides/creating-tracking-blocks.md)
- Transaction safety in [concepts/transaction-boundaries.md](../concepts/transaction-boundaries.md)

## Cross-Cutting Themes

### Separation of Concerns
**Phases 4, 6** - Sources emit data, downstream blocks manage transactions

**Current Documentation:**
- [Transaction Boundaries](../concepts/transaction-boundaries.md)
- [Creating Tracking Blocks](../guides/creating-tracking-blocks.md)

### Multi-Source Coordination
**Phases 3, 5, 6** - EpochVector enables coordination across multiple sources

**Current Documentation:**
- [Epoch Vectors](../concepts/epoch-vectors.md)
- [Global Alignment](../concepts/global-alignment.md)

### Transaction Safety
**Phases 5, 6** - Global alignment provides safe checkpoint boundaries

**Current Documentation:**
- [Transaction Boundaries](../concepts/transaction-boundaries.md)
- [Global Alignment](../concepts/global-alignment.md)

### Composability
**Phases 4, 6** - Flexible pipeline topologies with downstream transaction management

**Current Documentation:**
- [Creating Tracking Blocks](../guides/creating-tracking-blocks.md)

## Migration Path: From Phases to Current Docs

If you're reading a phase document and want to find the current documentation:

| Phase Concept | Current Location |
|---------------|------------------|
| Control signals → Epoch streams | [concepts/epochs.md](../concepts/epochs.md) |
| EpochVector operations | [concepts/epoch-vectors.md](../concepts/epoch-vectors.md) |
| Global alignment, watermarks | [concepts/global-alignment.md](../concepts/global-alignment.md) |
| IEpochLifecycleParticipant | [concepts/lifecycle-events.md](../concepts/lifecycle-events.md) |
| EntityTrackingBlock pattern | [guides/creating-tracking-blocks.md](../guides/creating-tracking-blocks.md) |
| Transaction boundaries | [concepts/transaction-boundaries.md](../concepts/transaction-boundaries.md) |

## Reading Guide

### For Understanding Evolution
Read phases in chronological order (2 → 3 → 4 → 5 → 6) to see how the system evolved.

### For Specific Topics
Use the table above to jump directly to the phase that introduced a concept, then read the current documentation.

### For Design Rationale
Phases contain the "why" behind decisions. Use them to understand:
- What alternatives were considered
- Why certain approaches were rejected
- What problems each phase solved

### For Current Implementation
**Don't use phases as implementation reference.** Use:
- `/concepts` - For conceptual understanding
- `/guides` - For implementation patterns
- `/reference` - For API details

## Future Phases

Phase 7 and beyond are outlined in [FUTURE_ENHANCEMENTS.md](../FUTURE_ENHANCEMENTS.md).

Potential future phases:
- Automatic lifecycle integration
- Built-in tracking block implementations
- Advanced coordination patterns
- Distributed transaction support
- Checkpoint and recovery mechanisms

---

For current, stable documentation, see [INDEX.md](../INDEX.md).
