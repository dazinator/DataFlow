# Tech Debt Analysis: Obsolete Constructor Refactoring
## Exploration Notes

**Date**: 2025-11-19
**Analyst**: Copilot (Tech Debt Duty)

## Initial Findings

### Obsolete Constructors Identified

Following PR #480 (DI registration system), three constructors were marked obsolete:

1. **`ActorBlock<TIn, TOut, TActor>(string name, IServiceScopeFactory)`**
   - Location: `/poc/DataFlow.POC/Blocks/ActorBlock.cs:25`
   - Marked obsolete to encourage DI registration via `services.AddDataFlows()`

2. **`BlockBase<TIn, TOut>(string name)`**
   - Location: `/poc/DataFlow.POC/Core/BlockBase.cs:15`
   - Protected constructor used by ALL block types
   - This is the BASE constructor that all blocks inherit

3. **`DataFlowGraphBuilder(string name, ILogger<DataFlowGraph>?)`**
   - Location: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs:31`
   - Legacy constructor for inline graph building

### Build Analysis

**POC Library**: 13 warnings (all internal - block constructors calling `base(name)`)
**Tests**: 0 errors, 0 warnings (tests don't build as standalone, need to verify)
**Benchmarks**: Build errors (unrelated - missing RecoveryCheckpoint interface member)

#### Internal Warnings (Block Definitions)

The 13 warnings in POC library are from block constructors calling the obsolete `base(name)`:

```
- BatchBlock.cs:14
- BroadcastBlock.cs:14
- EnvelopeBlocks.cs:14, 136
- EpochActorBlock.cs:33
- EpochBatchBlock.cs:18
- EpochSegmenterBlock.cs:16
- EpochSourceBlock.cs:21
- PlainSourceBlock.cs:20
- ProducerBlock.cs:14, 43
- RouterBlock.cs:15, 77
```

These are INTERNAL uses - block implementations calling their base class constructor.

#### External Usage Analysis

Need to count actual instantiations:
- ActorBlock instantiations: ~146 (from grep)
- DataFlowGraphBuilder instantiations: ~113 (from grep)
- Files with ActorBlock usage: 22
- Files with DataFlowGraphBuilder usage: 30

### Migration Path Available

PR #480 introduced DI registration system:
- `services.AddDataFlows()` extension method
- `DataFlowBuilder` fluent API
- `UseBlock(name)` method to reference DI-registered blocks
- Parameterless constructors for DI-friendly blocks

### Key Observations

1. **Block base constructor** - The protected `BlockBase(string name)` constructor is called by ALL block types. This is the most widespread usage.

2. **Two migration patterns needed**:
   - **Block definitions**: Update block constructors to use parameterless `base()` instead of `base(name)`
   - **Block instantiations**: Convert from `new ActorBlock("name", ...)` to DI registration

3. **Test impact**: Tests use inline graph building extensively - would need helper methods

4. **Benchmark impact**: Benchmarks have build errors (unrelated) but also use inline building

## Next Steps

1. Count actual external usages (tests + benchmarks)
2. Analyze test patterns for helper consolidation opportunities
3. Document migration strategy
4. Assess breaking change impact
5. Create findings report
