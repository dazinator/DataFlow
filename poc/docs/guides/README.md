# DataFlow POC - User Guides

Welcome to the DataFlow POC documentation! This collection of guides will help you master DataFlow from zero to advanced usage.

## 🚀 Getting Started

**New to DataFlow?** Start here:

1. **[Getting Started](./getting-started.md)** - Your first DataFlow pipeline
   - Installation and setup with global usings
   - Core concepts: Blocks, Edges, and Graphs
   - Building your first pipeline with the canonical DI-based API
   - Executing graphs in console, ASP.NET, and background services
   - Real-world example with Entity Framework Core

## 📚 Core Concepts

Build foundational knowledge:

2. **[Working with Blocks](./working-with-blocks.md)** - Block types and patterns
   - Source, Transform, and Processor blocks
   - Actor vs. non-actor blocks
   - The UseBlock pattern (recommended)
   - Block registration with dependency injection

3. **[Dependency Injection Registration](./dependency-injection-registration.md)** - Master the DI system
   - Namespace organization
   - Block and graph registration
   - Keyed services
   - Advanced DI patterns

4. **[Testing Guide](./testing-guide.md)** - Test your pipelines
   - Unit testing actors
   - Integration testing graphs
   - Mocking and test helpers

## 🔀 Topologies

Learn different graph patterns:

5. **[Broadcast Topology](./topology-broadcast.md)** - Fan-out to multiple consumers
   - One-to-many patterns
   - Parallel processing
   - Result aggregation

6. **[Control Flow Topologies](./control-flow-topologies.md)** - Advanced routing patterns
   - Competing consumers (load balancing)
   - Selective routing (content-based)
   - Complex graph structures

## 🔧 Data Sources

Create custom data sources:

7. **[Source Blocks](./source-blocks.md)** - Building data sources
   - Database sources with Entity Framework
   - File-based sources
   - API/HTTP sources
   - Epoch vs. non-epoch sources
   - Error handling and resilience

## 🎯 Advanced Features

Master advanced DataFlow capabilities:

8. **[Using Epochs](./using-epochs.md)** - Transaction boundaries and coordination
   - What are epochs?
   - Epoch segmentation strategies
   - Lifecycle hooks (OnBegin, OnCommit, OnRollback)
   - Epoch-scoped DI services
   - Use cases: transactions, batching, checkpointing

9. **[Epoch Actor Block](./epoch-actor-block.md)** - Scope rotation for memory management
   - Understanding scope rotation
   - When to use epoch actor blocks
   - Complete graph integration examples
   - Entity Framework Core lifecycle management
   - Best practices

10. **[EF Core with Epochs](./ef-core-epochs.md)** - Database transactions with DataFlow
    - Transaction management
    - Scoped DbContext usage
    - Commit/rollback patterns
    - Production examples

11. **[Checkpointing](./checkpointing.md)** - Resume processing from checkpoints *(Coming Soon)*
    - Configuring checkpointing
    - Implementing checkpoint-aware blocks
    - Persisting checkpoint data
    - Recovery scenarios

## 🏗️ Migration & Architecture

Understand the architecture and migration:

12. **[Business Logic Decoupling](./business-logic-decoupling.md)** - Separate concerns
    - Decoupling patterns
    - Actor design principles
    - Domain-driven design with DataFlow

13. **[Migrating from Current Design](./migrating-from-current-design.md)** - Upgrade guide
    - Breaking changes
    - Migration strategies
    - Before/after examples

---

## 📖 Recommended Learning Path

### Beginner Path (Zero to First Pipeline)
1. Getting Started
2. Working with Blocks
3. Testing Guide
4. Broadcast Topology

### Intermediate Path (Building Production Systems)
5. Dependency Injection Registration
6. Source Blocks
7. Control Flow Topologies
8. Business Logic Decoupling

### Advanced Path (Transactions & Checkpointing)
9. Using Epochs
10. Epoch Actor Block
11. EF Core with Epochs
12. Checkpointing *(when available)*

---

## 🎓 Quick Reference

### Common Patterns

**Global Usings Setup**
```csharp
// GlobalUsings.cs
global using DataFlow.POC.Core;
global using DataFlow.POC.Builder;
global using DataFlow.POC.Blocks;
global using DataFlow.POC.DependencyInjection;
global using DataFlow.POC.Actors;
global using Microsoft.Extensions.DependencyInjection;
```

**Basic Registration Pattern**
```csharp
builder.Services.AddDataFlows("app", df =>
{
    df.AddActorBlock<TIn, TOut, TActor>("block-name");
    
    df.AddGraph("graph-name", g =>
    {
        g.UseBlock("block-name");
    });
});
```

**Graph Resolution**
```csharp
var graph = services.GetKeyedService<DataFlowGraph>("app:graph-name");
var context = new ExecutionContext(services, cancellationToken);
await graph!.ExecuteAsync(context);
```

### Key Concepts

- **Block**: A unit of processing (source, transform, or processor)
- **Edge**: A connection between blocks (data flow path)
- **Graph**: A complete pipeline topology
- **Actor**: Business logic implementation for a block
- **Epoch**: A transaction/batch boundary for coordinated processing
- **UseBlock**: Recommended pattern for referencing registered blocks by name

---

## 🆘 Getting Help

**Documentation Issues?**
- Check the [Getting Started](./getting-started.md) troubleshooting section
- Review [Testing Guide](./testing-guide.md) for testing patterns

**Architecture Questions?**
- See [Business Logic Decoupling](./business-logic-decoupling.md)
- Review [Dependency Injection Registration](./dependency-injection-registration.md)

**Need Examples?**
- All guides include complete, runnable examples
- Check `/poc/DataFlow.POC.Tests/Documentation/` for verified code samples

---

## 📝 Documentation Standards

All guides in this collection follow these standards:

✅ **Complete Examples**: Every code snippet is complete and runnable  
✅ **Test Coverage**: All examples are verified via automated tests  
✅ **Progressive**: Builds on previous concepts incrementally  
✅ **Production-Ready**: Uses idiomatic, DI-based patterns  
✅ **Cross-Linked**: Related guides are linked for easy navigation  

---

**Last Updated**: 2025-11-25  
**DataFlow Version**: POC (Pre-release)
