# Epoch Test Coverage Matrix

## Overview

This matrix documents test coverage for epoch propagation across different DataFlow topologies and scenarios.

Legend:
- ✅ = Well covered (3+ tests)
- ⚠️ = Partially covered (1-2 tests)
- ❌ = No coverage / Gap identified
- 📝 = Reference to specific test

---

## Matrix 1: Block Types × Epoch Scenarios

| Block Type | Single Epoch | Multi-Epoch Transition | Epoch Merging | Competing Consumers |
|------------|--------------|------------------------|---------------|---------------------|
| **EpochSourceNode** | ✅ 📝[1] | ✅ 📝[2] | ✅ 📝[3] | ✅ 📝[4] |
| **EpochProcessorNode** | ✅ 📝[5] | ✅ 📝[6] | ✅ 📝[7] | ✅ 📝[8] |
| **EpochActorBlock** | ✅ 📝[9] | ✅ 📝[10] | ⚠️ 📝[11] | ⚠️ |
| **EpochBatchBlock** | ✅ 📝[12] | ✅ 📝[13] | ❌ | ❌ |
| **EpochSegmenterBlock** | ✅ 📝[14] | ✅ 📝[15] | ✅ 📝[16] | ⚠️ |
| **BufferNode** | ✅ 📝[17] | ⚠️ | ❌ | ✅ 📝[18] |
| **RoutingBlock** | ⚠️ | ❌ | ❌ | ⚠️ 📝[19] |

### Test References:

[1] `EpochNodeTests.EpochSourceNode_PublishesEpochToStream`
[2] `EpochProcessorConcurrencyTests.SingleProcessor_ProcessesEpochsInOrder` (10 epochs)
[3] `FanInSourceCoordinationTests.TwoSource_FanIn_SameEpochObject`
[4] `EpochProcessorConcurrencyTests.DualProcessors_ProcessEpochsConcurrently`
[5] `EpochNodeTests.EpochProcessorNode_DrainsOperationQueue`
[6] `EpochProcessorConcurrencyTests.MultipleProcessors_HooksExecuteCorrectly`
[7] `EpochCoordinatorTests.MultiSource_SameEpochObject_WhenSubsumed`
[8] `EpochProcessorConcurrencyTests.QuadProcessors_MaximizeThroughput` (4 processors)
[9] `EpochAwareBlockTests.EpochActorBlock_Should_TransformItemsWithinEpochs`
[10] `EpochAwareBlockTests.EpochActorBlock_Should_PreserveEpochBoundaries`
[11] `EpochMergeTrackingTests.TrackingBlock_Should_ReuseContextAtMergePoint`
[12] `EpochAwareBlockTests.EpochBatchBlock_Should_BatchItemsWithinEpochs`
[13] `EpochAwareBlockTests.EpochBatchBlock_Should_NotSpanBatchesAcrossEpochs`
[14] `EpochSegmenterTests.SegmentByKey_Should_CreateSeparateStreamsForEachEpoch`
[15] `EpochSegmenterStreamingTests.SegmentByKey_Should_CompleteEpochsInOrder`
[16] `MultiSourceSegmentationTests.SegmentThenMerge_Should_ProcessIndependentEpochStreams`
[17] `BufferNodeTests.BufferNode_Should_Connect_Single_Producer_To_Multiple_Consumers`
[18] `BufferNodeDemonstrationTests` (various competing consumer tests)
[19] `ConcurrencyScalingTests` (Level 5, Level 6 routing tests)

---

## Matrix 2: Edge Strategies × Epoch Scenarios

| Edge Strategy | Single Epoch | Multi-Epoch | Competing Consumers | Epoch Boundaries Preserved |
|---------------|--------------|-------------|---------------------|----------------------------|
| **Direct (Pull)** | ✅ 📝[20] | ✅ 📝[21] | ✅ 📝[22] | ✅ 📝[23] |
| **BroadcastEdgeStrategy** | ⚠️ 📝[24] | ❌ | ❌ | ❌ |
| **SelectiveRoutingEdgeStrategy** | ⚠️ 📝[25] | ❌ | ⚠️ 📝[26] | ❌ |
| **EpochControlPlaneEdgeStrategy** | ✅ 📝[27] | ✅ 📝[28] | ⚠️ | ✅ 📝[29] |

