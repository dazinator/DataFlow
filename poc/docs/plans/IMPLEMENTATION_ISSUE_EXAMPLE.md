# Example Implementation Issue: Distributed Epoch Coordination

This is an example of an implementation-ready GitHub issue created after POC research. It demonstrates how to use the `IMPLEMENTATION_ISSUE_TEMPLATE.md`.

---

# Distributed Epoch Coordination for Multi-Node DataFlow

## Context and Objectives

### Problem Statement
The current DataFlow implementation assumes a single-node execution model. To support distributed execution across multiple nodes, we need a coordination mechanism to ensure epoch boundaries are synchronized across the distributed system. Without this, checkpoint consistency and exactly-once processing guarantees cannot be maintained across node boundaries.

### Research Background
Research was conducted to validate coordination approaches and identify the optimal solution for distributed epoch synchronization.

**Research Documentation**: `/research/distributed-epoch-coordination-research.md`

Key findings from research:
- Centralized coordination provides best performance (900-1000 ops/sec) but creates coordination bottleneck
- Consensus-based coordination eliminates bottleneck but adds latency (15-20% slower)
- Hybrid approach: centralized for normal case, consensus for failure scenarios
- Vector clock synchronization required for partial ordering across nodes
- Tested with 3, 5, and 10 node configurations

### Objectives
What this implementation should achieve:
- [ ] Enable epoch coordination across multiple DataFlow execution nodes
- [ ] Maintain checkpoint consistency across distributed system
- [ ] Support graceful degradation when coordinator is unavailable
- [ ] Provide observable metrics for coordination health
- [ ] Integrate with existing epoch lifecycle without breaking changes

## Implementation Guidance

### Recommended Approach
Implement a hybrid coordination model validated during research:

1. **Normal Operation**: Lightweight centralized coordinator distributes epoch boundaries
2. **Failure Mode**: Fall back to consensus protocol (Raft) when coordinator unavailable
3. **Vector Clocks**: Track causality and partial ordering across nodes
4. **Heartbeat Protocol**: Detect coordinator failures within 5 seconds

**Key Principles**:
1. Optimize for the common case (centralized) while handling failures (consensus)
2. Use vector clocks to maintain partial ordering without full synchronization
3. Make coordination pluggable to support different deployment models
4. Fail safe: if coordination fails, pause processing rather than risk inconsistency

### Design References
Supporting documentation created during research:
- **Design Document**: `/poc/docs/design/distributed-epoch-architecture.md`
- **Architecture Decision Record**: `/poc/docs/adr/2025-11-04-hybrid-epoch-coordination.md`
- **Research Findings**: `/research/distributed-epoch-coordination-research.md`

### API/Interface Design
Interfaces validated during research prototyping:

```csharp
// Core coordination interface
public interface IEpochCoordinator
{
    /// <summary>
    /// Proposes a new global epoch boundary
    /// </summary>
    Task<EpochBoundary> ProposeEpochBoundaryAsync(
        NodeId proposer, 
        VectorClock vectorClock,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Commits an epoch boundary across all nodes
    /// </summary>
    Task<CommitResult> CommitEpochBoundaryAsync(
        EpochBoundary boundary,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the current coordination mode (Centralized/Consensus)
    /// </summary>
    CoordinationMode CurrentMode { get; }
}

// Node-local epoch manager integration
public interface IDistributedEpochManager
{
    /// <summary>
    /// Notifies of global epoch boundary from coordinator
    /// </summary>
    Task OnGlobalEpochBoundaryAsync(
        EpochBoundary boundary,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets local vector clock for this node
    /// </summary>
    VectorClock GetVectorClock();
}

// Vector clock for causality tracking
public sealed class VectorClock
{
    public Dictionary<NodeId, long> Clocks { get; }
    
    public void Increment(NodeId node);
    public bool HappenedBefore(VectorClock other);
    public VectorClock Merge(VectorClock other);
}
```

### Component Architecture
High-level component structure validated during research:

