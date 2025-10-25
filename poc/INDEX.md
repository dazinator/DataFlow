# DataFlow POC - New Edge-First Design

## 🎯 Quick Start - READ THIS FIRST

**Status: ✅ POC COMPLETE - All tests passing (10/10)**

**Key Document: [SUMMARY.md](./SUMMARY.md)** - Start here for full overview

## 📁 Repository Structure

```
poc/
├── 📘 README.md                      ← Overview and design goals
├── 🏆 SUMMARY.md                     ← ⭐ START HERE - Executive summary
├── 📊 COMPARISON.md                  ← Detailed comparison with current design
├── 🏗️ ARCHITECTURE.md                ← Visual diagrams and architecture
│
├── DataFlow.POC/                     ← Core library implementation
│   ├── Core/                         ← Core abstractions (IBlock, Edge, Graph)
│   ├── Blocks/                       ← Block implementations (all 6 types)
│   └── Builder/                      ← Fluent builder API
│
└── DataFlow.POC.Tests/               ← Comprehensive test suite
    ├── BasicFlowTests.cs             ← 3 tests: producer, transform, buffering
    ├── BatchFlowTests.cs             ← 2 tests: size-based, time-window
    ├── BroadcastFlowTests.cs         ← 1 test: fanout to multiple targets
    ├── RoutingFlowTests.cs           ← 2 tests: 2-way, 3-way routing
    └── ComplexFlowTests.cs           ← 2 tests: pipeline, diamond topology
```

## 📊 Quick Stats

| Metric | Value |
|--------|-------|
| **Tests** | 10/10 passing ✅ |
| **Block Types** | 6 (Producer, Transform, Processor, Batch, Router, Broadcast) |
| **Code Lines** | ~1,657 lines (library + tests) |
| **Code Reduction** | 40-84% per block vs current design |
| **Feature Parity** | 100% for core scenarios |

## 🎯 What This POC Proves

### ✅ Separation of Concerns
- **Blocks**: Pure transformation logic (no topology awareness)
- **Edges**: Connection + buffering policy management
- **Graph**: Orchestration and execution coordination

### ✅ Simpler Code
- Transform block: 97 lines → 33 lines (**66% reduction**)
- Router block: 250 lines → 40 lines (**84% reduction**)
- Overall: Much cleaner and maintainable

### ✅ Feature Complete
All major patterns working:
- Producer → Transform → Processor pipelines
- Batching (size and time-window based)
- Broadcasting (fanout to multiple consumers)
- Routing (dynamic path selection)
- Complex topologies (diamond, merge, split)

## 📖 Reading Guide

### For Quick Overview
1. [SUMMARY.md](./SUMMARY.md) - Complete POC summary with metrics

### For Design Understanding
1. [README.md](./README.md) - Design goals and principles
2. [ARCHITECTURE.md](./ARCHITECTURE.md) - Visual diagrams

### For Detailed Comparison
1. [COMPARISON.md](./COMPARISON.md) - Side-by-side code comparison

### For Implementation Details
1. `DataFlow.POC/Core/` - Core abstractions
2. `DataFlow.POC/Blocks/` - Block implementations
3. `DataFlow.POC.Tests/` - Test scenarios

## 🚀 Running the POC

### Build
```bash
cd poc/DataFlow.POC
dotnet build
```

### Run Tests
```bash
cd poc/DataFlow.POC.Tests
dotnet test
```

Expected output:
```
Test summary: total: 10, failed: 0, succeeded: 10, skipped: 0
```

### Example Usage
```csharp
// Create blocks
var producer = new ProducerBlock<int>("source", ctx => GenerateNumbers());
var transformer = new SimpleTransformerBlock<int, string>("transform", n => $"Item-{n}");
var processor = new ProcessorBlock<string>("sink", async (item, ctx) => Console.WriteLine(item));

// Build graph
var graph = new DataFlowGraphBuilder("my-flow")
    .AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor)
    .Connect(producer, transformer, BufferMode.Bounded, 100)
    .Connect(transformer, processor, BufferMode.Bounded, 50)
    .Build();

// Execute
await graph.ExecuteAsync(context);
```

