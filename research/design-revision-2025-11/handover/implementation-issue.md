# Implementation Issue: Revised DI Service Registration Design

**⚠️ IMPORTANT**: This specification **SUPERSEDES** issue #475, which was based on the original research from issue #473. Please refer to this document for implementation guidance.

---

## Implementation Context

**Research Reference**: Current issue (Design Revision Research)  
**Previous Research**: #473 (superseded by this revision)  
**Previous Implementation Issue**: #475 (superseded by this specification)  
**Research Documentation**: `/research/design-revision-2025-11/`  
**Status**: Ready for Implementation  
**Priority**: High  
**Complexity**: Medium  

---

## Summary of Changes from Previous Design

This revision addresses **six concerns** identified during implementation attempts of #473:

| Concern | Previous Design (#473) | Revised Design (This Issue) |
|---------|------------------------|------------------------------|
| **Builder Structure** | DataFlowGraphBuilderEx (parallel) | DataFlowGraphBuilder (enhanced) |
| **Default Lifetime** | Singleton | Scoped |
| **Duplicate Handling** | Unspecified | Throws on duplicates |
| **Graph Integration** | Separate topology | AddGraph() method |
| **Namespace Support** | Not supported | Optional namespace prefixes |
| **Block Registration** | Name in constructor | Typed helpers + IBlockContext |

**Key Improvements**: Single, enhanced builder with safer defaults, integrated API, modular monolith support, and DI-friendly block registration.

---

## Research Validation Summary

✅ **All Features Validated**:
- **Prototype**: 32/32 tests passing (includes 3 DI-friendly block tests)
- **Performance**: Negligible overhead (<0.1%)
- **Backward Compatibility**: Fully maintained
- **Documentation**: Complete research artifacts available

**Research Artifacts**:
- Main findings: `/research/design-revision-2025-11/README.md`
- Problem analysis: `/research/design-revision-2025-11/notes/analysis.md`
- API specification: `/research/design-revision-2025-11/design/api-spec.md`
- ADR: `/poc/docs/adr/2025-11-19-revised-di-service-registration.md`
- Prototype code: `/research/design-revision-2025-11/handover/prototype/`

---

## Features to Implement

### 1. Enhanced DataFlowGraphBuilder (Core)

**Changes to Existing Builder**:
```csharp
public class DataFlowGraphBuilder
{
    // NEW: Optional service provider constructor
    public DataFlowGraphBuilder(
        string name, 
        IServiceProvider? serviceProvider = null,  // NEW
        ILogger<DataFlowGraph>? logger = null);
    
    // NEW: Resolve blocks from DI
    public DataFlowGraphBuilder UseBlock(string name);
    
    // UNCHANGED: Existing methods
    public DataFlowGraphBuilder AddBlock(IBlock block);
    // ... all other methods unchanged ...
}
```

**Status**: ✅ Backward compatible enhancement

### 2. Service Registration with Lifetime Methods (Core)

**API**:
```csharp
services.AddDataFlows(df => 
{
    // Default: Scoped (safe for most scenarios)
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    
    // Explicit lifetimes
    df.AddScopedBlock("scoped", sp => new ScopedBlock(...));
    df.AddSingletonBlock("singleton", sp => new StatelessBlock(...));
    df.AddTransientBlock("transient", sp => new TransientBlock(...));
    
    // Strategy registration
    df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
});

// Usage (same DataFlowGraphBuilder class)
var builder = new DataFlowGraphBuilder("flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
```

**Components**:
- `DataFlowBuilder` - Registration builder with lifetime methods
- `ServiceCollectionExtensions.AddDataFlows()` - Extension method
- Duplicate detection with clear error messages

### 3. Graph Integration (Core)

**API**:
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);
    df.AddBlock("transformer", ...);
    
    // Inline graph configuration
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
    
    // Or class-based
    df.AddGraphDefinition<MyGraphDefinition>("complex");
});

// Resolve graph from DI
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");
```

**Components**:
- `AddGraph()` method
- `IDataFlowDefinition` interface
- `AddGraphDefinition<T>()` method

### 4. Namespace Support (Enhancement)

**API**:
```csharp
// Global namespace (default)
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);  // Registered as "global:producer"
    df.AddGraph("main", ...);      // Registered as "global:main"
});

