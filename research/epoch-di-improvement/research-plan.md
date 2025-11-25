# Research Plan: Epoch DI Improvement

## Research Objective

Improve the ergonomics of the `ConfigureEpochs` API by removing the awkward requirement for developers to manually provide an `IServiceScopeFactory` dependency when configuring epochs.

**Current State (Awkward)**:
```csharp
services.AddDataFlows("orders", df =>
{
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             config.SetPolicy(EpochPolicy.ByCount(1000));
             config.AddProcessor("order-processor");
             config.SetHooks(new EpochHooks {
                 OnBeginEpoch = async (epoch, ct) => { /* tx */ },
                 OnCommitEpoch = async (epoch, ct) => { /* commit */ }
             });
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
         //^^^ This factory is out of place and requires developer to understand internals
    });
});
```

**Desired State (Clean)**:
```csharp
services.AddDataFlows("orders", df =>
{
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             config.SetPolicy(EpochPolicy.ByCount(1000));
             config.AddProcessor("order-processor");
             config.SetHooks(new EpochHooks {
                 OnBeginEpoch = async (epoch, ct) => { /* tx */ },
                 OnCommitEpoch = async (epoch, ct) => { /* commit */ }
             });
         });
         // No factory needed - DI handles it automatically
    });
});
```

## Research Questions

1. **Where is the `IServiceProvider` available in the call chain?**
   - Is it available in `DataFlowGraphBuilder`?
   - Is it passed through from the service registration layer?

2. **Can we inject `EpochCoordinator` directly through DI?**
   - Should `EpochCoordinator` be registered as a singleton, scoped, or transient service?
   - What are the lifetime implications given that it needs `IServiceScopeFactory`?

3. **Can we make the factory optional with a sensible default?**
   - Provide a default factory that uses `IServiceScopeFactory` from DI
   - Allow advanced users to override if needed

4. **Are there other coordinator implementations we need to support?**
   - Check if `IEpochCoordinator` has multiple implementations
   - Understand if the factory pattern is needed for extensibility

5. **What is the lifecycle of `EpochCoordinator`?**
   - Is it per-graph, per-flow, or shared?
   - Does it need disposal tracking?

## Success Metrics

**Quantitative**:
- Developer reduces LOC by ~1-2 lines per `ConfigureEpochs` call
- No breaking changes to existing code (backward compatible)

**Qualitative**:
- API feels more "batteries included"
- Developer doesn't need to understand `IServiceScopeFactory` to use epochs
- Advanced scenarios still supported via optional factory

**Baseline**:
- Current API requires factory parameter on every `ConfigureEpochs` call
- Developer must understand `EpochCoordinator` constructor requirements

**Validation**:
- Prototype shows cleaner API
- Tests demonstrate backward compatibility
- Code review confirms improved developer experience

## Validation Approach

1. **Code Analysis**:
   - Trace how `IServiceProvider` flows through builder chain
   - Identify where `IServiceScopeFactory` is available

2. **Prototype Development**:
   - Create backward-compatible API improvement
   - Test with existing tests (should still pass)
   - Create new tests demonstrating simplified usage

3. **Documentation Review**:
   - Update API examples to show improved usage
   - Document migration path for existing code

## Expected Outcomes

- **Research documentation** in `/research/epoch-di-improvement/`
- **Implementation-ready work item** with complete specifications
- **Formal documentation** (analysis, design, ADR if needed)
- **Prototype code** demonstrating the improvement
- **Backward compatibility** preserved

## Timeline

Estimated research duration: 2-3 days

## Notes

- This is POC codebase, so changes are exploratory
- Code will be reverted after research approval
- Implementation team will create production version from specs
