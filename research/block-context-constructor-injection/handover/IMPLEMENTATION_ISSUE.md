# Implement Block Context Constructor Injection

## Context and Objectives

### Problem Statement

Currently, block context (IBlockContext) is set via a two-phase initialization pattern:
1. Block is constructed with its dependencies
2. SetContext method is called afterward to provide the block name and metadata

This creates:
- Nullable context state during construction
- SetContext method that must be internal (exposed to DI registration code)
- Context unavailable during block construction
- Non-idiomatic dependency injection pattern

The goal is to make IBlockContext a constructor parameter, enabling proper constructor injection.

### Research Background

Research was conducted to validate approaches and determine the best implementation strategy.

**Research Documentation**: `/research/block-context-constructor-injection/`

**Key Research Artifacts**:
- Main findings: `/research/block-context-constructor-injection/README.md`
- Design docs: `/research/block-context-constructor-injection/design/`
- Prototype code: `/research/block-context-constructor-injection/handover/prototype/`
- Before/after comparison: `/research/block-context-constructor-injection/handover/prototype/BEFORE_AFTER_COMPARISON.md`

Key findings from research:
- **Issue clarification**: Original issue mentioned "reflection code" but current implementation uses direct calls (no reflection)
- **Core issue remains valid**: Two-phase initialization is not ideal
- **Four approaches explored**: Constructor injection, factory pattern, hybrid pattern, dedicated registration methods
- **Recommended**: Approach 4 (Dedicated Registration Methods) - extends existing AddActorBlock pattern
- **User impact**: Minimal - API remains unchanged for library users

### Objectives

What this implementation should achieve:
- [x] IBlockContext passed via constructor instead of SetContext
- [x] Remove SetContext method from BlockBase
- [x] Make block context immutable (readonly field)
- [x] Maintain terse registration API (one line, one name)
- [x] Provide typed registration helpers for common block types
- [x] Clear migration path for custom block authors

## Implementation Guidance

### Recommended Approach

**Approach 4: Dedicated Registration Methods Per Block Type**

Extend the existing AddActorBlock pattern to provide typed registration methods for each block type. Each method handles its specific constructor signature and passes IBlockContext as a constructor parameter.

**Key Principles**:
1. **Constructor injection**: All dependencies (including context) via constructor
2. **Typed helpers**: One registration method per common block type
3. **API preservation**: No changes to user-facing API
4. **Direct construction**: No reflection, direct constructor calls
5. **Immutable context**: Context set once during construction

### Design References

Supporting documentation created during research:
- **Research Findings**: `/research/block-context-constructor-injection/README.md`
- **Recommended Approach**: `/research/block-context-constructor-injection/design/approach-4-dedicated-registration-recommended.md`
- **Before/After Comparison**: `/research/block-context-constructor-injection/handover/prototype/BEFORE_AFTER_COMPARISON.md`
- **Prototype Code**: `/research/block-context-constructor-injection/handover/prototype/`

### API/Interface Design

**BlockBase Changes**:
```csharp
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private readonly IBlockContext _context;  // Now readonly, not nullable

    // Constructor now requires context
    protected BlockBase(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    // No SetContext method
    // Name property no longer needs null check
    public string Name => _context.BlockName;
}
```

**ActorBlock Changes**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
{
    public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }
}
```

**Registration Changes**:
```csharp
public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
{
    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        var context = new BlockContext(key as string);
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new ActorBlock<TIn, TOut, TActor>(context, scopeFactory);
    });
    return this;
}
```

### Component Architecture

```
┌─────────────────────────────────────┐
│   DataFlowBuilder                   │
│   ┌───────────────────────────────┐ │
│   │ AddActorBlock<TIn,TOut,TActor>│ │
│   │ AddProducerBlock<T>           │ │  Typed helpers create
│   │ AddTransformBlock<TIn,TOut>   │ │  IBlockContext and pass
│   └───────────────┬───────────────┘ │  to constructors
└───────────────────┼─────────────────┘
                    │
                    ▼
         ┌──────────────────┐
         │  IBlockContext   │  Created during
         │  (BlockContext)  │  registration
         └────────┬─────────┘
                  │
                  ▼
      ┌───────────────────────┐
      │   BlockBase<TIn,TOut> │  Receives context
      │   - readonly _context │  via constructor
      │   - Name property     │
      └───────────┬───────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
  ┌──────────┐      ┌──────────────┐
  │ActorBlock│      │ProducerBlock │
  └──────────┘      └──────────────┘
