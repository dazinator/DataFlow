# Prototype Code - Block Context Constructor Injection

This directory contains prototype code demonstrating the recommended approach for block context constructor injection.

## Purpose

These prototypes validate that IBlockContext can be passed via constructor instead of SetContext, while maintaining the terse registration API.

## Prototype Files

### BlockBase_Prototype.cs
Demonstrates BlockBase with constructor injection:
- IBlockContext passed as constructor parameter
- Readonly context field (immutable)
- No SetContext method
- No nullable context state

**Key Changes from Current**:
- Remove: `private IBlockContext? _context;`
- Add: `private readonly IBlockContext _context;`
- Remove: `internal void SetContext(IBlockContext context)`
- Remove: Parameterless constructor
- Add: `protected BlockBase(IBlockContext context)`

### ActorBlock_Prototype.cs
Demonstrates ActorBlock with constructor injection:
- IBlockContext as first parameter
- IServiceScopeFactory as second parameter
- Passes context to base constructor

**Key Changes from Current**:
- Change: `public ActorBlock(IServiceScopeFactory scopeFactory)`
- To: `public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)`

### ServiceCollectionExtensions_Prototype.cs
Demonstrates updated registration methods:
- AddActorBlock_Prototype - Updated ActorBlock registration
- AddProducerBlock_Prototype - New ProducerBlock registration
- AddTransformBlock_Prototype - New TransformBlock registration

**Key Changes from Current**:
- Remove: `_services.TryAddScoped<ActorBlock<...>>()`
- Remove: `var block = sp.GetRequiredService<ActorBlock<...>>()`
- Remove: `block.SetContext(context)`
- Add: Direct construction with all dependencies

**Pattern**:
```csharp
_services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
{
    var context = new BlockContext(key as string);
    var dep = sp.GetRequiredService<IDependency>();
    return new XyzBlock<...>(context, dep);
});
```

### BEFORE_AFTER_COMPARISON.md
Detailed comparison showing:
- Code changes for each file
- API usage changes (minimal)
- Impact assessment
- Migration complexity

## How to Use These Prototypes

1. **Review the pattern**: See how constructor injection works in BlockBase_Prototype
2. **Compare before/after**: Read BEFORE_AFTER_COMPARISON.md for detailed changes
3. **Understand registration**: See ServiceCollectionExtensions_Prototype for updated methods
4. **Adapt for production**: Use these as reference when implementing in actual codebase

## Key Takeaways

1. **Simple Change**: Just add IBlockContext parameter to constructors
2. **No Reflection**: Direct constructor calls, same as current approach
3. **API Preserved**: User-facing API unchanged for typed helpers
4. **Immutable State**: Context set once, never changes
5. **Clear Pattern**: Each block type has dedicated registration method

## Implementation Notes

- These are prototypes for reference, not production code
- File names include "_Prototype" suffix to avoid confusion
- Some dependencies (like IExecutionContext) are referenced but not included
- Focus is on the constructor injection pattern, not complete implementation

## Next Steps

See `/research/block-context-constructor-injection/handover/IMPLEMENTATION_ISSUE.md` for complete implementation guidance.
