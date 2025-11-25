# Research Plan: Library Usage Guides

**Research Topic**: Creating comprehensive, cohesive user guides for DataFlow library  
**Status**: In Progress  
**Created**: 2025-11-25  
**Researcher**: @copilot

---

## Research Objective

Create a complete set of user guides that take developers from "zero to hero" with the DataFlow library, addressing gaps in the current documentation and providing clear, idiomatic usage patterns for all major features including epochs, checkpointing, and topologies.

## Problem Statement

A developer provided feedback highlighting several documentation gaps:

1. **No working examples of epoch blocks in graph context** - EpochSourceBlock/EpochActorBlock tests only execute blocks standalone, not in integrated graphs
2. **Missing "getting started" guide** - No clear entry point for new users
3. **Lack of end-to-end examples** - Existing guides are feature-specific but don't show the full picture
4. **Inconsistent patterns** - Modern idiomatic patterns (like UseBlock semantics) aren't documented
5. **Fragmented topology documentation** - Current topology guide tries to cover too much in one document

## Research Questions

1. What is the ideal structure for a progressive learning path (beginner → advanced)?
2. How can we create a cohesive set of guides that build on each other without repetition?
3. What is the "hello world" baseline that all guides should reference?
4. What are the modern, idiomatic usage patterns that should be documented?
5. How should we organize epoch-related documentation to make it approachable?

## Current State Analysis

### Existing Guides (in /poc/docs/guides/)

1. **dependency-injection-registration.md** - Good DI guide, but assumes familiarity with the library
2. **using-epochs.md** - Comprehensive epoch guide, but no simple entry point
3. **ef-core-epochs.md** - Specific use case, builds on epoch knowledge
4. **control-flow-topologies.md** - Extensive but tries to cover too much
5. **business-logic-decoupling.md** - Advanced pattern
6. **testing-guide.md** - Comprehensive testing guide
7. **migrating-from-current-design.md** - Migration specific

### Gaps Identified

#### Critical Gaps
- ✗ **No "Getting Started" guide** - New users have no clear entry point
- ✗ **No basic graph building tutorial** - How to create your first flow
- ✗ **No execution context examples** - How to run graphs in different scenarios (console, ASP.NET, services)
- ✗ **No epoch examples in graph context** - All epoch examples are standalone
- ✗ **No source block guide** - How to create and use source blocks

#### Structure Gaps
- ✗ **No guide navigation/index** - Hard to know where to start
- ✗ **No progressive learning path** - Guides don't build on each other
- ✗ **Topology guide too monolithic** - Should be split into focused guides
- ✗ **Missing cross-references** - Guides don't link to each other effectively

#### Pattern Gaps
- ✗ **UseBlock pattern not documented** - Modern recommended approach
- ✗ **Keyed services pattern unclear** - How to inject and obtain graphs
- ✗ **DI namespace isolation not covered** - Multi-tenant scenarios

## Proposed Guide Structure

### Tier 1: Getting Started (NEW)
**Purpose**: Zero to first working graph

#### 1. Getting Started Guide (NEW)
**File**: `getting-started.md`  
**Topics**:
- Installation (assume package "uniun.dataflow")
- What is DataFlow? (brief overview)
- Your first graph (console input → transform → output)
- Registering with DI
- Executing the graph
  - From Program.cs
  - In ASP.NET endpoint (with [FromKeyedServices])
  - In a regular service (factory pattern)
- Where to go next (links to feature guides)

**Baseline "Hello World" Flow**:
```csharp
// This will be the baseline all other guides extend
Producer: Console readline input
Transform: Convert to uppercase
Processor: Write to console
```

### Tier 2: Core Features (ENHANCED/NEW)
**Purpose**: Add core features to the hello world flow

#### 2. Working with Blocks Guide (NEW)
**File**: `working-with-blocks.md`
**Topics**:
- Block types overview
- Creating custom blocks
- Using built-in blocks
- Block registration patterns (UseBlock semantics)
- Block naming and DI

#### 3. Understanding Topologies (REFACTORED)
**Split current topology guide into focused guides**:

##### 3a. Broadcast Topology Guide (NEW - extracted)
**File**: `topology-broadcast.md`
**Topics**:
- When to use broadcast
- Setting up broadcast edges
- Example: Extending hello world with logging and metrics

##### 3b. Competing Consumers Guide (NEW - extracted)
**File**: `topology-competing-consumers.md`
**Topics**:
- When to use competing consumers
- Load balancing patterns
- Example: Extending hello world with parallel processing

##### 3c. Selective Routing Guide (NEW - extracted)
**File**: `topology-selective-routing.md`
**Topics**:
- Content-based routing
- Dynamic routing logic
- Example: Extending hello world with conditional routing

#### 4. Source Blocks Guide (NEW)
**File**: `source-blocks.md`
**Topics**:
- What are source blocks
- Creating a simple source
- Epoch streams and non-epoch streams
- Example: Replacing console input with a database source

### Tier 3: Advanced Features (ENHANCED)
**Purpose**: Transaction boundaries, checkpointing, advanced patterns

#### 5. Introducing Epochs (ENHANCED)
**File**: `using-epochs.md` (update existing)
**Topics**:
- High-level concept (builds on hello world)
- When to use epochs vs regular blocks
- Epoch segmentation strategies
- Epoch lifecycle hooks (transaction example)
- Epoch DI scopes
- **NEW**: Complete graph example with epochs (not just standalone)

#### 6. Epoch Actor Block Guide (NEW)
**File**: `epoch-actor-block.md`
**Topics**:
- What is the epoch actor block
- Works on any stream (single epoch when not configured)
- Scope rotation explained
- Why it's useful (memory management, DbContext)
- Example: Extending hello world with scope rotation

#### 7. Checkpointing Guide (ENHANCED)
**File**: `checkpointing.md` (NEW or major update to existing)
**Topics**:
- Builds on epochs (link back)
- What is checkpointing
- Configuring checkpoints
- Implementing checkpoint-aware blocks
- Resuming from checkpoints
- **Critical**: How to persist checkpoints (library doesn't do this)
- Example: Adding checkpointing to hello world

### Tier 4: Reference (EXISTING - minimal updates)
- DI Registration guide (exists, add cross-links)
- EF Core with Epochs (exists, ensure it links to epoch guide)
- Business Logic Decoupling (exists, ensure cross-linking)
- Testing Guide (exists, ensure cross-linking)

### Navigation (NEW)
**File**: `README.md` or update to INDEX.md
- Clear navigation showing the learning path
- Visual diagram of guide progression
- Quick reference table

## Success Criteria

### Documentation Quality
- ✅ Clear progressive learning path from beginner to advanced
- ✅ Consistent "hello world" baseline across all guides
- ✅ Each guide can be understood independently but references others
- ✅ Code examples are complete and runnable
- ✅ All major features covered with real-world examples

### Completeness
- ✅ Getting started guide exists and is comprehensive
- ✅ All execution contexts covered (console, ASP.NET, services)
- ✅ Epoch examples show integration in graphs, not just standalone
- ✅ Source blocks documented
- ✅ All topology patterns have focused guides
- ✅ Modern patterns (UseBlock, keyed services) documented

### Usability
- ✅ New users can go from zero to working graph in <30 minutes
- ✅ Users can discover and learn features progressively
- ✅ Cross-references make it easy to navigate between guides
- ✅ Examples show idiomatic, recommended patterns

## Validation Approach

This is a documentation-only research effort where the documentation will be directly merged. No code changes are required - we're creating new guides and refactoring existing ones.

### Review Process
1. Create guides in research folder first
2. Review with stakeholders
3. Move to `/poc/docs/guides/` after approval
4. Update existing guides for consistency
5. Create navigation/index

## Timeline

- **Phase 1**: Analysis and planning (COMPLETED)
- **Phase 2**: Create getting started guide (next)
- **Phase 3**: Create tier 2 guides (blocks, topologies)
- **Phase 4**: Create tier 3 guides (epochs, checkpointing)
- **Phase 5**: Update existing guides for consistency
- **Phase 6**: Create navigation and finalize

## Notes

- All guides will use C# syntax highlighting
- Examples will be complete and copy-paste ready
- Will maintain consistent formatting across all guides
- Cross-references will use relative paths for GitHub rendering
