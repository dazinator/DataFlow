# Current Implementation Analysis

## Date: 2025-11-20

## Current SetContext Pattern

### How It Works Now

1. **Block Registration** (ServiceCollectionExtensions.cs, AddActorBlock method):
   ```csharp
   _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
   {
       // Step 1: Resolve block from DI
       var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
       
       // Step 2: Create context from key
       var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
       var context = new BlockContext(blockName);
       
       // Step 3: Call SetContext via reflection (internal method)
       block.SetContext(context);
       
       return block;
   });
   ```

2. **BlockBase Implementation** (BlockBase.cs):
   ```csharp
   private IBlockContext? _context;
   
   // SetContext is internal - called by registration infrastructure
   internal void SetContext(IBlockContext context)
   {
       ArgumentNullException.ThrowIfNull(context);
       if (_context is not null)
       {
           throw new InvalidOperationException("Block context has already been set");
       }
       _context = context;
   }
   
   public string Name => _context?.BlockName ?? string.Empty;
   ```

3. **Block Constructor** (ActorBlock.cs):
   ```csharp
   public ActorBlock(IServiceScopeFactory scopeFactory)
       : base()  // Calls BlockBase() - no context yet
   {
       _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
   }
   ```

### Issues with Current Approach

1. **Not True Constructor Injection**
   - Context set AFTER construction completes
   - Violates principle of "complete object after constructor"
   - Cannot use context in constructor logic

2. **Reflection Required** ⚠️ 
   - Wait, looking at the code again - it's NOT using reflection!
   - It's calling `block.SetContext(context)` directly
   - The method is `internal`, which is accessible from DependencyInjection namespace
   - **Correction**: No reflection is actually used - this is a direct method call

3. **Mutable State**
   - Context is null initially, then set later
   - Requires nullable `_context` field
   - Name property returns empty string if context not set

4. **Two-Phase Initialization**
   - Block created (phase 1)
   - Context set (phase 2)
   - Creates temporal coupling

5. **Internal Method Exposure**
   - SetContext must be `internal` to allow DI registration assembly to call it
   - Cannot be `private` which would be more encapsulated

## What Works Well

1. **Terse Registration API**
   ```csharp
   df.AddActorBlock<int, string, TestActor>("transformer");
   ```
   - Single line, no duplication
   - Name only specified once
   - All dependencies auto-injected

2. **Scoped Services Work**
   - IServiceScopeFactory properly injected
   - TActor resolved from DI scope
   - Multiple DI dependencies supported

3. **Namespace Support**
   - Keyed service registration with namespace prefix
   - Works with modular monolith pattern

4. **Backward Compatibility**
   - Legacy constructor still available (marked obsolete)
   - Gradual migration path

## The Real Problem

Re-reading the issue more carefully, the user says "there is some reflection code to call SetContext" - but I don't see reflection in the current code. Let me check if there's reflection elsewhere...

Looking at line 230 in ServiceCollectionExtensions.cs:
```csharp
block.SetContext(context);
```

This is a direct method call, not reflection. The method is `internal` so it's accessible.

**Conclusion**: The issue description may be outdated, OR there's reflection code elsewhere I haven't found yet. Let me search more thoroughly.
