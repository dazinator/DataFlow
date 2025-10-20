# Routing Unification Summary

## Overview

This document summarizes the changes made to unify the routing mechanism in the DataFlow library to align with the branch-based architecture.

## Key Changes

### 1. Route Definitions Now Use Branches

**Before:**
- Route factories returned `IDataFlow` objects
- Routes created independent sub-dataflows
- No connection to the main graph model

**After:**
- Route factories return `IBranchBuilder` (via `IRouteBuilder` which extends `IBranchBuilder`)
- Routes are branches that integrate with the graph model
- Route definitions are stored in `DataFlowGraph.RouteDefinitions`

### 2. Enhanced Route Context

Routes now have access to the parent graph through `RouteContext.ParentGraph`, enabling:
- Lookup of existing block definitions
- Access to other route definitions
- Potential to reuse existing branches from the parent graph

### 3. API for Merging Routes

A new `.MergeInto(blockName)` method allows routing blocks to specify a downstream target block where all route outputs should be merged. This enables scenarios like:

```csharp
builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
    .RegisterRoute("even", context => { /* build even route */ })
    .RegisterRoute("odd", context => { /* build odd route */ })
    .MergeInto("merger")  // New capability
    .ReceiveFrom("source");

builder.AddBuffer<int>("merger");  // Collect all route outputs
```

### 4. Backward Compatibility

All existing routing tests (11/11) pass without modification to test logic. The only changes required were updating route factories to return `IBranchBuilder` instead of calling `.Build()`.

## Implementation Status

### ✅ Completed

1. **Architecture Changes**
   - `IRouteBuilder` extends `IBranchBuilder`
   - `RouteBuilder` implements both interfaces properly
   - `RouteDefinition` updated to use `Func<RouteContext, IBranchBuilder>` factory
   - `DataFlowGraph` now stores route definitions
   - `RouteContext` includes `ParentGraph` reference

2. **API Updates**
   - `.MergeInto(blockName)` method on routing block builder
   - `MergeIntoBlockName` option in `StructuredRoutingBlockOptions`
   - Parent graph passed to `StructuredRoutingBlock` constructor

3. **Testing**
   - All 11 existing routing tests updated and passing
   - 2 new API demonstration tests showing the new capabilities
   - 3 comprehensive tests written (skipped until merge implementation)

### 🚧 Deferred (Future Work)

1. **Runtime Merge Implementation**
   - Actual wiring of route output blocks to merge target
   - Handling of route lifecycle with merge targets
   - Connection of route's last source block to configured merge target

2. **Branch Lookup Examples**
   - Practical examples of routes looking up and reusing existing branches
   - Documentation on when to create new vs. reuse existing branches

## Testing Summary

### Existing Tests
- ✅ `StaticRouting_Routes_Items_To_PreRegistered_Routes`
- ✅ `StaticRouting_ThrowsException_When_RouteNotFound`
- ✅ `StaticRouting_Routes_To_Complex_DataFlow_With_Multiple_Blocks`
- ✅ `StaticRouting_HandlesMultipleItemsConcurrently`
- ✅ `DynamicRouting_Creates_Routes_OnDemand_Using_Template`
- ✅ `DynamicRouting_RespectsDynamicRouteLimit`
- ✅ `DynamicRouting_ThrowsException_When_TemplateNotFound`
- ✅ `DynamicRouting_Uses_TriggeringItem_In_RouteContext`
- ✅ `RouteBuilder_AutomaticallySelectsFirstTargetBlock_AsEntryBlock`
- ✅ `RouteBuilder_ValidatesEntryBlock_IsTargetBlock`
- ✅ `RouteCreation_DisposesScope_WhenFactoryThrowsException`

### New Tests
- ✅ `RoutingBlock_CanBe_ConfiguredWithMergeOption` - Verifies API works
- ✅ `RouteContext_HasAccessTo_ParentGraph` - Verifies graph access
- ⏭️ `StaticRouting_CanMerge_RoutesIntoDownstreamBuffer` - Awaiting implementation
- ⏭️ `DynamicRouting_CanMerge_RoutesIntoDownstreamBuffer` - Awaiting implementation
- ⏭️ `Routing_CanLookup_ExistingBranchFromGraph` - Awaiting implementation

## Migration Guide

### For Existing Code Using Routing

**Before:**
```csharp
builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
    .RegisterRoute("even", context =>
    {
        var routeBuilder = context.RouteBuilder;
        routeBuilder.AddProcessor("processor", sp => new MyProcessor())
            .AsEntry();
        return routeBuilder.Build();  // Returns IDataFlow
    })
```

**After:**
```csharp
builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
    .RegisterRoute("even", context =>
    {
        var routeBuilder = context.RouteBuilder;
        routeBuilder.AddProcessor("processor", sp => new MyProcessor())
            .AsEntry();
        return routeBuilder;  // Returns IBranchBuilder
    })
```

## Benefits

1. **Unified Architecture**: Routing now uses the same branch-based model as the rest of the system
2. **Better Composability**: Routes can access and potentially reuse parts of the parent graph
3. **Merge Capability**: Foundation for merging route outputs (previously impossible)
4. **Cleaner API**: Route builders work like branch builders, reducing conceptual overhead
5. **Maintained Compatibility**: All existing functionality preserved

## Future Enhancements

1. Complete merge implementation to wire route outputs to downstream targets
2. Add examples of routes that lookup and reuse existing graph branches
3. Document patterns for when to create new branches vs. reuse existing ones
4. Consider adding support for conditional merging or routing to multiple merge targets