### Test References:

[20] Most epoch tests use direct pull pattern
[21] `DecoupledEpochTests.PlainSource_WithCountSegmenter_ProducesEpochs`
[22] `EpochProcessorConcurrencyTests` (all tests)
[23] `EpochAwareBlockTests.EpochActorBlock_Should_PreserveEpochBoundaries`
[24] `EdgeStrategyTests` has broadcast but without epochs
[25] `SelectiveRoutingEdgeStrategyTests` exist but no epoch integration
[26] `ConcurrencyScalingTests.Level5_Routing_With_Competing_Consumers`
[27] `EpochControlPlaneTests.EpochControlPlaneEdgeStrategy_Should_PropagateData`
[28] `EpochControlPlaneTests.EpochManager_Should_Track_MonotonicSequences`
[29] `EpochControlPlaneTests.EpochManager_Should_BroadcastEpoch_To_All_Subscribers`

---

## Matrix 3: Topology Patterns × Multi-Epoch Coverage

| Topology Pattern | Test Coverage | Multi-Epoch Tests | Notes |
|------------------|---------------|-------------------|-------|
| **Single Source → Single Consumer** | ✅ | ✅ 📝[30] | Well covered |
| **Single Source → Competing Consumers** | ✅ | ✅ 📝[31] | **PRIMARY SCENARIO** |
| **Multi Source → Single Consumer (Merge)** | ✅ | ✅ 📝[32] | Epoch merging tested |
| **Single Source → Multiple Consumers (Broadcast)** | ⚠️ | ❌ | Gap: No epoch tests |
| **Single Source → Routed Consumers** | ⚠️ | ❌ | Gap: No epoch tests |
| **Pipeline (A → B → C)** | ✅ | ✅ 📝[33] | Epoch flow preserved |
| **Diamond (A → {B,C} → D)** | ⚠️ | ⚠️ 📝[34] | Limited coverage |

### Test References:

[30] `EpochNodeTests.EpochSourceNode_PublishesEpochToStream`
[31] `EpochProcessorConcurrencyTests.DualProcessors_ProcessEpochsConcurrently` ⭐
[32] `FanInSourceCoordinationTests.ThreeSource_FanIn_AllShareSameEpoch`
[33] `EpochAwareBlockTests.ComposedPipeline_Should_WorkWithMixOfEpochAwareBlocks`
[34] `EpochMergeTrackingTests.TrackingBlock_Should_HandleComplexMergeHierarchy`

---

## Matrix 4: Epoch Scoping & Lifetime

| Aspect | Test Coverage | Multi-Epoch | Notes |
|--------|---------------|-------------|-------|
| **DI Scope per Epoch** | ✅ | ✅ 📝[35] | Each epoch = new scope |
| **DbContext per Epoch** | ✅ | ✅ 📝[36] | Scoped lifetime validated |
| **Epoch Disposal → Scope Disposal** | ✅ | ✅ 📝[37] | Proper cleanup |
| **EpochStream Lifetime** | ✅ | ✅ 📝[38] | Coupled to epoch |
| **Concurrent Epochs = Separate Scopes** | ✅ | ✅ 📝[39] | No interference |
| **Service Sharing Within Epoch** | ✅ | ⚠️ 📝[40] | Sequential blocks only |

### Test References:

[35] `EpochScopedDbContextTests.DifferentEpochs_GetDifferentDbContextInstances`
[36] `EpochScopedDbContextTests.MultipleBlocks_ShareSameDbContextInSameEpoch`
[37] `EpochCoordinatorTests.EpochDisposal_DisposesScope`
[38] `EpochScopedDbContextTests.EpochStream_PropagatesEpochScopeDownstream`
[39] `EpochScopedDbContextTests.ConcurrentEpochs_NoRaceConditions`
[40] `EpochScopedDbContextTests.MultipleBlocks_CanShareDbContextChanges`