```
┌─────────────────────────────────────────────────────┐
│                  Coordinator Node                    │
│  ┌─────────────────────────────────────────────┐   │
│  │       CentralizedEpochCoordinator           │   │
│  │  - Receives proposals from nodes            │   │
│  │  - Distributes global epoch boundaries      │   │
│  │  - Maintains heartbeat with all nodes       │   │
│  └─────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
    ┌────────┐      ┌────────┐      ┌────────┐
    │ Node 1 │      │ Node 2 │      │ Node 3 │
    │        │      │        │      │        │
    │ Local  │      │ Local  │      │ Local  │
    │ Epoch  │      │ Epoch  │      │ Epoch  │
    │ Manager│      │ Manager│      │ Manager│
    │        │      │        │      │        │
    │ Vector │      │ Vector │      │ Vector │
    │ Clock  │      │ Clock  │      │ Clock  │
    └────────┘      └────────┘      └────────┘
    
    On Coordinator Failure:
    ┌─────────────────────────────────────────┐
    │   Consensus Protocol (Raft)             │
    │   - Nodes elect new coordinator         │
    │   - Continues epoch coordination        │
    └─────────────────────────────────────────┘
```

### Key Implementation Considerations

1. **Coordinator Failure Detection**
   - Research showed 5-second heartbeat interval provides good balance
   - Use exponential backoff for reconnection (validated in prototype)
   - Critical: Don't commit local epochs during coordinator failure until consensus reached

2. **Vector Clock Synchronization**
   - Research found that piggyback vector clocks on epoch boundary messages
   - No separate synchronization needed - reduces network overhead by 40%
   - Store only deltas, not full clocks (memory optimization from research)

3. **Backward Compatibility**
   - Single-node mode must continue to work without any coordination overhead
   - Use factory pattern to inject coordinator only in distributed mode
   - Research validated zero overhead for single-node case

4. **Consensus Protocol Choice**
   - Research compared Paxos, Raft, and custom protocol
   - **Recommendation**: Use Raft (most mature .NET implementations available)
   - DotNext.Net.Cluster provides production-ready Raft implementation

### Reusable Patterns/Code
Patterns validated during research prototyping:

```csharp
// Pattern for proposing epoch boundaries with retry
public async Task<EpochBoundary> ProposeWithRetryAsync(
    NodeId proposer,
    VectorClock clock,
    CancellationToken cancellationToken)
{
    const int maxRetries = 3;
    var delay = TimeSpan.FromMilliseconds(100);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await _coordinator.ProposeEpochBoundaryAsync(
                proposer, clock, cancellationToken);
        }
        catch (CoordinatorUnavailableException) when (i < maxRetries - 1)
        {
            await Task.Delay(delay, cancellationToken);
            delay *= 2; // Exponential backoff
        }
    }
    
    // Research showed falling back to consensus after retries
    return await _consensusProtocol.ProposeEpochBoundaryAsync(
        proposer, clock, cancellationToken);
}
```

### Integration Points
How this integrates with existing codebase:

- **DataFlowBuilder**: Add `.UseDistributedCoordination()` extension method
- **EpochManager**: Inject `IEpochCoordinator` dependency (optional, null for single-node)
- **Metrics**: Integrate with existing `IDataFlowMetrics` for coordination metrics
- **DI Registration**: Add `.AddDistributedEpochCoordination()` to service collection

## Testing and Validation

### Test Coverage Required
Test scenarios identified and validated during research:

#### Unit Tests

1. **Test Scenario: Vector Clock Causality**
   - Setup: Create two vector clocks with different node updates
   - Expected: `HappenedBefore` correctly determines causality
   - Why: This is fundamental to distributed correctness (discovered edge case in research with concurrent updates)

2. **Test Scenario: Coordinator Proposal Handling**
   - Setup: Multiple nodes propose epoch boundaries concurrently
   - Expected: Coordinator serializes proposals and maintains order
   - Why: Research found race conditions without proper serialization

3. **Test Scenario: Heartbeat Failure Detection**
   - Setup: Coordinator stops sending heartbeats
   - Expected: Nodes detect failure within 6 seconds (5s heartbeat + 1s grace)
   - Why: Critical for timely failover validated in research

#### Integration Tests

