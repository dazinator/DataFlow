# BroadcastBlock Design Considerations

## Current State (After Refactoring)

The BroadcastBlock was refactored to accept on-demand connections from downstream blocks, removing the need for pre-configured targets. However, this simplification came at a cost:

### Lost Capabilities
1. **Per-target transformations**: Can no longer extract different data for different targets
2. **Type flexibility**: All targets must accept the same type `T`
3. **Concurrent access safety**: All targets receive the same object reference

### Current Architecture
```csharp
BroadcastBlock<Invoice> // All targets get Invoice
  ├─> ProcessorBlock<Invoice> (validator)
  ├─> ProcessorBlock<Invoice> (archiver)
  └─> ProcessorBlock<Invoice> (notifier)
```

## Design Requirements (From Feedback)

### 1. Concurrent Access Safety
**Problem**: Multiple downstream blocks receiving the same object instance can cause thread-safety issues if objects are mutable.

**Solutions**:
- **Per-target transformations**: Create separate instances for each target
- **Immutable types**: Recommend value types, records, or immutable classes
- **Defensive copying**: Automatically clone objects for each target

**Recommendation**: Per-target transformations provide the most flexibility and solve the core use case.

### 2. Type Flexibility
**Problem**: Financial import scenario requires extracting different properties:
- Entity code (string) for one target
- GL account (string) for another
- Bank account (string) for a third

**Current Limitation**: All targets must accept `Invoice`

**Desired Architecture**:
```csharp
BroadcastBlock<Invoice>
  ├─> ProcessorBlock<string> (entity-importer)  // Gets invoice.EntityCode
  ├─> ProcessorBlock<string> (gl-importer)      // Gets invoice.GLAccount
  └─> ProcessorBlock<string> (bank-importer)    // Gets invoice.BankAccount
```

### 3. Connection Model
**Goal**: Maintain on-demand connections using standard graph model

**Options**:

**Option A: Metadata-Based Transforms**
```csharp
builder.AddBroadcast<Invoice>("fanout");

// Transforms stored in connection metadata
builder.AddProcessor<string>("entity-importer", ...)
    .ReceiveFrom("fanout", transform: inv => inv.EntityCode);
```

**Option B: Builder Registration**
```csharp
// Current implementation uses clone functions, not transformations
var broadcast = builder.AddBroadcast<Invoice>("fanout", 
    defaultCloneFunc: inv => inv with { })
    .WithTarget("entity-importer", clone: null)  // No cloning for this target
    .WithTarget("gl-importer", clone: inv => inv.DeepClone())  // Custom clone
    .ReceiveFrom("source");

// Standard connections - all targets receive Invoice type
builder.AddProcessor<Invoice>("entity-importer", ...).ReceiveFrom("fanout");
builder.AddProcessor<Invoice>("gl-importer", ...).ReceiveFrom("fanout");
```

**Option C: Hybrid Approach (Future Enhancement)**
```csharp
// Future: Support for heterogeneous transformations
builder.AddBroadcast<Invoice>("fanout", options => {
    // Register transforms by target name
    options.RegisterTransform<string>("entity-importer", inv => inv.EntityCode);
    options.RegisterTransform<string>("gl-importer", inv => inv.GLAccount);
});

// Standard connections - targets receive different types
builder.AddProcessor<string>("entity-importer", ...).ReceiveFrom("fanout");
builder.AddProcessor<string>("gl-importer", ...).ReceiveFrom("fanout");
```

## Recommended Solution: Hybrid Approach

### Architecture

1. **BroadcastBlock<TInput>**
   - Accepts `TInput` from upstream
   - Maintains internal registry of transforms by target name
   - Creates typed channels on-demand when targets connect

2. **Configuration Phase**
   ```csharp
   builder.AddBroadcast<Invoice>("fanout", options => {
       options.AddTransform("entity", inv => inv.EntityCode);
       options.AddTransform("gl", inv => inv.GLAccount);
       options.AddTransform("bank", inv => inv.BankAccount);
   });
   ```

3. **Connection Phase**
   ```csharp
   builder.AddProcessor<string>("entity-importer", ...)
       .ReceiveFrom("fanout:entity");  // Explicit target name
   ```

4. **Runtime Behavior**
   - BroadcastBlock receives `Invoice`
   - For each invoice, applies all registered transforms
   - Writes transformed results to target-specific channels
   - Each target gets its own type-safe channel

### Benefits
- ✅ Type safety: Each target specifies its expected type
- ✅ Concurrent safety: Transformations create separate instances
- ✅ Flexibility: Different types per target
- ✅ Standard connections: Uses existing `ReceiveFrom()` API
- ✅ Clear intent: Transform registration makes data flow explicit

### Trade-offs
- Transforms must be registered upfront (not fully on-demand)
- Requires naming convention for targets (e.g., "fanout:entity")
- Slightly more verbose than simple broadcast

## Implementation Plan

### Phase 1: Update BroadcastBlockOptions
```csharp
public class BroadcastBlockOptions<TInput>
{
    private Dictionary<string, object> _transforms = new();
    
    public void AddTransform<TOutput>(string targetName, Func<TInput, TOutput> transform)
    {
        _transforms[targetName] = transform;
    }
}
```

### Phase 2: Update BroadcastBlock
```csharp
public ISourceBlock<TOutput> GetSourceForTarget<TOutput>(string targetName)
{
    // Look up transform for this target
    // Create typed channel
    // Return adapter that applies transform and writes to channel
}
```

### Phase 3: Update Builder
```csharp
// Parse "fanout:entity" syntax in ReceiveFrom()
// Call GetSourceForTarget() on broadcast block
// Wire up connection
```

### Phase 4: Documentation
- Usage guide with examples
- Thread-safety recommendations
- Migration guide from previous design

## Alternative: Keep Both Approaches

Provide two broadcast block types:

1. **SimpleBroadcastBlock<T>**: Current implementation
   - Same type to all targets
   - Shares object references
   - Use with immutable types

2. **TransformingBroadcastBlock<TInput>**: Enhanced implementation
   - Different types per target
   - Per-target transformations
   - Use for complex scenarios

This gives users the choice based on their requirements.

## Next Steps

1. Gather feedback on recommended solution
2. Implement chosen approach
3. Add comprehensive tests
4. Update documentation
5. Consider deprecation path for old API if needed
