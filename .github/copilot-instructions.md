# GitHub Copilot Instructions for DataFlow

**Version**: 2.0 (Phase 4 - Layered Architecture)  
**Last Updated**: 2025-11-12

---

## Required Context

**⚠️ CRITICAL - READ FIRST**: This orchestration layer (Layer 0) imports and coordinates all other layers. Before proceeding, you must understand:

### Layer 3: Kernel (Platform Abstraction)

**📖 [Kernel Layer](../.team/kernel/README.md)** - Platform-specific operations abstracted into semantic operations

**What It Provides**:
- 12 semantic operations for work item management
- Platform drivers (GitHub, Azure DevOps planned)
- Abstracts away platform-specific details

**When You Use It**: Never directly - always through semantic operations referenced in procedures and duties

### Layer 1: Global Procedures (Platform-Agnostic)

**📖 [Global Procedures](../.team/procedures/README.md)** - Reusable, platform-agnostic procedures

**What It Provides**:
- Duty assignment logic
- Multi-phase work item handling
- Self-improvement feedback
- Work item creation patterns
- Handover procedures
- Comment patterns

**When You Use It**: Referenced by duties and orchestration for common operations

### Layer 0: Orchestration (This Document)

**Purpose**: Entry point that:
1. Loads required context (kernel, procedures)
2. Determines which duty should handle the work
3. Dispatches to the appropriate duty
4. Provides repository-specific guidance

---

## Quick Navigation

**Start here based on your task:**

### For New Work Items (All Duties)