```

### Key Implementation Considerations

1. **Breaking Change for Custom Blocks**
   - Custom blocks inheriting from BlockBase need constructor updates
   - Migration: Change `string name` parameter to `IBlockContext context`
   - Can provide temporary backward compatibility via obsolete constructors
   
2. **IBlockContext Parameter Position**
   - Recommendation: **First parameter** (convention for required dependencies)
   - Example: `ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)`
   - Consistent across all block types

3. **Remove TryAddScoped for Blocks**
   - Current: `_services.TryAddScoped<ActorBlock<...>>()`
   - New: Construct directly in factory, no need to register block type separately
   - Simplifies registration code

4. **Legacy Constructor Support (Optional)**
   - Can keep old constructors marked `[Obsolete]` for one major version
   - Provides migration path for custom blocks
   - Remove in next breaking change release

### Reusable Patterns/Code

**Pattern from Research Prototype**:
```csharp
// Typed helper pattern for block registration
public DataFlowBuilder AddXyzBlock<...>(string name, ...)
{
    ValidateBlockName(name);
    var fullKey = ResolveKey(name);
    CheckDuplicateRegistration(fullKey, "Block");

    _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
    {
        // 1. Create context from key
        var blockName = key as string ?? 
            throw new InvalidOperationException("Block key must be a string");
        var context = new BlockContext(blockName);
        
        // 2. Resolve other dependencies
        var dep1 = sp.GetRequiredService<IDependency1>();
        var dep2 = sp.GetRequiredService<IDependency2>();
        
        // 3. Construct block with all dependencies
        return new XyzBlock<...>(context, dep1, dep2, ...);
    });

    return this;
}
```

**Prototype Code Reference**:
- `/research/block-context-constructor-injection/handover/prototype/BlockBase_Prototype.cs`
- `/research/block-context-constructor-injection/handover/prototype/ActorBlock_Prototype.cs`
- `/research/block-context-constructor-injection/handover/prototype/ServiceCollectionExtensions_Prototype.cs`

### Integration Points

- **BlockBase.cs**: Core base class - all blocks inherit from this
- **ActorBlock.cs**: First block to update (already has typed helper)
- **ProducerBlock.cs**: Need to add typed helper AddProducerBlock
- **ServiceCollectionExtensions.cs**: Add new registration methods
- **Tests**: RevisedDiRegistrationTests.cs and others need updates

## Multi-Phase Implementation Assessment

**Recommendation**: ✅ Single-Phase

### Rationale

This should be implemented as a single atomic change because:

1. **Tightly Coupled Changes**: BlockBase, block implementations, and registration methods must change together
2. **Breaking Change**: Cannot partially migrate - either context is nullable or it isn't
3. **Small Scope**: Only ~5-10 block classes and registration methods affected
4. **Test Validation**: Single PR can validate entire change atomically
5. **Clear Rollback**: If issues found, single revert handles everything

**Volume**: Low (5-10 files)
**Complexity**: Medium (breaking change requires careful migration)
**Risk**: Medium (affects all blocks but well-defined changes)
**Review**: Single focused review better than multiple related reviews

## Testing and Validation

### Test Coverage Required

Test scenarios identified and validated during research:

#### Unit Tests

1. **Constructor Injection Works**
   - Setup: Create block with IBlockContext in constructor
   - Expected: Block.Name returns context.BlockName immediately
   - Why: Validates context available during and after construction

2. **No Null Context State**
   - Setup: Access Name property immediately after construction
   - Expected: Name returns valid string, never null or empty
   - Why: Validates immutable initialization

3. **AddActorBlock Creates Block Correctly**
   - Setup: Register ActorBlock via AddActorBlock, resolve from DI
   - Expected: Block created with correct name from key
   - Why: Validates registration pattern works

4. **Namespace Resolution Works**
   - Setup: Register blocks in different namespaces
   - Expected: Each block has correct namespaced name
   - Why: Validates namespace prefix handling

5. **Scoped Services Work**
   - Setup: Multiple blocks in same scope
   - Expected: Different instances across scopes, same within scope
   - Why: Validates DI lifetime management

#### Integration Tests

1. **Graph with Constructor-Injected Blocks**
   - Setup: Create full graph with new block pattern
   - Expected: Graph executes successfully
   - Why: End-to-end validation

2. **Multiple Block Types in One Graph**
   - Setup: Use ActorBlock, ProducerBlock together
   - Expected: All blocks initialize correctly
   - Why: Validates different block types work together

#### Example/Demo Tests

**Minimum Examples**:
- [x] Basic actor block registration and usage
- [x] Producer block registration and usage
- [x] Custom block with constructor injection
- [x] Migration example (old vs new pattern)

### Performance Validation

No performance testing required because:
- Current implementation doesn't use reflection
- Proposed implementation doesn't use reflection
- Both approaches use direct constructor calls
- Change is in initialization pattern only, not runtime behavior

### Edge Cases

Edge cases discovered during research prototyping:

1. **Block Without Context During Migration**
   - How to handle: Provide obsolete constructor temporarily
   - Test coverage: Warning appears, both constructors work

2. **Invalid Block Key (Not String)**
   - How to handle: Throw InvalidOperationException
   - Test coverage: Test with non-string key (shouldn't happen but defensive)

3. **Null Context Parameter**
   - How to handle: ArgumentNullException in constructor
   - Test coverage: Test direct construction with null

## Constraints and Requirements

### Technical Constraints

- **Constructor Parameter Order**: IBlockContext must be first parameter consistently
- **Internal Access**: No reflection required - direct method calls in same assembly
- **.NET 8.0**: Target framework for POC

### Performance Requirements

- Same performance as current approach (both use direct constructor calls)
- No reflection overhead
- No additional allocations beyond one BlockContext instance per block

### Compatibility Requirements

- **Backward Compatibility**: Breaking change - can provide obsolete constructors temporarily
- **API Stability**: User-facing registration API remains unchanged
- **.NET Version**: .NET 8.0 (POC target)

### Dependencies

No new dependencies required - uses existing types and patterns.

## Alternatives Explored

During research, multiple approaches were evaluated:

### Alternative 1: Constructor Injection (Basic)
**Description**: Simply add IBlockContext to all constructors without typed helpers
**Pros**: 
- Simple and direct
- Clean DI pattern
**Cons**: 
- Verbose registration for users
- No API sugar
- Breaking change for users
**Why Not Chosen**: Loses API terseness that typed helpers provide

### Alternative 2: Factory Pattern with IBlockFactory<T>
**Description**: Introduce factory interface that creates blocks with context
**Pros**: 
- Separation of concerns
- Flexible for complex initialization
**Cons**: 
- Too complex for simple use case
- More classes to maintain
- Indirection reduces clarity
**Why Not Chosen**: Over-engineered for this problem

### Alternative 3: Hybrid Pattern (Constructor + SetContext Fallback)
**Description**: Support both patterns, detect at runtime which to use
**Pros**: 
- Gradual migration possible
- Backward compatible
**Cons**: 
- **Introduces reflection** (worse than current)
- Two code paths to maintain
- Runtime overhead
**Why Not Chosen**: Adds complexity that current approach doesn't have

## References and Resources

### Documentation

All supporting documentation created during research:
- **Research Report**: `/research/block-context-constructor-injection/README.md`
- **Approach 1 Analysis**: `/research/block-context-constructor-injection/design/approach-1-constructor-injection.md`
- **Approach 2 Analysis**: `/research/block-context-constructor-injection/design/approach-2-factory-pattern.md`
- **Approach 3 Analysis**: `/research/block-context-constructor-injection/design/approach-3-hybrid-pattern.md`
- **Approach 4 Analysis** (RECOMMENDED): `/research/block-context-constructor-injection/design/approach-4-dedicated-registration-recommended.md`
- **Before/After Comparison**: `/research/block-context-constructor-injection/handover/prototype/BEFORE_AFTER_COMPARISON.md`
- **Prototype Code**: `/research/block-context-constructor-injection/handover/prototype/`

### Prior Work

Related issues and PRs:
- **Research Issue**: Current issue (this is the research issue)
- **Research PR**: Will be created from this work

## Implementation Checklist

### Phase 1: Core Changes

- [ ] **Update BlockBase.cs**
  - [ ] Remove parameterless constructor
  - [ ] Add constructor with IBlockContext parameter
  - [ ] Remove SetContext method
  - [ ] Make _context readonly
  - [ ] Update Name property (remove null check)

- [ ] **Update ActorBlock.cs**
  - [ ] Add IBlockContext as first constructor parameter
  - [ ] Pass context to base constructor
  - [ ] Remove obsolete constructor (or mark [Obsolete] temporarily)

- [ ] **Update AddActorBlock in ServiceCollectionExtensions.cs**
  - [ ] Remove TryAddScoped<ActorBlock<...>>() line
  - [ ] Create BlockContext in factory
  - [ ] Resolve IServiceScopeFactory
  - [ ] Construct ActorBlock with new constructor

### Phase 2: Additional Block Types

- [ ] **Update ProducerBlock.cs**
  - [ ] Change constructor to accept IBlockContext
  - [ ] Update obsolete constructor

- [ ] **Add AddProducerBlock method**
  - [ ] Follow AddActorBlock pattern
  - [ ] Create BlockContext from key
  - [ ] Construct ProducerBlock directly

- [ ] **Update other block types** (if they exist in POC)
  - [ ] TransformBlock
  - [ ] ProcessorBlock
  - [ ] BatchBlock

### Phase 3: Tests

- [ ] **Update existing tests**
  - [ ] RevisedDiRegistrationTests.cs
  - [ ] Any tests using direct block construction
  - [ ] Tests using obsolete constructors

- [ ] **Add new tests**
  - [ ] Constructor injection validation
  - [ ] No null context state
  - [ ] Typed helper methods work
  - [ ] Migration example test

### Phase 4: Documentation

- [ ] **Update migration guide** for custom block authors
- [ ] **Update README** with new pattern examples
- [ ] **Document breaking changes** in changelog
- [ ] **Add XML doc comments** to new methods

## Documentation Deliverables

### Required Documentation

- [x] **Usage guide** for new registration pattern
  - Scope: Quick start guide (<3KB)
  - Audience: Library users and custom block authors
  - Location: Update existing ServiceCollectionExtensions XML comments

- [x] **Migration guide** for custom blocks
  - Scope: Focused guide showing before/after
  - Audience: Custom block authors
  - Location: `/research/block-context-constructor-injection/handover/prototype/BEFORE_AFTER_COMPARISON.md`

- [ ] **README update** for POC
  - Add section on block registration
  - Show typed helper examples
  - Location: `/poc/README.md`

### Navigation Updates

- [ ] Update `/research/FOLDER_STRUCTURE.md` if needed
- [ ] Ensure research folder is linked from main README

## Success Criteria

This implementation is complete when:

- [x] All block types use constructor injection for IBlockContext
- [x] SetContext method removed from BlockBase
- [x] Context field is readonly (immutable)
- [x] Typed registration helpers provided for common blocks
- [x] All tests pass with new pattern
- [x] Documentation updated
- [x] Code review complete
- [x] CI/CD pipeline passes

## Questions for Implementation Team

1. **Backward Compatibility Duration**: Should we keep obsolete constructors for one release cycle or make a clean break?
   - Recommendation: Keep obsolete constructors for one minor version, remove in next major

2. **Which Block Types Need Typed Helpers**: Should we add AddXyzBlock for ALL block types or just common ones?
   - Recommendation: Start with ActorBlock, ProducerBlock, TransformBlock. Add others if requested.

3. **Parameter Naming**: Should the parameter be named `context` or `blockContext`?
   - Recommendation: `context` is shorter, but `blockContext` is clearer. Suggest `context` for brevity.

## Notes

- **No Reflection in Current Code**: The issue description mentioned reflection, but current implementation uses direct calls. The core issue (two-phase initialization) remains valid regardless.
- **API Unchanged for Users**: This is an internal improvement. Users of typed helpers see no difference.
- **Simple Migration for Custom Blocks**: Just change constructor parameter from `string name` to `IBlockContext context`.

---

**Created**: 2025-11-20
**Research PR**: Will be created from this branch
