# ActorBlock vs ScopeBlock Naming Analysis

## Current Naming: ActorBlock

### How It's Used

```csharp
var actorBlock = new ActorBlock<int, string, MyActor>("my-actor", scopeFactory);
```

**Pattern**:
- Takes an `IStreamActor<TIn, TOut>` implementation
- Manages DI scope lifecycle
- Handles scope rotation on request
- Executes actor with proper scoping

## Naming Evaluation

### Option 1: Keep "ActorBlock"

**Pros**:
- Already established in codebase
- "Actor" suggests active processing entity
- Familiar to users of actor model patterns
- Short and concise

**Cons**:
- Misleading - not true actor model (no mailbox, no message passing)
- Doesn't convey the key feature: DI scope management
- Could confuse developers familiar with Akka.NET or similar
- Doesn't highlight the block's primary purpose

### Option 2: Rename to "ScopeBlock"

**Pros**:
- Clearly conveys primary purpose: DI scope management
- Accurate - the block's key feature is scope isolation and rotation
- Less likely to confuse developers
- Aligns with actual behavior

**Cons**:
- Breaking change for existing users
- "Scope" might be too abstract for some developers
- Less intuitive for what the block *does* (processes items)
- Longer with qualifiers: "ScopedActorBlock"?

### Option 3: "ProcessorBlock" or "ScopedProcessorBlock"

**Pros**:
- Describes what it does: processes items
- "Scoped" variant makes DI aspect clear
- Aligns with common terminology

**Cons**:
- "Processor" is generic, doesn't convey uniqueness
- Conflicts with existing `ProcessorBlock` concepts
- Doesn't highlight scope management feature

### Option 4: Keep but Document Clearly

**Pros**:
- No breaking changes
- Clear documentation solves confusion
- Name is established

**Cons**:
- Doesn't fix fundamental naming issue
- Documentation might not be read
- Still confusing for newcomers

## Actor Model Comparison

### Traditional Actor Model (Akka, Erlang)
- Isolated state
- Message-based communication
- Mailbox for queuing
- Location transparency
- Supervision hierarchies

### DataFlow "Actor"
- Stream-based processing
- Pull-based communication (not messages)
- No mailbox
- DI scope isolation (key feature)
- Scope rotation (not supervision)

**Assessment**: DataFlow "actors" are NOT traditional actors. The name is somewhat misleading.

## User Impact Analysis

### Impact of Keeping "ActorBlock"
- Confusion for developers familiar with actor model
- Need to explain "not really an actor"
- Focus on wrong mental model

### Impact of Renaming to "ScopeBlock"
- Breaking change for existing code
- Re-learning required
- Better long-term clarity
- Accurate mental model

## Recommendation

### Short-term: Keep "ActorBlock" with Clear Documentation

**Rationale**:
1. Avoid breaking changes
2. Already established in POC
3. Can improve with documentation

**Actions**:
- Add prominent doc comment explaining it's NOT traditional actor
- Emphasize DI scope management purpose
- Provide examples of proper usage

**Example Documentation**:
```csharp
/// <summary>
/// Executes stream actors with automatic DI scope management and rotation.
/// 
/// NOTE: Despite the name, this is NOT a traditional actor model implementation.
/// The "actor" refers to the IStreamActor interface which processes streams
/// within isolated DI scopes. The key feature is scope management, not actor
/// model semantics (no mailbox, no message passing).
/// 
/// Key Features:
/// - DI scope isolation for each actor instance
/// - Automatic scope rotation on request
/// - Safe concurrent access to scoped dependencies
/// </summary>
```

### Long-term: Consider Rename in Major Version

**If doing major version bump**:
- Rename to `ScopeBlock` or `ScopedProcessorBlock`
- Provide migration guide
- Keep `ActorBlock` as deprecated alias initially
- Clear upgrade path

## Alternative: Hybrid Approach

### Keep ActorBlock, Add Alias

```csharp
// Current name
public class ActorBlock<TIn, TOut, TActor> : IBlock<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    // Implementation
}

// Add type alias for clarity
using ScopeBlock<TIn, TOut, TActor> = ActorBlock<TIn, TOut, TActor>;
```

**Pros**:
- No breaking changes
- Provides clarity for new users
- Both names available

**Cons**:
- Two names for same thing
- Potential confusion
- Inconsistency in codebase

## Conclusion

### For This Research

**Recommendation**: Keep "ActorBlock" name but STRONGLY recommend improving documentation.

**Justification**:
1. Renaming is breaking change - not within scope of research
2. Documentation improvements can solve most confusion
3. Can recommend rename in implementation handover
4. Focus research on testability, not naming

### For Implementation Team

**Recommendation for Handover Issue**:
- Document the naming concern
- Provide options analysis
- Let product owner decide on rename vs. document
- If rename chosen, do it early (less migration pain)

### Documentation Improvements (Immediate)

1. **Class-level docs**: Explain NOT traditional actor
2. **Interface docs**: Clarify `IStreamActor` purpose
3. **Examples**: Show proper usage patterns
4. **Glossary**: Define "actor" in DataFlow context
5. **Migration guide**: If coming from Akka.NET/similar

## Impact on Testability

**Key Insight**: Naming doesn't affect testability!

Whether called ActorBlock or ScopeBlock:
- Testing challenges remain the same
- Test helpers work identically
- Business logic decoupling still recommended

**Conclusion**: Naming is important for clarity and onboarding, but not a testing concern.

## Final Recommendation

### For Research Documentation
- Note the naming concern
- Provide analysis
- Recommend documentation improvements
- Defer rename decision to implementation

### For Implementation Issue
- Include naming discussion
- Recommend: Document now, consider rename later
- Provide this analysis as reference
- Let product owner decide

**Priority**: Low-Medium (important for clarity, not critical for functionality)
