# GitHub Copilot Instructions for DataFlow

## Quick Navigation

**Start here based on your task:**

1. **Research Task** (validate approaches, create specifications)
   → See `.github/workflows/RESEARCH_WORKFLOW.md`

2. **Implementation Task** (implement validated designs)
   → See `.github/workflows/IMPLEMENTATION_WORKFLOW.md`

3. **POC Work** (evolving architecture exploration)
   → See section below, then follow appropriate workflow

4. **Continuing Existing Work**
   → Check `/implementation/plan.md` first

**⚠️ ALWAYS complete self-improvement evaluation before PR review** (see below)

---

## Self-Improvement Loop

**⚠️ CRITICAL**: Before marking ANY PR ready for review, you MUST complete the self-improvement evaluation.

### Required Steps Before PR Review

1. **Evaluate Workflow Effectiveness**: Reflect on the workflow you followed
   - Which workflow did you use? (Research, Implementation, POC, etc.)
   - Did the workflow guidance help or hinder progress?
   - Were there missing instructions that would have been helpful?

2. **Document What Worked Well**: Identify positive aspects
   - What workflow steps were clear and effective?
   - What guidance helped you avoid mistakes?
   - What tools or processes worked smoothly?

3. **Document What Didn't Work Well**: Identify pain points
   - What was unclear or confusing?
   - What steps were missing or incomplete?
   - What caused delays or required iteration?

4. **Propose Specific Improvements**: Be actionable and concrete
   - How could workflow documentation be improved?
   - What new guidance should be added?
   - Should issue templates be updated?

5. **Add to Workflow Improvements File**:
   - Open `.github/workflow-improvements.md`
   - Check if your suggestion already exists
   - If new, add your evaluation using the template format

**Example Entry:**
```markdown
- **Date**: 2025-11-05
- **Issue/PR**: #123
- **What worked well**: Handover document template was comprehensive
- **What didn't work well**: No guidance on when to create benchmarks
- **Suggested improvement**: Add decision tree for "When to Create Benchmarks"
```

### Why This Matters

This self-improvement loop ensures our workflows continuously evolve based on real experiences. Every agent's feedback helps improve the process for future work.

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

```
.github/
├── workflows/
│   ├── RESEARCH_WORKFLOW.md         # Research process
│   └── IMPLEMENTATION_WORKFLOW.md   # Implementation process
├── copilot-instructions.md          # This file (navigation hub)
└── workflow-improvements.md         # Self-improvement tracking

/research/                           # Research findings and handovers
├── FOLDER_STRUCTURE.md             # Research folder conventions
└── [topic]/                        # Per-topic research folders

/implementation/                     # In-flight implementation tracking
├── README.md                       # Implementation folder guide
├── plan.md                         # Current implementation plan (if any)
└── archive/                        # Completed implementation plans

/poc/                               # POC code (evolving architecture)
/src/                               # Production code
```

---

## Coding Standards

### C# Style
- Use **file-scoped namespaces** (`namespace Uniun.DataFlow;`)
- Use **var** for local variable declarations when type is apparent
- Prefer **expression-bodied members** where appropriate
- Use **implicit object creation** when type is apparent (`new()`)
- Use **primary constructors** for simple cases
- Follow `.editorconfig` rules in `src/.editorconfig`
- 4 spaces for indentation
- Place `using` directives **inside namespace** (except global usings)

### Async/Await Patterns
- All data processing operations are async
- Use `IAsyncEnumerable<T>` for streaming operations
- Use `ValueTask<T>` for hot-path operations where appropriate
- Always respect `CancellationToken` - pass it through all async operations
- Use `ConfigureAwait(false)` in library code (not in test code)

### Testing Standards

**Test Categories:**
- `[UnitTest]` - Fast, isolated unit tests
- `[IntegrationTest]` - Tests involving multiple components
- `[Category("Performance")]` - Performance/benchmark tests
- `[Exploratory]` - Exploratory or diagnostic tests

**Test Pattern:**
```csharp
public class MyBlockTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public MyBlockTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }
    
    public IServiceCollection Services { get; }
    public ITestOutputHelper Output => _testOutputHelper;
}
```

**Test Naming:** Use descriptive names like `Should_ProcessItemsConcurrently_When_MaxConcurrencyIsSet()`

**Assertions:** Use `Shouldly` - `result.ShouldBe(expected)`, `list.ShouldContain(item)`

---

## Research vs Implementation

**How to identify which workflow to follow:**

| Indicator | Research | Implementation |
|-----------|----------|----------------|
| Issue label | `research` | `implementation` |
| Issue contains | "⚠️ This is a research issue" | "⚠️ This is an implementation issue" |
| Purpose | Validate approach, create specs | Implement validated design |
| Code fate | REVERTED after approval | MERGED into codebase |
| Primary output | Documentation + handover issue | Working code |
| Workflow doc | `.github/workflows/RESEARCH_WORKFLOW.md` | `.github/workflows/IMPLEMENTATION_WORKFLOW.md` |

**When in doubt:** Ask "Is this validating an approach (research) or implementing a validated design (implementation)?"

---

## POC Work Guidelines

The `/poc` folder contains evolving architectural explorations. Work in POC follows either:
- **Research workflow** if validating new approaches (code will be reverted)
- **Implementation workflow** if implementing validated designs (code will be merged)

### POC-Specific Requirements

When implementing in POC:

**Documentation Structure** (`/poc/docs/`):
- `/poc/docs/plans/` - Action plans and proposals
- `/poc/docs/design/` - Architecture and design docs
- `/poc/docs/guides/` - Implementation patterns
- `/poc/docs/adr/` - Architecture Decision Records
- `/poc/docs/POC_GLOSSARY.md` - Terminology (keep updated)

**Key Guidelines:**
1. Read `/poc/README.md` and `/poc/docs/POC_GLOSSARY.md` first
2. Create plan in `/poc/docs/plans/` for each POC implementation
3. Use ADRs in `/poc/docs/adr/` for significant decisions
4. Update glossary with new terminology
5. Use POC test projects (`DataFlow.POC.Tests`)
6. Document benchmarks in `/research/` if conducting performance analysis

See `/poc/README.md` for complete POC architecture details.

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
    
    public MyProducer(ILogger<MyProducer> logger) => _logger = logger;
    
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

## Documentation Standards

### Diagram Preferences

**ALWAYS prefer Mermaid diagrams** where visualization helps understanding:
- ✅ Use mermaid flowcharts for process flows and decision trees
- ✅ Use mermaid sequence diagrams for interaction flows
- ✅ Use mermaid graphs for architecture and data flow
- ❌ Avoid ASCII art diagrams (hard to maintain, less clear)

**Example:**
````markdown
```mermaid
flowchart LR
    A[Source] --> B[Transform]
    B --> C[Sink]
```
````

---

## Don't Do

- Don't add new external dependencies without careful consideration
- Don't break the pull-based architecture principles
- Don't use synchronous blocking operations in async code paths
- Don't ignore cancellation tokens
- Don't modify working code without tests that validate the changes
- Don't use `Task.Result` or `.Wait()` - always use `await`
- Don't create new block types without discussing the design first

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

**Workflow Questions:**
- Research process: `.github/workflows/RESEARCH_WORKFLOW.md`
- Implementation process: `.github/workflows/IMPLEMENTATION_WORKFLOW.md`
- Implementation tracking: `/implementation/README.md`

**Code Questions:**
- POC architecture: `/poc/README.md`
- Production code: `/src/` (consult specific README files)

**Process Improvements:**
- Add suggestions to `.github/workflow-improvements.md`
- Self-improvement evaluation required before PR review
