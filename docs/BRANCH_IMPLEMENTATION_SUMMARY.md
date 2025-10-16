# Branch Builder Implementation

## Overview

The branch builder feature enables creating named, independent paths in DataFlow pipelines. Branches are particularly useful with BroadcastBlock for fanout scenarios.

## API Reference

### Types

**`IBranchBuilder`** - Interface for branch builders
- Properties: `BranchName`, `BranchIndex`, `ParentBlockAtCreation`
- Methods: Same as `StructuredDataFlowBuilder` (AddProcessor, AddTransform, etc.)

**`BranchBuilder`** - Implementation
- Wraps parent builder while maintaining independent "last block" state
- Automatically adds branch metadata to all blocks created within it

**`DiagramRenderOptions`** - Controls diagram rendering
- `CollapseConcurrentBranches` - Enable branch collapsing (default: false)
- `MaxBranchesToShowIndividually` - Collapse threshold (default: 5)
- `GroupBranchesInSubgraphs` - Render branches in subgraphs (default: true)

### Methods

**`AddBranch(string? branchName = null, string? startFromBlock = null)`**
- Creates a new branch
- Branch name is optional (auto-generated if not provided)
- Auto-generated format: `{flowName}-{currentBlock}-branch-{index}`

**`GetBranch(string branchName)`**
- Retrieves an existing branch by name

**`GetBranches()`**
- Returns all created branches

## Key Features

### Branch Metadata Tracking

Each block created within a branch automatically has metadata added:
- `BranchName` - The branch's name
- `BranchIndex` - The branch's index (0-based)
- `ParentBlockAtCreation` - The parent's current block when branch was created

This metadata enables:
- Smart diagram rendering (grouping, collapsing)
- Future features (branch merging, dynamic routing)
- Enhanced observability (per-branch metrics)

### DI Scoping

Each branch block operates as an independent actor with its own DI scope, safely supporting scoped dependencies like `DbContext`.

### Diagram Rendering

Branches are rendered as Mermaid subgraphs:
- Below threshold: individual subgraphs with full names
- Above threshold: collapsed subgraph with "..N" label
- Type information shown on connections only

## Usage Example

```csharp
var builder = new StructuredDataFlowBuilder(sp, "MyFlow");

// Source
builder.AddProducer<int>("source", sp => new MyProducer());

// Broadcast for fanout
builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

// Create branches with auto-generated names
for (int i = 0; i < 5; i++)
{
    var branch = builder.AddBranch(); // Names: "MyFlow-fanout-branch-0", etc.
    branch.AddProcessor<int>($"processor-{i}", sp => new MyProcessor())
        .ReceiveFrom("fanout");
}
```

## Future Enhancements

### BufferBlock for Competing Consumers

A planned BufferBlock implementation will enable load distribution (competing consumer semantics) with branches, where each item is processed by ONE branch instead of ALL branches.

### Branch Merging

Future support for merging multiple branch outputs back together using BufferBlock or similar constructs.
