namespace Tests.DataFlow;

using System.Collections.Concurrent;
using System.IO;
using Benchmarks.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow.Builder.Graph;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests that generate documentation artifacts for the Complex ETL benchmark dataflow.
/// </summary>
public class ComplexEtlDiagramTests
{
    private readonly ITestOutputHelper _output;

    public ComplexEtlDiagramTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Category("Documentation")]
    public void GenerateMermaidDiagram()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        // Register required dependencies for the dataflow
        services.AddSingleton<ConcurrentBag<ComplexEtlDataFlow.ProcessedRecord>>();
        services.AddSingleton<ConcurrentBag<ComplexEtlDataFlow.AggregatedBatch>>();
        services.AddSingleton<ConcurrentDictionary<string, ConcurrentBag<ComplexEtlDataFlow.EnrichedRecord>>>();

        var serviceProvider = services.BuildServiceProvider();

        // Act - Build the dataflow
        var builder = ComplexEtlDataFlow.BuildDataFlow(
            serviceProvider,
            recordCount: 1000,
            maxConcurrency: 4,
            batchSize: 100);

        // Generate Mermaid diagram
        var mermaidDiagram = builder.Graph.ToMermaidDiagram();

        // Determine output path - relative to repository root
        var repoRoot = FindRepositoryRoot();
        var outputPath = Path.Combine(repoRoot, "docs", "diagrams", "complex-etl-benchmark.md");
        var outputDir = Path.GetDirectoryName(outputPath)!;

        // Ensure directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write diagram to file with markdown wrapper
        var markdownContent = $@"# Complex ETL Benchmark DataFlow

This diagram represents the sophisticated ETL pipeline used for benchmarking the DataFlow library.
It demonstrates real-world scenarios including:

- Data ingestion and validation
- Enrichment with external data lookups
- Category-based routing (TypeA, TypeB, TypeC)
- Multiple processing paths:
  - **TypeA**: Individual record processing
  - **TypeB**: Batch aggregation
  - **TypeC**: Category-based storage

## Pipeline Diagram

```mermaid
{mermaidDiagram}
```

## Dataflow Configuration

- **Record Count**: Configurable (default: 10,000)
- **Max Concurrency**: 4 workers per block
- **Batch Size**: 100 records
- **Routing**: 3 categories evenly distributed

## Usage

This dataflow is used in:
- `ComplexEtlBenchmark` - BenchmarkDotNet performance benchmarks
- Performance profiling with dotnet-trace and dotnet-counters
- OpenTelemetry metrics collection

See `src/Benchmarks/Shared/ComplexEtlDataFlow.cs` for the complete implementation.
";

        File.WriteAllText(outputPath, markdownContent);

        // Assert
        Assert.NotNull(mermaidDiagram);
        Assert.NotEmpty(mermaidDiagram);
        Assert.True(File.Exists(outputPath), $"Diagram file should be created at {outputPath}");

        _output.WriteLine($"Mermaid diagram generated successfully at:");
        _output.WriteLine(outputPath);
        _output.WriteLine("");
        _output.WriteLine("Diagram content:");
        _output.WriteLine(mermaidDiagram);
    }

    private static string FindRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            if (Directory.Exists(Path.Combine(currentDir, ".git")))
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        // Fallback - assume we're in the Tests directory
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."));
    }
}