## 🎨 Key Design Pattern

```
┌─────────────────────────────────────────────────────────┐
│                    DataFlowGraph                         │
│  Owns: Topology, Channels, Orchestration                │
│                                                           │
│  Block A ──[Edge with Channel]──▶ Block B ──▶ Block C  │
│            BufferMode: Bounded                           │
│            Capacity: 100                                 │
└─────────────────────────────────────────────────────────┘

Block A:  IAsyncEnumerable<int> → IAsyncEnumerable<string>
          (Pure transformation, no topology awareness)

Edge:     Connection + Buffering Policy
          (Graph creates and manages channels)

Graph:    Wires blocks, starts execution, handles completion
          (Orchestration and coordination)
```

## ✨ Key Benefits

### Developer Experience
- ✅ **Simpler blocks** - just write transformation logic
- ✅ **Easier testing** - blocks testable in isolation
- ✅ **Better debugging** - clear separation of concerns
- ✅ **Less coupling** - blocks don't know about topology

### Architecture
- ✅ **Clean separation** - blocks, edges, graph have distinct roles
- ✅ **Extensibility** - add features at edge level (rate limiting, metrics)
- ✅ **Visibility** - full graph topology known upfront
- ✅ **Flexibility** - change buffering without changing blocks

### Operations
- ✅ **Monitoring** - graph can inspect all edge states
- ✅ **Diagnostics** - track items through edges
- ✅ **Performance** - tune buffering per edge
- ✅ **Visualization** - easy to generate topology diagrams

## 🎯 Test Scenarios Covered

### Basic Flows (3 tests)
- ✅ Producer to Processor
- ✅ Producer → Transform → Processor
- ✅ Unbuffered edges with backpressure

### Batching (2 tests)
- ✅ Size-based batching (max batch size)
- ✅ Time-window batching (periodic flush)

### Broadcasting (1 test)
- ✅ Single source to multiple consumers

### Routing (2 tests)
- ✅ Two-way routing (even/odd split)
- ✅ Three-way routing (low/medium/high)

### Complex Topologies (2 tests)
- ✅ Transform → Batch → Route pipeline
- ✅ Diamond topology (split → process → merge)

## 🔬 Next Steps (If Adopting)

1. **✅ POC Complete** - Design validated
2. **Stakeholder Review** - Get feedback on direction
3. **Performance Benchmarking** - Compare with current design
4. **Feature Gap Analysis** - Metrics, error handling, etc.
5. **Migration Strategy** - Plan transition path
6. **Production Pilot** - Test in real scenario
7. **Gradual Rollout** - Incremental adoption

## 📝 Original Issue Context

This POC addresses the GitHub issue discussion about separating concerns in the DataFlow execution model:

**Problem Statement:**
> "Each block currently handles three roles: node definition, edge management, and execution policy. When these coexist, you lose clarity about what drives what."

**Solution Demonstrated:**
- ✅ Blocks now handle **only** node definition (transformation logic)
- ✅ Edges handle connection and buffering policy
- ✅ Graph handles execution orchestration
- ✅ All major scenarios work correctly with cleaner code

## 🎓 Learning Resources

For understanding the concepts:
1. Read [ARCHITECTURE.md](./ARCHITECTURE.md) for visual explanations
2. Review test code in `DataFlow.POC.Tests/` for usage patterns
3. Study block implementations in `DataFlow.POC/Blocks/` for simplicity

## 📞 Questions?

See [SUMMARY.md](./SUMMARY.md) for:
- Detailed metrics and comparisons
- Benefits and trade-offs
- Recommendations for next steps
- Complete feature parity matrix

---

**POC Status: ✅ Complete and Successful**

**All 10 tests passing - Ready for stakeholder review**

**Key Documents:**
- 🏆 [SUMMARY.md](./SUMMARY.md) - **Start here**
- 📊 [COMPARISON.md](./COMPARISON.md) - Design comparison
- 🏗️ [ARCHITECTURE.md](./ARCHITECTURE.md) - Visual diagrams
