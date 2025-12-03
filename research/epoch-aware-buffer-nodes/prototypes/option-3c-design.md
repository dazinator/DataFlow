# Option 3C: Keep Buffer Nodes for Non-Epoch Flows Only

## Overview

Accept that buffer nodes are for plain types only and document this limitation. Provide alternative patterns for epoch stream scenarios.

## Key Concept

**Buffer nodes remain unchanged** and explicitly document their scope:
- ✅ Supported for plain types (`int`, `string`, custom types)
- ✅ Supported for side channel envelopes (`IDataEnvelope`)
- ❌ Not supported for epoch streams (`IEpochStream<T>`)

Users who need buffering with epoch streams use alternative patterns (likely Option 3A).

## Implementation

### Phase 1: Documentation (Immediate)

Add clear documentation to buffer node classes and docs:

```csharp
/// <summary>
/// Represents a first-class buffer node in the dataflow graph.
/// 
/// <para>
/// <strong>Supported Use Cases:</strong>
/// </para>
/// <list type="bullet">
/// <item>Plain types (int, string, custom types)</item>
/// <item>Side channel envelopes (IDataEnvelope)</item>
/// </list>
/// 
/// <para>
/// <strong>Not Supported:</strong>
/// </para>
/// <list type="bullet">
/// <item>Epoch streams (IEpochStream&lt;T&gt;) - use EpochBufferBlock instead</item>
/// </list>
/// </summary>
public class BufferNode
{
    // ... existing implementation
}
```

### Phase 2: Runtime Validation (Optional)

Add validation to detect and reject epoch stream usage:

```csharp
public class BufferNode
{
    public BufferNode(Type dataType, int capacity, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        
        // Validate not epoch stream
        if (IsEpochStreamType(dataType))
        {
            throw new ArgumentException(
                $"Buffer nodes do not support epoch streams (IEpochStream<T>). " +
                $"Use EpochBufferBlock for epoch stream buffering.",
                nameof(dataType));
        }
        
        DataType = dataType;
        Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name;
    }
    
    private static bool IsEpochStreamType(Type type)
    {
        return type.IsGenericType && 
               type.GetGenericTypeDefinition() == typeof(IEpochStream<>);
    }
}
```

### Phase 3: Alternative Pattern (Option 3A)

Provide `EpochBufferBlock<T>` as the recommended alternative for epoch streams.

**User guidance**:
```csharp
// ❌ Don't use buffer nodes with epoch streams
BufferNode<IEpochStream<int>> buffer = new(100); // Throws exception

// ✅ Do use EpochBufferBlock for epoch stream buffering
builder.AddEpochBuffer<int>("buffer", capacity: 100);
```

## Advantages

### 1. Minimal Change
- No changes to existing buffer node implementation
- No risk of breaking existing code
- Quick to implement (documentation only)

### 2. Clear Semantics
- Buffer nodes have one clear purpose: buffer plain items
- No confusion about what they support
- Simple mental model for users

### 3. Separation of Concerns
- Plain buffering: `BufferNode<T>`
- Epoch buffering: `EpochBufferBlock<T>`
- Each specialized for its use case

### 4. Future Flexibility
- Can optimize buffer nodes for plain types
- Can optimize epoch buffers for epoch patterns
- Independent evolution

### 5. Low Risk
- Existing code continues to work
- New code uses new pattern
- No complex migration needed

## Disadvantages

### 1. Two Buffer Concepts
- Users must learn both patterns
- "Why can't I use BufferNode for everything?"
- Documentation burden

### 2. Inconsistent API
- Different APIs for similar concepts
- May feel like incomplete design
- Users may expect consistency

### 3. Future Deprecation Path
If we eventually want to deprecate buffer nodes:
- Need migration path for existing users
- Potentially breaking change
- May cause user frustration

## Migration Strategy

### For New Code
Simply use the appropriate buffer type:
- Plain flows: `BufferNode<T>`
- Epoch flows: `EpochBufferBlock<T>`

### For Existing Code
No migration needed - continues to work unchanged.

### Future Deprecation (If Needed)
If we later decide to deprecate `BufferNode`:

1. **Deprecation notice** (v1.x)
   ```csharp
   [Obsolete("Use EpochBufferBlock for new code. BufferNode will be removed in v2.0")]
   public class BufferNode<T>
   ```

2. **Adapter implementation** (v1.x)
   ```csharp
   // Provide helper to convert BufferNode to EpochBufferBlock usage
   public static DataFlowGraphBuilder MigrateBufferNode<T>(
       this DataFlowGraphBuilder builder,
       BufferNode<T> oldBuffer,
       string newBlockName)
   ```

