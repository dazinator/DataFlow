# Research Plan: Separation of Epoch Streams from Source

## Research Objective

Investigate whether epoch segmentation should be controlled by source blocks (current source-centric design) or be decoupled into a separate component that applies segmentation policies externally (decoupled design).

The key question: **Should sources emit `IEpochStream<T>` or plain `IAsyncEnumerable<T>`, with epoch segmentation applied as an external concern?**

## Background Context

### Current Design (Source-Centric)
- Sources emit `IAsyncEnumerable<IEpochStream<T>>` directly
- `EpochSourceBlock<T, TActor>` wraps source actors that produce epoch streams
- `ISourceActor<T>.ProduceEpochsAsync()` is responsible for epoch boundaries
- `EpochSegmenter` utility class exists but is used within source actors
- Epoch control is tightly coupled to domain batching logic

### Design Discussion Insight
Recent discussions questioned whether source-centric epoching:
- Tightly couples segmentation to domain batching
- Makes it hard to generalize across sources
- Complicates coordination across multiple sources
- Limits flexibility in testing alternate segmentation strategies

## Research Questions

### 1. Benefits Analysis
**Question**: What are the concrete benefits of separating epoch concerns from the source block?

**Sub-questions**:
- Does it improve composability of sources across different pipelines?
- Does it enable reusability of sources without epoch knowledge?
- Does it make testing simpler (test source logic independent of epoch logic)?
- Does it allow dynamic segmentation policy changes?
- Does it simplify source implementation complexity?

### 2. Impact on Subsystems
**Question**: How does this change impact other subsystems?

**Sub-questions**:
- **EpochVector**: Does the ancestry and merge logic remain unchanged?
- **Lifecycle Events**: How do `OnEpochCreatedAsync`, `OnEpochCompletedAsync`, and `OnGlobalEpochAlignedAsync` work?
- **EfCore Tracking**: How does per-epoch DbContext management work in the POC example?
- **CompletionBasedEpochProgress**: Does progress tracking still work correctly?
- **GlobalEpochAlignment**: Does watermark calculation remain correct?
- **Metrics and Observability**: How are epochs tracked for metrics?

### 3. Performance Impact
**Question**: Does decoupling have negative performance effects?

**Sub-questions**:
- What is the overhead of an additional block in the pipeline?
- Does it introduce extra allocations or buffering?
- How does it affect throughput in micro-benchmarks?
- How does it affect throughput with realistic workloads (e.g., 30ms processing delay per item)?
- What is the memory overhead?
- What is the CPU overhead?

### 4. Design Trade-offs
**Question**: What are the ergonomic and architectural trade-offs?

**Sub-questions**:
- API clarity: Is it more or less intuitive?
- Configuration: Where do segmentation policies live?
- Error handling: How do errors in segmentation propagate?
- Debugging: Is it easier or harder to debug?
- Documentation: What needs to change?

### 5. Implementation Viability
**Question**: Given the research findings, is this something we want to approve for implementation?

**Evaluation Criteria**:
- Net benefit vs. cost
- Migration path from current design
- Breaking change impact
- Long-term maintainability

## Validation Approach

### Phase 1: Code Analysis
1. Analyze current `EpochSourceBlock` and source actor patterns
2. Analyze existing `EpochSegmenter` utility usage
3. Map out all subsystem dependencies on epoch streams
4. Document current behavior comprehensively

### Phase 2: Prototype Development
1. Create `EpochSegmenterBlock` as a pipeline block
2. Implement segmentation policies:
   - **None**: Pass-through without epochs (plain `IAsyncEnumerable<T>`)
   - **Count**: Segment by item count
   - **Time**: Segment by time window
   - **Key**: Segment by key selector (existing)
   - **Custom**: Custom policy interface
3. Modify source blocks to emit plain `IAsyncEnumerable<T>`
4. Connect sources → segmenter → rest of pipeline

### Phase 3: Subsystem Impact Analysis
1. Test with existing EfCore tracking block example
2. Test with lifecycle events
3. Test with global alignment and watermarks
4. Test with multi-source fan-in scenarios
5. Document "before" vs "after" behavior for each subsystem
6. Create mermaid diagrams showing flow differences

### Phase 4: Performance Benchmarking
1. **Micro-benchmarks** (pure throughput):
   - Source-centric: Producer → Transform → Sink
   - Decoupled: Producer → Segmenter → Transform → Sink
   - Measure: ops/sec, allocations, CPU
   
2. **Realistic workload benchmarks** (simulated processing):
   - Add 30ms delay per item to simulate real work
   - Compare throughput with and without segmentation
   - Measure impact on end-to-end latency
   
3. **Memory benchmarks**:
   - Measure memory usage with different epoch sizes
   - Sequential vs overlapped execution policy
   
4. **Scenarios**:
   - Small epochs (100 items)
   - Medium epochs (1000 items)
   - Large epochs (10000 items)
   - No epochs (pass-through mode)

### Phase 5: API Ergonomics Comparison
1. Code examples: before vs after
2. Common use cases: ease of implementation
3. Error scenarios: clarity of error messages
4. Documentation burden: what needs explanation

## Expected Outcomes

1. **Research Documentation**: Comprehensive analysis in `/research/epoch-stream-separation/README.md`
2. **Design Documentation**: 
   - Architecture overview with mermaid diagrams
   - API design for `EpochSegmenterBlock`
   - Subsystem integration patterns
3. **ADRs**:
   - Decision on source-centric vs decoupled approach
   - Rationale with pros/cons from research
4. **Benchmark Results**: Performance data with analysis
5. **Implementation Issue**: If viable, complete handover issue for implementation team
6. **Impact Analysis Document**: Detailed before/after behavior for all subsystems

## Timeline

- **Week 1**: Code analysis, prototype development, subsystem mapping
- **Week 2**: Benchmarking, impact analysis, documentation
- **Week 3**: Research findings, ADRs, implementation issue (if viable)

## Success Criteria

- [ ] All research questions answered with data
- [ ] Prototype validates technical feasibility
- [ ] Performance benchmarks show quantified impact
- [ ] Subsystem impacts fully documented
- [ ] API ergonomics evaluated with examples
- [ ] Clear recommendation with rationale
- [ ] Complete implementation-ready issue (if recommended)
- [ ] All exploratory code changes reverted (after reviewer approval)