1. **Integration Scenario: 3-Node Epoch Coordination**
   - Setup: Three nodes with centralized coordinator
   - Expected: Epochs advance consistently across all nodes
   - Validation: Compare epoch vectors after 1000 operations
   
2. **Integration Scenario: Coordinator Failover**
   - Setup: 5 nodes, kill coordinator during processing
   - Expected: Consensus protocol elects new coordinator within 10 seconds
   - Validation: Processing resumes without data loss

3. **Integration Scenario: Network Partition**
   - Setup: Create network partition isolating 2 of 5 nodes
   - Expected: Majority partition continues, minority pauses
   - Validation: Ensure no split-brain scenario

### Performance Validation
Performance requirements based on research benchmarks:

- **Throughput**: 900+ operations/second (3-node configuration)
- **Coordination Latency**: p50 < 5ms, p99 < 15ms (centralized mode)
- **Failover Time**: < 10 seconds to resume after coordinator failure
- **Memory Overhead**: < 10MB per node for coordination state

**Benchmark Scenarios**:
1. **Baseline Performance**: Single node (should be unchanged from current)
2. **3-Node Distributed**: Measure coordination overhead vs baseline
3. **10-Node Distributed**: Validate scalability with more nodes
4. **Failure Recovery**: Measure time from coordinator failure to recovery

**Reference**: See detailed benchmark methodology in `/research/distributed-epoch-coordination-research.md`

### Edge Cases
Edge cases discovered during research prototyping:

1. **Edge Case: Concurrent Coordinator Election**
   - Description: Multiple nodes simultaneously detect coordinator failure
   - How to handle: Raft consensus handles this naturally (validated in research)
   - Test coverage: Unit test for election protocol, integration test with controlled failure

2. **Edge Case: Vector Clock Overflow**
   - Description: Long-running nodes may overflow vector clock counters
   - How to handle: Use 64-bit counters, reset protocol for overflow (documented in research)
   - Test coverage: Unit test simulating 2^63 increments

3. **Edge Case: Delayed Messages After Partition Heal**
   - Description: Epoch boundary messages from before partition arrive after healing
   - How to handle: Use vector clock to detect and discard stale messages
   - Test coverage: Integration test with simulated partition and healing

## Constraints and Requirements

### Technical Constraints
- **Constraint: No Breaking Changes**: Must work with existing single-node DataFlows without modification
- **Constraint: .NET 8.0**: Implementation must target .NET 8.0 (current target)
- **Constraint: Async-First**: All coordination operations must be async (consistent with existing code)

### Performance Requirements
- Single-node mode must have zero coordination overhead (validated in research)
- 3-node distributed mode should achieve 80%+ of single-node throughput
- Coordination latency should not exceed 20ms p99

### Compatibility Requirements
- **Backward Compatibility**: Existing DataFlows must run unchanged
- **API Stability**: New APIs follow semantic versioning
- **.NET Version**: Target .NET 8.0

### Dependencies
New dependencies validated during research:
- **DotNext.Net.Cluster**: Version 5.x - Production-ready Raft implementation
  - Validated in research: stable, well-maintained, good performance
  - License: MIT (compatible with AGPL-3.0)

## Alternatives Explored

During research, multiple approaches were evaluated:

### Alternative 1: Pure Centralized Coordination
**Description**: Single coordinator, no consensus fallback
**Pros**: 
- Simplest implementation
- Best performance (1000 ops/sec in research)
- Lowest coordination latency
**Cons**: 
- Single point of failure
- No automatic recovery from coordinator failure
**Why Not Chosen**: Unacceptable availability characteristics for production use. Research showed 10+ minute recovery times for manual coordinator restart.

### Alternative 2: Pure Consensus-Based Coordination
**Description**: All epoch boundaries decided through Raft consensus
**Pros**: 
- No single point of failure
- Automatic recovery from node failures
- Proven in distributed systems
**Cons**: 
- Higher latency (20ms p99 vs 5ms for centralized)
- Lower throughput (800 ops/sec vs 1000 ops/sec)
- More complex implementation
**Why Not Chosen**: Performance overhead not justified for common case where coordinator is healthy. Research showed coordinator failure is rare (< 1% of time in realistic scenarios).

