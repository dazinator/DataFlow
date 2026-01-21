# Research Plan: Trigger Context Passing

## Research Objective

Determine the best approach for passing trigger-specific context data to a dataflow and making it available throughout execution.

## Context

Dataflows can be executed in different regimes:

1. **Synchronous trigger** - Web request directly executes dataflow (likely anti-pattern)
2. **Async message** - Message pulled from queue, dataflow processes it
   - Benefits: Durability, scalability, resilience, retry capability
3. **Scheduled trigger** - Cron job or scheduled execution
   - May need to pass specific context (e.g., tenant ID)

## Research Questions

### 1. How to allow dataflow execution in different trigger scenarios?

- Current: `graph.ExecuteAsync(IExecutionContext)` 
- Need to understand what trigger context is needed

### 2. How to supply execution context?

Current state:
- `IExecutionContext` has: CancellationToken, ServiceProvider, InvocationId, RecoveryCheckpoint, Metrics
- `IExecutionContext` does NOT allow arbitrary trigger-specific information

Options to explore:
- **A) Extend IExecutionContext** - Add trigger context as property
- **B) Trigger data as stream item** - Treat trigger request as first data item
- **C) AsyncLocal storage** - Store in AsyncLocal for ambient access
- **D) DI container** - Register trigger context in service provider

### 3. Trigger context as data item vs global invariant?

**Trigger as data item (TIn)**:
- Pros: Type-safe, flows through transforms naturally
- Cons: 
  - Doesn't work with EpochSourceBlocks (no inbound items)
  - Must be copied to all transformed items
  - Increases complexity of stream processing

**Trigger as global invariant**:
- Pros: Available anywhere without threading through stream
- Cons: 
  - Potential AsyncLocal performance overhead
  - Less explicit data flow

### 4. Current actor context access

Questions to answer:
- Can `IStreamActor<TIn,TOut>` obtain execution context?
- How robust is AsyncLocal mechanism?
- What's the performance impact?

### 5. Context propagation architecture

Current contexts:
- `IExecutionContext` - Graph-level, created at ExecuteAsync()
- `IActorExecutionContext` - Actor-level, passed to actors
- `BlockContext` - Constructor-time context
- `ExecutionContext.Current` - AsyncLocal ambient access

## Success Metrics

- **Quantitative**:
  - Performance overhead < 5% for context access
  - Memory overhead minimal (measured)
  
- **Qualitative**:
  - Clear, intuitive API
  - Type-safe where possible
  - Works with all trigger scenarios
  - No breaking changes to existing code

- **Baseline**: 
  - Current execution without trigger context
  - Measure with simple dataflow benchmark

- **Validation**:
  - Prototype working for all 3 trigger scenarios
  - Performance benchmarks show acceptable overhead
  - Code examples demonstrate clarity

## Validation Approach

1. **Analysis Phase**: Document current architecture and all options
2. **Prototype Phase**: Build working prototypes for each approach
3. **Benchmark Phase**: Measure performance of each approach
4. **Comparison Phase**: Create decision matrix with trade-offs

## Expected Outcomes

- Research documentation in `/research/trigger-context-passing/`
- Implementation-ready work item with recommended approach
- Formal ADR documenting decision
- Prototype code demonstrating recommended approach
- Benchmark data supporting decision

## Timeline

- Analysis: 1-2 days
- Prototyping: 2-3 days
- Benchmarking: 1 day
- Documentation: 1 day
- Total: 5-7 days