**⚠️ CRITICAL - CHECK THE DUTY LABEL FIRST:**
- **ALWAYS check the work item's duty label BEFORE doing any other analysis**
- The duty label (`workflow:triage`, `workflow:research`, `workflow:implementation`, etc.) **takes precedence over work item content**
- **DO NOT** assume duty type from work item description - the label is the authoritative source
- **DO NOT** proceed without checking the label - this is your primary directive
- Query your duty queue to find assigned work items
- See [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for querying and handover patterns

**Comment Prefix Convention:**
- Prefix ALL comments and responses with `[Copilot-Duty: <duty-name>]` to verify you've checked the label
- Example: `[Copilot-Duty: Research] I've analyzed the approach...`
- This serves as confirmation that you followed the label-first directive

### By Duty Type

**📖 Use the [Duty Assignment Procedure](../.team/procedures/duty-assignment.md) to determine the correct duty, then dispatch:**

1. **Triage Duty** (assess and route new work items)
   → See [Triage Duty](../.team/duties/TRIAGE_DUTY.md)

2. **Research Duty** (validate approaches, create specifications)
   → See [Research Duty](../.team/duties/RESEARCH_DUTY.md)

3. **Implementation Duty** (implement validated designs)
   → See [Implementation Duty](../.team/duties/IMPLEMENTATION_DUTY.md)

4. **Tech Debt Duty** (discover and address technical debt)
   → See [Tech Debt Duty](../.team/duties/TECH_DEBT_DUTY.md)

5. **Product Prioritization Duty** (prioritize backlog items)
   → See [Product Prioritization Duty](../.team/duties/PRODUCT_PRIORITIZATION_DUTY.md)

6. **Process Modeling Duty** (improve workflows and processes)
   → See [Process Modeling Duty](../.team/duties/PROCESS_MODELING_DUTY.md)

7. **Unassigned Duty** (handle work items where duty cannot be inferred)
   → See [Unassigned Duty](../.team/duties/UNASSIGNED_DUTY.md)

**⚠️ ALWAYS complete self-improvement evaluation before PR review** (see [Self-Improvement Loop](#self-improvement-loop) below)

**⚠️ ALWAYS check if your work item is part of a multi-phase plan** - See [Multi-Phase Work Item Procedures](../.team/procedures/multi-phase-work-items.md)

---

## Duty Assignment and Dispatch

### Step 1: Determine Duty Assignment

**Use the [Duty Assignment Procedure](../.team/procedures/duty-assignment.md)** to determine which duty should handle the current work item.

### Step 2: Dispatch to Appropriate Duty

Once duty is determined, **load and follow the corresponding duty procedure**:

| Duty | File Path | Purpose |
|------|-----------|---------|
| `triage` | [Triage Duty](../.team/duties/TRIAGE_DUTY.md) | Assess and route work items |
| `research` | [Research Duty](../.team/duties/RESEARCH_DUTY.md) | Validate approaches, create specs |
| `implementation` | [Implementation Duty](../.team/duties/IMPLEMENTATION_DUTY.md) | Implement validated designs |
| `tech-debt` | [Tech Debt Duty](../.team/duties/TECH_DEBT_DUTY.md) | Discover and document technical debt |
| `product-backlog` | [Product Prioritization Duty](../.team/duties/PRODUCT_PRIORITIZATION_DUTY.md) | Prioritize backlog items |
| `process-modeling` | [Process Modeling Duty](../.team/duties/PROCESS_MODELING_DUTY.md) | Improve workflows and processes |
| `None` or unrecognized | [Unassigned Duty](../.team/duties/UNASSIGNED_DUTY.md) | Handle edge cases |

**Each duty provides**:
- Complete step-by-step procedures
- Semantic operations to use
- Handover patterns to other duties
- Common scenarios and examples

---

## ⚠️ CRITICAL: Duty Labels and File Ownership

### Supported Duty Labels

**⚠️ REQUIRED READING**: Before labeling any work items, you MUST read the authoritative label schema:

**📖 [Workflow Topology Guide - Label Schema](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md#label-schema)**

**Critical Rules:**

1. **ONLY use labels from the Label Schema** - do not invent new ones
2. **Use exact format**: `workflow:<name>` with a colon (`:`) separator
3. **One duty label per work item** - work items have exactly ONE duty label at a time

**❌ Common Mistakes to Avoid:**
- `workflow-implementation` ❌ (incorrect - use `workflow:implementation`)
- `implementation-workflow` ❌ (incorrect - use `workflow:implementation`)
- `workflow_implementation` ❌ (incorrect - use `workflow:implementation`)
- Inventing new duty labels not in the schema ❌

**The Label Schema is the single source of truth** - always reference it before creating or updating work item labels.

### Duty File Ownership

**⚠️ CRITICAL - Process Modeling Owns Duty Files:**

The following files are **EXCLUSIVELY OWNED** by the Process Modeling duty:

- `.team/duties/*_DUTY.md` (all duty documentation files)
- `.team/procedures/*.md` (all global procedure files)
- `.team/kernel/**/*.md` (all kernel layer files)
- `.github/copilot-instructions.md` (this file)
- `.github/ISSUE_TEMPLATE/*.md` (issue templates)
- `.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md` (workflow system docs)

**If you're in ANY other duty and encounter tasks involving these files:**

1. ✅ **STOP** - Do not update these files yourself
2. ✅ **Create or update a Process Modeling work item** for the changes needed
3. ✅ **Hand over** to process modeling duty
4. ✅ **Document** what changes are needed and why

**See**: [Handover Procedure](../.team/procedures/handover.md) for complete handover guidance.

**Why This Matters:**
- Duty files are tested through tabletop simulation
- Changes need validation across all duty scenarios
- Process modeling ensures consistency and quality
- Prevents duty documentation from becoming fragmented or contradictory

---

## Self-Improvement Loop

**⚠️ CRITICAL**: Before marking ANY PR ready for review, you MUST complete the self-improvement evaluation.

**📖 See**: [Self-Improvement Procedure](../.team/procedures/self-improvement.md) for complete details.

### Why This Matters

This self-improvement loop ensures our duties continuously evolve based on real experiences. Every agent's feedback helps improve the process for future work.

---

## Repository Overview

**DataFlow** is a high-performance, pull-based data processing pipeline library built on modern .NET features using `System.Threading.Channels`. It provides a fluent API for building concurrent data processing pipelines with efficient backpressure handling.

### Key Architecture Principles

**Pull-Based Architecture:**
- Downstream blocks pull data from upstream when ready
- Provides natural backpressure
- All blocks communicate via `System.Threading.Channels`

**Block System:**
- **Source Blocks** - supply data (e.g., `ProducerBlock`)
- **Propagator Blocks** - process and pass data (e.g., `TransformBlock`, `BatchBlock`)
- **Target Blocks** - terminal consumers (e.g., `ProcessorBlock`)

**Dependency Injection & Scoping:**
- First-class DI support throughout
- Actor model: each concurrent worker runs in its own DI scope
- Allows scoped dependencies (like `DbContext`) to be used safely in concurrent processing

### Repository Structure

**Key Directories**:
- `.github/` - GitHub configuration and this orchestration file
- `.team/` - Layered architecture (kernel, procedures, duties)
- `/research/` - Research findings and handovers
- `/implementation/` - In-flight implementation tracking
- `/poc/` - POC code (evolving architecture)
- `/src/` - Production code

**For detailed structure**: See duty-specific documentation (each duty references relevant directories)

---

## Research vs Implementation

**How to identify which duty to follow:**

| Indicator | Research | Implementation |
|-----------|----------|----------------|
| Work item label | `research` OR `workflow:research` | `implementation` OR `workflow:implementation` |
| Work item contains | "⚠️ This is a research work item" | "⚠️ This is an implementation work item" |
| Purpose | Validate approach, create specs | Implement validated design |
| Code fate | REVERTED after approval | MERGED into codebase |
| Primary output | Documentation + handover work item | Working code |
| Duty doc | [Research Duty](../.team/duties/RESEARCH_DUTY.md) | [Implementation Duty](../.team/duties/IMPLEMENTATION_DUTY.md) |

**When in doubt:** Ask "Is this validating an approach (research) or implementing a validated design (implementation)?"

---

## Duty Topology System

DataFlow uses a **GitHub label-based duty topology system** to track which duty a work item belongs to and provide formal handover mechanisms.

### Duty Labels

All open work items have exactly ONE duty label:

- `workflow:triage` - Awaiting assessment and routing
- `workflow:research` - Research and validation
- `workflow:implementation` - Implementation work
- `workflow:tech-debt` - Technical debt discovery/analysis
- `workflow:product-backlog` - Prioritization needed
- `workflow:process-modeling` - Process improvements

### Querying Your Duty Queue

**Always start by querying your duty queue to find assigned work items.**

### Querying Your Duty Queue and Handovers

**📖 See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on:
- Duty label schema
- Handover patterns
- Querying duty queues
- Common transitions
- Troubleshooting

**📖 See**: [Handover Procedure](../.team/procedures/handover.md) for handover guidance.

---

## Common Patterns

### Defining a Data Flow
```csharp
public class MyFlowConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        builder
            .AddProducer<int>("source", sp => sp.GetRequiredService<MyProducer>())
            .AddBatch<int>("batcher", maxBatchSize: 100, windowPeriod: TimeSpan.FromSeconds(5))
            .ReceiveFrom("source")
            .AddProcessor<int[]>("writer", sp => sp.GetRequiredService<MyBatchWriter>())
            .ReceiveFrom("batcher");
    }
}
```

### Implementing Block Dependencies
```csharp
public class MyProducer : IProducer<int>
{
    private readonly ILogger<MyProducer> _logger;
    
    public MyProducer(ILogger<MyProduducer> logger) => _logger = logger;
    
    public async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Delay(10, cancellationToken);
        }
    }
}
```

---

## Graph Management

**📖 See**: [Testing Framework - Graph Management](../docs/design/prompt-engineering/testing-framework.md#graph-management) for graph update procedures.

**Update Process**: See Process Modeling Duty for graph update procedures.

---

## Don't Do

- Don't add new external dependencies without careful consideration
- Don't break the pull-based architecture principles
- Don't use synchronous blocking operations in async code paths
- Don't ignore cancellation tokens
- Don't modify working code without tests that validate the changes
- Don't use `Task.Result` or `.Wait()` - always use `await`
- Don't create new block types without discussing the design first
- Don't use platform-specific operations (GitHub MCP tools) - use semantic operations instead
- Don't duplicate procedure logic in duties - reference the procedure instead

---

## Building and Testing

### Commands
```bash
# Restore tools
dotnet tool restore

# Build
dotnet build

# Run tests
dotnet test

# Run specific test category
dotnet test --filter "Category=UnitTest"

# Format code
dotnet format
```

### .NET SDK
- Target: .NET 8.0
- SDK requirement in `src/global.json`

---

## Performance Considerations

- **Concurrency**: Configurable max concurrency via actor pools
- **Backpressure**: Automatically handled via bounded channels
- **Memory**: Object pooling where appropriate (e.g., `BatchBlock`)
- **Order Preservation**:
  - Single transformer preserves order
  - Multiple concurrent transformers may interleave
  - Batch blocks maintain order

---

## License

**Dual-licensed project:**
- **Non-Commercial**: AGPL-3.0
- **Commercial**: Requires separate license

When suggesting code changes, ensure AGPL-3.0 compatibility.

---

## Helpful Context

- Similar to TPL Dataflow but with modern .NET features
- Performance competitive with TPL Dataflow (within ~10%)
- Builder pattern is fluent and chainable
- Blocks connected via `.ReceiveFrom()` for pipeline topology
- Emphasizes correctness and composability over raw speed

---

## Getting Help

**Duty Questions:**
- All duties: See "By Duty Type" section at the top of this document
- Implementation tracking: `/implementation/README.md`

**Code Questions:**
- POC architecture: `/poc/README.md`
- Production code: `/src/` (consult specific README files)

**Process Improvements:**
- See [Self-Improvement Procedure](../.team/procedures/self-improvement.md) for complete details
- Use `submit_feedback` semantic operation
- Self-improvement evaluation required before PR review

---

## Design References

This orchestration layer implements the design documented in:

- **[Main Design](../docs/design/prompt-engineering/README.md)** - Complete prompt engineering architecture
- **[Core Concepts](../docs/design/prompt-engineering/concepts.md)** - Layer 0 (Orchestration) definition
- **[Semantic Language](../docs/design/prompt-engineering/semantic-language.md)** - Semantic operations reference
- **[Testing Framework](../docs/design/prompt-engineering/testing-framework.md)** - Testing methodology and change procedures

