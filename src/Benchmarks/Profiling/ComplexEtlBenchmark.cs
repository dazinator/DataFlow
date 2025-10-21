namespace Benchmarks.Profiling;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Benchmarks.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow;

/// <summary>
/// Sophisticated ETL benchmark that models a real-world data processing pipeline.
/// This benchmark includes:
/// - Data ingestion from multiple sources
/// - Complex transformations with validation and enrichment
/// - Batching and aggregation
/// - Routing to different targets based on category
/// - Error handling and validation
/// 
/// Uses the shared ComplexEtlDataFlow definition for consistency across tests and benchmarks.
/// Configure OpenTelemetry to export metrics for detailed performance analysis.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Config))]
public class ComplexEtlBenchmark
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Median);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
        }
    }

    private ServiceProvider _serviceProvider;

    [Params(10000)] // Number of records to process
    public int RecordCount { get; set; }

    [Params(4)] // Processing concurrency
    public int MaxConcurrency { get; set; }

    [Params(100)] // Batch size for aggregation
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        _serviceProvider = services.BuildServiceProvider();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    [Benchmark]
    public async Task ComplexEtlPipeline()
    {
        // Use shared ETL dataflow definition
        var builder = ComplexEtlDataFlow.BuildDataFlow(
            _serviceProvider,
            RecordCount,
            MaxConcurrency,
            BatchSize);

        var dataFlow = builder.Build();
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "ComplexEtlBenchmark"
        };

        await dataFlow.ExecuteAsync(context);
    }
}
