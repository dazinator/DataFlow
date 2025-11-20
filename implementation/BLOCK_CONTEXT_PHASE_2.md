# [Implementation] Block Context Constructor Injection - Phase 2

## Context

This is a continuation of the Block Context Constructor Injection work started in PR #[previous-pr-number]. 

**Phase 1 (Completed)**: ActorBlock updated to use IBlockContext constructor injection
**Phase 2 (This Issue)**: Extend the pattern to remaining block types

## Background

The original implementation plan included two phases:
- **Phase 1**: Core changes to BlockBase and ActorBlock (✅ Completed)
- **Phase 2**: Apply the same pattern to other block types (⏳ This issue)

## Objectives

Apply IBlockContext constructor injection to the remaining block types in the POC, following the same pattern established with ActorBlock.

## Scope

### Block Types to Update

1. **ProducerBlock.cs**
   - [ ] Update constructor to accept IBlockContext as first parameter
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

2. **TransformBlock.cs** (if exists in POC)
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

3. **ProcessorBlock.cs** (if exists in POC)
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

4. **BatchBlock.cs**
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

5. **BroadcastBlock.cs** (if exists in POC)
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

6. **RouterBlock.cs** (if exists in POC)
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

7. **EpochActorBlock.cs** (if exists in POC)
   - [ ] Update constructor to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructor as [Obsolete]

8. **Other Epoch blocks** (EpochBatchBlock, EpochSegmenterBlock, EpochSourceBlock, etc.)
   - [ ] Update constructors to accept IBlockContext
   - [ ] Pass context to base constructor
   - [ ] Mark string name constructors as [Obsolete]

## Registration Methods to Add

For each block type that benefits from a typed helper (following the AddActorBlock pattern):

1. **AddProducerBlock<T>**
   ```csharp
   public DataFlowBuilder AddProducerBlock<T>(
       string name,
       Func<IExecutionContext, IAsyncEnumerable<T>> producer)
   {
       var fullKey = ResolveKey(name);
       _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
       {
           var context = new BlockContext(key as string);
           return new ProducerBlock<T>(context, producer);
       });
       return this;
   }
   ```

2. **AddTransformBlock<TIn, TOut>** (if TransformBlock exists)
   - Follow AddActorBlock pattern
   - Create BlockContext from key
   - Construct TransformBlock directly

3. **AddBatchBlock<T>**
   - Follow AddActorBlock pattern
   - Create BlockContext from key
   - Construct BatchBlock directly

4. **Other typed helpers as needed**
   - Only add helpers for commonly-used block types
   - Evaluate demand before adding

## Test Updates Required

### Update BlockHelpers.cs

Update the test helper methods to use new constructor pattern:

```csharp
public static ProducerBlock<T> CreateProducer<T>(
    string name,
    Func<IExecutionContext, IAsyncEnumerable<T>> producer)
{
    var context = new BlockContext(name);
    return new ProducerBlock<T>(context, producer);
}

public static BatchBlock<T> CreateBatch<T>(
    string name,
    int maxBatchSize,
    TimeSpan? windowPeriod = null)
{
    var context = new BlockContext(name);
    return new BatchBlock<T>(context, maxBatchSize, windowPeriod);
}
```

### Add Test Coverage

For each block type updated:
- [ ] Test that constructor accepts IBlockContext
- [ ] Test that Name property is available immediately
- [ ] Test that null context throws ArgumentNullException
- [ ] Integration test with new registration helper (if added)

## Implementation Pattern

Follow the pattern established in Phase 1:

**Before:**
```csharp
public ProducerBlock(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
    : base(name)
{
    _producer = producer;
}
```

**After:**
```csharp
[Obsolete("Use the constructor with IBlockContext parameter via services.AddDataFlows(). This constructor will be removed in a future version.")]
public ProducerBlock(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
    : base(name)
{
    _producer = producer;
}

public ProducerBlock(IBlockContext context, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
    : base(context)
{
    _producer = producer;
}
```

## Success Criteria

- [ ] All block types in POC use IBlockContext constructor injection
- [ ] Typed registration helpers added for commonly-used blocks
- [ ] All obsolete constructors are marked with [Obsolete] attribute
- [ ] All tests pass (existing + new validation tests)
- [ ] Test infrastructure (BlockHelpers) updated
- [ ] Build succeeds with only expected obsolete warnings
- [ ] Documentation updated (if needed)

## References

- **Phase 1 PR**: [Link to completed PR]
- **Research Documentation**: `/research/block-context-constructor-injection/`
- **Original Implementation Issue**: `/research/block-context-constructor-injection/handover/IMPLEMENTATION_ISSUE.md`
- **Before/After Comparison**: `/research/block-context-constructor-injection/handover/prototype/BEFORE_AFTER_COMPARISON.md`

## Notes

- This work extends the pattern established in Phase 1
- Each block type follows the same constructor injection pattern
- Only add typed registration helpers where they provide clear value
- Keep obsolete constructors for backward compatibility (remove in next major version)
- Focus on POC blocks first - production blocks can be updated separately if needed