// Module-specific namespace
services.AddDataFlows("moduleA", df => 
{
    df.AddBlock("producer", ...);  // Registered as "moduleA:producer"
    
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")         // Resolves "moduleA:producer"
         .UseBlock("global:logger")    // Cross-namespace reference
         .Connect("producer", "global:logger");
    });
});

// Resolution with namespace
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:main");
```

**Components**:
- `AddDataFlows(string namespacePrefix, ...)` overload
- Key resolution logic with colon separator detection
- Namespace tracking in `DataFlowBuilder` and `DataFlowGraphBuilder`
- Cross-namespace reference support

**Key Behaviors**:
- Default "global" namespace for consistency
- Colon (`:`) separator indicates fully-qualified key
- Namespace applies to blocks, strategies, and graphs
- `DataFlowBuilder.Namespace` property for introspection

### 6. DI-Friendly Block Registration (Enhancement)

**Problem**: Block constructors required name parameter, causing duplication and verbose factory functions.

**Solution**: Typed helper methods + `IBlockContext` lifecycle initialization pattern.

**API**:
```csharp
services.AddDataFlows(df => 
{
    // Old way (still supported for custom blocks)
    df.AddBlock("custom", sp => new CustomBlock(...));
    
    // New way (DI-friendly typed helpers)
    df.AddActorBlock<int, string, TransformActor>("transformer");
    // All dependencies auto-injected, name set via IBlockContext
});
```

**Implementation Approach**:
```csharp
// 1. Block type registered with DI (scoped)
services.TryAddScoped<ActorBlock<TIn, TOut, TActor>>();

// 2. Keyed factory resolves + initializes
services.AddKeyedScoped<IBlock>(fullKey, (sp, key) => 
{
    var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
    block.SetContext(new BlockContext(name));  // Lifecycle initialization
    return block;
});

// 3. Block uses context for name
public class BlockBase 
{
    private IBlockContext? _context;
    
    public void SetContext(IBlockContext context) => _context = context;
    public string Name => _context?.BlockName ?? string.Empty;
}
```

**Key Design Decisions**:
- Uses existing `IBlockContext` interface (already in codebase)
- `SetContext()` method for lifecycle initialization (called once post-construction)
- Standard DI resolution (`GetRequiredService`) - no `ActivatorUtilities`
- Proper scoped service disposal tracking guaranteed
- Extensible via `IBlockContext.Metadata` for future needs

**Benefits**:
- No name duplication between registration and construction
- Leverages DI for all dependencies
- Name guaranteed to match registration key
- Clean, concise API
- Type-safe
- Proper disposal tracking

**Typed Helpers to Implement**:
- `AddActorBlock<TIn, TOut, TActor>(string name)` - For actor-based blocks
- Additional helpers can be added for other block types as needed

### 7. Runtime Resolution and Execution (Documentation)

**Guidance to Include**:

**Pattern 1: Direct Resolution**
```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");
await graph.ExecuteAsync(cancellationToken);
```

**Pattern 2: Scoped Resolution**
```csharp
using (var scope = serviceProvider.CreateScope())
{
    var graph = scope.ServiceProvider.GetKeyedService<DataFlowGraph>("main");
    await graph.ExecuteAsync(cancellationToken);
}
```

**Pattern 3: Factory Service**
```csharp
public class DataFlowGraphFactory
{
    public DataFlowGraph GetGraph(string name);
    public DataFlowGraph GetNamespacedGraph(string ns, string name);
}
```

**Pattern 4: Direct Injection**
```csharp
services.AddScoped(sp => 
    sp.GetKeyedService<DataFlowGraph>("main") ?? throw ...);