3. **Removal** (v2.0)
   - Remove `BufferNode` class
   - Only `EpochBufferBlock` remains

**Note**: Deprecation is **optional**. Buffer nodes can coexist with epoch buffers indefinitely.

## Comparison: Option 3C vs Option 3A Alone

| Aspect | 3C (Keep Both) | 3A Only (Replace Buffer Nodes) |
|--------|----------------|--------------------------------|
| **Change scope** | ✅ Minimal | ⚠️ Moderate |
| **User migration** | ✅ None | ⚠️ Required |
| **Concepts to learn** | ⚠️ Two patterns | ✅ One pattern |
| **Plain type performance** | ✅ Optimized | ⚠️ Overhead for non-epoch |
| **Epoch stream support** | ✅ Via new block | ✅ Via new block |
| **API consistency** | ⚠️ Two APIs | ✅ One API |
| **Implementation effort** | ✅ Low | ⚠️ Medium |
| **Risk** | ✅ Minimal | ⚠️ Medium |

## Recommendation

**✅ Option 3C is RECOMMENDED as the pragmatic path**

**Rationale**:
1. **Minimal risk**: Existing code unchanged
2. **Quick delivery**: Documentation-only change
3. **Clear separation**: Each buffer type has clear purpose
4. **Future flexibility**: Can deprecate later if needed

**Combined approach**: Option 3C + Option 3A
- Keep buffer nodes for plain types (Option 3C)
- Add epoch buffer blocks for epoch streams (Option 3A)
- Document the distinction clearly

## Implementation Plan

### Step 1: Documentation (Week 1)
- Add documentation to `BufferNode` class
- Update user guides
- Add examples showing both patterns

### Step 2: Validation (Week 1, Optional)
- Add runtime check in `BufferNode` constructor
- Helpful error message pointing to `EpochBufferBlock`

### Step 3: Implement Option 3A (Week 2-3)
- Implement `EpochBufferBlock<T>`
- Add graph builder extensions
- Create comprehensive tests

### Step 4: Documentation Updates (Week 3)
- Architecture docs explaining two buffer types
- Migration guide (when to use each)
- Update examples

## Usage Examples

### Plain Type Buffering
```csharp
// For non-epoch flows
var builder = new DataFlowGraphBuilder("flow");

builder
    .AddProducer<PlainSource>("source1")
    .AddProducer<PlainSource>("source2")
    .AddBuffer<int>("buffer", capacity: 100)  // BufferNode
        .ReceiveFrom("source1")
        .ReceiveFrom("source2")
    .AddProcessor<PlainProcessor>("processor")
        .ReceiveFrom("buffer");
```

### Epoch Stream Buffering
```csharp
// For epoch flows
var builder = new DataFlowGraphBuilder("flow");

builder
    .AddSourceActor<EpochSource1>("source1")
    .AddSourceActor<EpochSource2>("source2")
    .AddEpochBuffer<int>("buffer", capacity: 100)  // EpochBufferBlock
        .ReceiveFrom("source1")
        .ReceiveFrom("source2")
    .AddStreamActor<EpochProcessor>("processor")
        .ReceiveFrom("buffer");
```

### Side Channel (Buffer Nodes)
```csharp
// Side channels use buffer nodes (non-epoch)
builder.AddBuffer<IDataEnvelope>("side-channel", capacity: 50);
```

## Decision Matrix

Choose buffer type based on flow characteristics:

| Flow Type | Block Input/Output | Buffer Type |
|-----------|-------------------|-------------|
| Plain items | `T` → `T` | `BufferNode<T>` |
| Side channel | `IDataEnvelope` | `BufferNode<IDataEnvelope>` |
| Epoch streams | `IEpochStream<T>` | `EpochBufferBlock<T>` |

## Success Criteria

- [ ] Buffer nodes documented as plain-type only
- [ ] Runtime validation added (optional)
- [ ] Clear user guidance on when to use each buffer type
- [ ] Option 3A implemented as alternative for epoch streams
- [ ] Examples updated showing both patterns
- [ ] No breaking changes to existing code

## Related Design Decisions

This option aligns with the broader principle:
**Different abstraction levels deserve different components**

Just as we have:
- `IProducer<T>` for plain sources
- `ISourceActor<T>` for epoch sources

We have:
- `BufferNode<T>` for plain buffering
- `EpochBufferBlock<T>` for epoch buffering

This creates a consistent pattern across the API.