### Alternative 3: Gossip-Based Coordination
**Description**: Nodes gossip epoch boundaries, eventually consistent
**Pros**: 
- No coordinator needed
- Scales well with node count
- Simple implementation
**Cons**: 
- Eventual consistency incompatible with exactly-once guarantees
- Research found 30-60 second convergence times unacceptable
- Difficult to reason about correctness
**Why Not Chosen**: Cannot provide strong consistency guarantees required for exactly-once processing.

## References and Resources

### Documentation
All supporting documentation created during research:
- **Research Report**: `/research/distributed-epoch-coordination-research.md`
- **Design Document**: `/poc/docs/design/distributed-epoch-architecture.md`
- **Architecture Decision Record**: `/poc/docs/adr/2025-11-04-hybrid-epoch-coordination.md`
- **Glossary**: See terms in `/poc/docs/POC_GLOSSARY.md`:
  - Vector Clock
  - Epoch Boundary
  - Coordination Mode
  - Consensus Protocol

### Prior Work
Related issues and PRs:
- **Research Issue**: #123 - "Investigate Distributed Epoch Coordination"
- **Research PR**: #456 - "Research documentation for distributed epochs"

### External References
External documentation or resources referenced during research:
- Raft Consensus Protocol: https://raft.github.io/
- DotNext.Net.Cluster Documentation: https://dotnet.github.io/dotNext/
- Vector Clocks Explained: Lamport, "Time, Clocks, and the Ordering of Events in a Distributed System"

## Implementation Phases

Implementation should be phased to reduce risk:

### Phase 1: Core Abstractions and Single-Node Pass-Through
- [ ] Implement `IEpochCoordinator` interface
- [ ] Create pass-through implementation for single-node mode
- [ ] Add DI registration and configuration
- [ ] Ensure zero overhead for single-node case
- **Goal**: Infrastructure in place, existing functionality unchanged

### Phase 2: Centralized Coordinator Implementation
- [ ] Implement `CentralizedEpochCoordinator`
- [ ] Add heartbeat protocol
- [ ] Implement vector clock synchronization
- [ ] Add coordinator metrics
- **Goal**: Basic distributed coordination working in happy path

### Phase 3: Consensus Fallback
- [ ] Integrate DotNext.Net.Cluster for Raft consensus
- [ ] Implement failure detection
- [ ] Implement coordinator failover logic
- [ ] Add failover metrics
- **Goal**: Fault-tolerant distributed coordination

### Phase 4: Testing and Validation
- [ ] Comprehensive unit test suite
- [ ] Integration tests with multiple nodes
- [ ] Chaos engineering tests (network failures, etc.)
- [ ] Performance benchmarks
- **Goal**: Production-ready quality

## Success Criteria

This implementation is complete when:

- [ ] All objectives are met
- [ ] Test coverage > 80% for coordination code
- [ ] Performance requirements are met (validated with benchmarks)
- [ ] All edge cases are handled with tests
- [ ] Documentation is updated (user guide + API docs)
- [ ] Code review is complete
- [ ] CI/CD pipeline passes all tests and benchmarks
- [ ] Single-node performance unchanged from baseline (< 1% regression)

## Questions for Implementation Team

1. Should we support custom consensus protocols beyond Raft? (Research only validated Raft)
2. What is the preferred approach for integration tests requiring multiple processes? (Docker, in-process simulation, etc.)
3. Should coordinator assignment be automatic or configured? (Research assumed configured)

## Notes

**Important**: The vector clock implementation is critical for correctness. Research discovered subtle bugs in naive implementations. Pay careful attention to the concurrent increment and merge operations.

**Performance**: Research used .NET 8.0 on Linux (Ubuntu 22.04). Windows performance was 5-10% lower due to async I/O differences.

**Monitoring**: Add comprehensive metrics from day one. Research showed debugging distributed coordination issues without metrics is extremely difficult.

---

**Created**: 2025-11-04
**Research Issue**: #123
**Research PR**: #456