```

**Documentation Requirements**:
- Include all resolution patterns in guide
- Document lifetime implications (scoped blocks per scope)
- Document concurrency considerations
- Provide error handling examples

---

## Implementation Checklist

### Phase 1: Core Implementation (2-3 days)

**1.1 Enhanced DataFlowGraphBuilder**:
- [ ] Add optional `IServiceProvider?` field
- [ ] Add constructor overload with service provider parameter
- [ ] Implement `UseBlock(string name)` method
- [ ] Add validation and error messages
- [ ] Ensure backward compatibility (all existing tests pass)

**1.2 Service Registration**:
- [ ] Create `DataFlowBuilder` class
- [ ] Implement `AddBlock()` (defaults to scoped)
- [ ] Implement `AddScopedBlock()`, `AddSingletonBlock()`, `AddTransientBlock()`
- [ ] Implement strategy registration methods
- [ ] Implement duplicate detection
- [ ] Create `ServiceCollectionExtensions.AddDataFlows()`
- [ ] Implement namespace support (optional prefix parameter)
- [ ] Implement key resolution with namespace prefix logic
- [ ] Implement typed helper: `AddActorBlock<TIn, TOut, TActor>()`
- [ ] Implement `IBlockContext` initialization pattern in helpers

**1.3 Block Lifecycle Enhancement**:
- [ ] Add `SetContext(IBlockContext)` method to `BlockBase` (if not present)
- [ ] Update `BlockBase.Name` to use `_context?.BlockName`
- [ ] Ensure `SetContext()` can only be called once (guard against re-initialization)
- [ ] Verify existing `IBlockContext` interface has `BlockName` property

**1.4 Graph Integration**:
- [ ] Implement `IDataFlowDefinition` interface
- [ ] Implement `AddGraph()` method
- [ ] Implement `AddGraphDefinition<T>()` method

### Phase 2: Testing (1-2 days)

- [ ] Port all 32 prototype tests (19 original + 10 namespace + 3 DI-friendly block tests)
- [ ] Add integration tests with realistic scenarios
- [ ] Test all lifetime scenarios (scoped, singleton, transient)
- [ ] Test duplicate detection edge cases
- [ ] Test graph resolution from DI
- [ ] Test namespace scenarios (global, custom, cross-namespace)
- [ ] Test DI-friendly block helpers (AddActorBlock)
- [ ] Test IBlockContext initialization pattern
- [ ] Test scoped service disposal tracking
- [ ] Test backward compatibility (existing inline tests)
- [ ] Validate error messages are clear

### Phase 3: Documentation (2-3 days)

**3.1 README Updates**:
- [ ] Add "DI Registration" section to main POC README
- [ ] Show API examples (including namespace usage)
- [ ] Show runtime resolution and execution examples
- [ ] Link to guides

**3.2 New Guides**:
- [ ] Create migration guide from inline to DI registration
- [ ] Create lifetime selection guide
- [ ] Create guide on when to use AddGraph vs dynamic building
- [ ] Create class-based definition guide
- [ ] Create namespace usage guide (modular monolith scenarios)
- [ ] Create graph resolution and execution guide
- [ ] Create DI-friendly block registration guide (typed helpers)

**3.3 API Documentation**:
- [ ] Comprehensive XML documentation on all public APIs
- [ ] Document lifetime implications
- [ ] Document error conditions
- [ ] Document namespace prefix behavior
- [ ] Document graph resolution patterns

### Phase 4: Examples (1-2 days)

- [ ] Create basic DI registration example
- [ ] Create lifetime management example
- [ ] Create graph integration example
- [ ] Create class-based definition example
- [ ] Create hybrid example (mix DI and direct blocks)
- [ ] Create namespace example (modular monolith)
- [ ] Create graph resolution and execution example
- [ ] Create DI-friendly block registration example (using typed helpers)
- [ ] Update 1-2 existing examples to show both patterns

### Phase 5: Polish (1 day)

- [ ] Code review
- [ ] Performance validation
- [ ] Ensure all tests pass
- [ ] Update CHANGELOG
- [ ] Address review feedback

**Total Estimated Effort**: 7-11 days

---

## Critical Implementation Notes

### ⚠️ DO NOT Create DataFlowGraphBuilderEx

**Previous design created a new class. This revision enhances the existing class.**

- ❌ DON'T: Create `DataFlowGraphBuilderEx`
- ✅ DO: Enhance existing `DataFlowGraphBuilder`
- **Why**: Avoids parallel structures and confusion

### ⚠️ Default Lifetime is Scoped (Not Singleton)

**This is a change from the previous research.**

- Previous (#473): `AddBlock()` registered as singleton
- **This revision**: `AddBlock()` registers as scoped
- **Migration**: Use `AddSingletonBlock()` to preserve singleton behavior

### ⚠️ Throw on Duplicate Names

**Duplicate block/strategy/graph names throw `InvalidOperationException`.**

- Clear error message with component name
- Helps catch configuration mistakes
- Prevents ambiguous behavior

---

## Success Criteria

### Functional
- [ ] All 32 prototype tests pass (including DI-friendly block tests)
- [ ] Existing POC tests still pass (backward compatibility)
- [ ] Can register blocks with all lifetime scopes
- [ ] Can resolve blocks from DI
- [ ] Can register and resolve graphs from DI
- [ ] Duplicate names throw clear errors
- [ ] Error messages are actionable
- [ ] DI-friendly typed helpers work correctly
- [ ] IBlockContext initialization works properly
- [ ] Scoped services are tracked for disposal

### Non-Functional
- [ ] Performance overhead <1%
- [ ] XML documentation on all public APIs
- [ ] Code follows POC conventions
- [ ] No breaking changes to existing inline usage

### Documentation
- [ ] Migration guide available
- [ ] Lifetime selection guide available
- [ ] Examples demonstrate all patterns
- [ ] README updated

---

## Performance Requirements

Based on research validation:
- **Keyed Service Resolution**: <1% overhead (validated: <0.1%)
- **Memory**: No additional overhead
- **Graph Building**: No significant impact

---

## Migration from Previous Design (#473 / #475)

### If You Started Implementing #475

**Stop and use this specification instead.**

**Key Changes**:
1. Don't create `DataFlowGraphBuilderEx` - enhance `DataFlowGraphBuilder`
2. Default lifetime is scoped (not singleton)
3. Add duplicate detection
4. Add `AddGraph()` integration

### Migration Example

**Old Pattern** (from #473/#475):
```csharp
// DON'T USE THIS
var builder = new DataFlowGraphBuilderEx("flow", sp);
```

**New Pattern** (this revision):
```csharp
// USE THIS INSTEAD
var builder = new DataFlowGraphBuilder("flow", sp);
```

---

## File Locations

### Production Code

**New Files**:
- `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
  - Reference: `/research/design-revision-2025-11/handover/prototype/ServiceCollectionExtensions.cs`

