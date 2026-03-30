# CLAUDE.md — DataFlow

## Repository Overview

**DataFlow** is a high-performance, pull-based data processing pipeline library built on modern .NET using `System.Threading.Channels`. It provides a fluent API for building concurrent pipelines with natural backpressure.

### Key Architecture Principles

- **Pull-based**: downstream blocks pull from upstream when ready — never push
- **Backpressure**: automatically handled via bounded `System.Threading.Channels`
- **DI-first**: first-class dependency injection; actor model gives each concurrent worker its own DI scope (safe for `DbContext` etc.)
- **Cancellation**: every async code path must respect `CancellationToken` — never ignore it

### Block Types

| Type | Description |
|------|-------------|
| Source | Supplies data (e.g. `ProducerBlock`) |
| Propagator | Transforms/routes data (e.g. `TransformBlock`, `BatchBlock`) |
| Target | Terminal consumer (e.g. `ProcessorBlock`) |

### Repository Structure

| Path | Contents |
|------|----------|
| `src/` | Production library code |
| `poc/` | POC / evolving architecture (Blazor visualisation etc.) |
| `poc/DataFlow.Blazor/` | Blazor WASM visualisation components |
| `poc/DataFlow.Blazor.Server/` | ASP.NET Core server (SignalR, EF persistence) |
| `poc/DataFlow.Blazor.Shared/` | Shared models / events / snapshots |
| `src/Tests/` | Unit and integration tests |
| `research/` | Research findings and handover documents |
| `.github/` | GitHub configuration and Copilot instructions |

---

## Building and Testing

```bash
# Restore tools
dotnet tool restore

# Build
dotnet build

# Run all tests
dotnet test

# Run a specific category
dotnet test --filter "Category=UnitTest"

# Format
dotnet format
```

- Target framework: **.NET 8.0**
- SDK version pinned in `src/global.json`

---

## Testing Requirements

**Always check test coverage for any change you make.**

- Bug fixes must include a test that would have caught the bug — a regression test proving the fix is correct.
- New features must include tests covering the happy path and key edge cases.
- If an existing test file covers the area you changed, add to it rather than creating a new file.
- Do not modify working code without tests that validate the change.

---

## Coding Standards

- Use `await` everywhere — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Always propagate `CancellationToken` through async call chains
- Do not add external dependencies without clear justification
- Do not break the pull-based architecture (no push-based workarounds)
- Do not use synchronous blocking in async code paths
- Do not create new block types without discussing the design first
- Keep changes minimal and focused — do not refactor surrounding code unless asked
- Do not add comments or docstrings to code you didn't change
- AGPL-3.0 compatibility required for all suggested code

---

## Common Patterns

### Defining a Flow

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

### Implementing a Producer

```csharp
public class MyProducer : IProducer<int>
{
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

## Performance Notes

- Concurrency: configurable via actor pools
- Memory: object pooling used where appropriate (e.g. `BatchBlock`)
- Order: single transformer preserves order; multiple concurrent transformers may interleave
- Performance target: within ~10% of TPL Dataflow

---

## License

Dual-licensed:
- **Non-commercial**: AGPL-3.0
- **Commercial**: separate license required
