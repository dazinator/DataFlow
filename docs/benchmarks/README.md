# Benchmark Results

This directory contains historical benchmark results for the DataFlow library.

## How Benchmarks Are Run

Benchmarks are executed via GitHub Actions workflow that can be triggered manually. The workflow runs the benchmarks and commits the results to this directory.

## Benchmark Types

The following benchmarks are available:

- **minimal** - Benchmark a minimal data flow vs TPL with simple 100 items doing pretty much nothing
- **simple** - Benchmark a simple data flow against TPL with varying workload and concurrency
- **batch** - BatchBlock comparison benchmark (old vs new implementation)
- **transform-model** - TransformBlock execution model comparison benchmark
- **transform-memory** - TransformBlock memory usage benchmark
- **memory-rate** - RateLimitBlock memory usage benchmark
- **diagnose** - Diagnostic test (not a traditional benchmark)

## Running Benchmarks Locally

To run benchmarks locally:

```bash
cd src/Benchmarks
dotnet run -c Release -- [benchmark-name]
```

For example:
```bash
dotnet run -c Release -- simple
```

## File Naming Convention

Benchmark results are stored with the following naming pattern:
```
{benchmark-name}_{timestamp}.txt
```

For example: `simple_20250115_143022.txt`

## Comparing Results

To compare benchmark performance over time:

1. Look at the git history of this directory
2. Compare results from different timestamps
3. Check for performance regressions or improvements

You can use git to view historical versions:
```bash
git log -- docs/benchmarks/
git show <commit-hash>:docs/benchmarks/<filename>
```