**Modified Files**:
- `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
  - Reference: `/research/design-revision-2025-11/handover/prototype/DataFlowGraphBuilder.diff`
- `/poc/DataFlow.POC/Core/BlockBase.cs`
  - Add `SetContext()` method and update `Name` property
- `/poc/DataFlow.POC/Blocks/ActorBlock.cs`
  - Add parameterless constructor (if needed for DI-friendly registration)

### Tests

**New Test File**:
- `/poc/DataFlow.POC.Tests/RevisedDiRegistrationTests.cs` (or similar name)
  - Reference: `/research/design-revision-2025-11/handover/prototype/RevisedDiDesignTests.cs`

---

## Design References

- **ADR**: `/poc/docs/adr/2025-11-19-revised-di-service-registration.md`
- **API Spec**: `/research/design-revision-2025-11/design/api-spec.md`
- **Research README**: `/research/design-revision-2025-11/README.md`
- **Analysis**: `/research/design-revision-2025-11/notes/analysis.md`
- **Prototype**: `/research/design-revision-2025-11/handover/prototype/`

---

## Dependencies

**Framework**:
- .NET 8+ (for keyed services)
- Microsoft.Extensions.DependencyInjection

**Existing Code**:
- `IBlock` interface
- `EdgeStrategy` base class
- `DataFlowGraph` and `DataFlowGraphBuilder`

**No new external dependencies required**

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Breaking changes | Low | Backward compatible enhancement |
| Performance regression | Low | Validated <0.1% overhead |
| Adoption confusion | Medium | Clear migration docs |
| String typos at runtime | Medium | Clear error messages |

---

## Related Issues

- Research (Original): #473 (superseded)
- Research (This Revision): Current issue
- Implementation (Original): #475 (superseded by this specification)

---

## Notes

This implementation is based on a **fully validated research prototype**:
- ✅ 32/32 tests passing (including DI-friendly block tests)
- ✅ Performance validated (<0.1% overhead)
- ✅ API design validated through comprehensive testing
- ✅ Backward compatibility confirmed
- ✅ All six concerns from #473 resolved
- ✅ Proper scoped service disposal tracking verified

**Prototype code is production-ready** - mainly needs:
- XML documentation enhancement
- Integration tests
- User-facing documentation
- Examples

**Risk Level**: **Low** - Prototype eliminates technical risks.

---

## Labels

- `workflow:implementation`
- `enhancement`
- `poc`
- `supersedes-475`