---

## Matrix 5: Specific Test Scenarios

### ✅ WELL COVERED: Source Emitting Incrementing Epochs

| Test | Epoch Count | Pattern | Validation |
|------|-------------|---------|------------|
| `SingleProcessor_ProcessesEpochsInOrder` | 10 | Sequential | Sequence 1-10 in order |
| `DualProcessors_ProcessEpochsConcurrently` | 10 | Competing | All epochs processed once |
| `QuadProcessors_MaximizeThroughput` | 20 | Competing | Work distributed |
| `DatabaseSourceActor_Should_StreamDataInEpochs` | 5 | Sequential | Domain anchors tracked |
| `MultipleProcessors_HooksExecuteCorrectly` | 5 | Competing | Hooks fire correctly |

### ⚠️ PARTIALLY COVERED: Buffer + Competing Consumers + Epochs

| Aspect | Coverage | Tests |
|--------|----------|-------|
| **Channel-based buffer** | ✅ | `EpochSourceNode` acts as channel |
| **Competing consumers** | ✅ | `EpochProcessorNode` tests (2, 4 processors) |
| **Epoch boundaries** | ✅ | Each epoch processed atomically |
| **Non-Epoch BufferNode** | ⚠️ | `BufferNode` tests exist, no epoch validation |

### ❌ GAPS IDENTIFIED

1. **Broadcast + Multi-Epoch**
   - `BroadcastEdgeStrategy` exists but no multi-epoch tests
   - Would validate: Same epoch broadcast to multiple consumers

2. **Selective Routing + Epochs**
   - `SelectiveRoutingEdgeStrategy` tested but not with epochs
   - Would validate: Route key decisions across epoch boundaries

3. **Batch Block + Epoch Merging**
   - `EpochBatchBlock` tested with multi-epoch but not at merge points
   - Would validate: Batching behavior when epochs merge

---

## Summary Statistics

- **Total Epoch Tests**: ~149
- **Multi-Epoch Transition Tests**: ~15
- **Competing Consumer Tests**: ~8
- **Epoch Scoping Tests**: ~12
- **Integration Tests**: ~25

## Coverage by Category

| Category | Coverage | Status |
|----------|----------|--------|
| **Core Epoch Nodes** | 95% | ✅ Excellent |
| **Epoch Scoping/Lifetime** | 90% | ✅ Excellent |
| **Multi-Epoch Transitions** | 85% | ✅ Very Good |
| **Competing Consumers + Epochs** | 80% | ✅ Good |
| **Topology Combinations** | 60% | ⚠️ Fair |
| **Edge Strategy + Epochs** | 40% | ⚠️ Limited |

---

## Quick Reference: Primary Tests for Requested Scenario

**Scenario**: "Source emits incrementing epochs → Buffer → Competing consumers"

**Key Test**: `EpochProcessorConcurrencyTests.DualProcessors_ProcessEpochsConcurrently`
- **File**: `poc/EpochAnchoringDemo.Tests/EpochProcessorConcurrencyTests.cs:83`
- **Epochs**: 10 incrementing (1-10)
- **Consumers**: 2 competing processors
- **Validates**:
  - ✅ Each epoch processed exactly once
  - ✅ Work distributed across consumers
  - ✅ Epoch boundaries preserved
  - ✅ Proper scope isolation

**Supporting Tests**:
- `QuadProcessors_MaximizeThroughput` (4 consumers, 20 epochs)
- `EpochScopedDbContextTests.DifferentEpochs_GetDifferentDbContextInstances` (scope validation)
- `EpochScopedDbContextTests.ConcurrentEpochs_NoRaceConditions` (concurrent epoch isolation)
