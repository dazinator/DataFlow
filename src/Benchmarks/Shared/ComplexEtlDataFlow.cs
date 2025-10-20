namespace Benchmarks.Shared;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder;
using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Shared complex ETL dataflow definition used for benchmarking and testing.
/// This models a realistic data processing pipeline with multiple stages:
/// - Data ingestion and validation
/// - Enrichment with external data
/// - Routing based on record category (TypeA, TypeB, TypeC)
/// - Different processing paths (individual, batch aggregation, category storage)
/// </summary>
public static class ComplexEtlDataFlow
{
    /// <summary>
    /// Builds the complete ETL dataflow using StructuredDataFlowBuilder
    /// </summary>
    public static StructuredDataFlowBuilder BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4,
        int batchSize = 100)
    {
        var builder = new StructuredDataFlowBuilder(serviceProvider, "ComplexEtlBenchmark");

        var blockOptions = new BlockOptions
        {
            MaxConcurrency = maxConcurrency,
            Capacity = 100
        };

        // Source: Generate raw data records
        builder.AddProducer<RawRecord>("data-source",
            (IServiceProvider sp) => new DataSourceProducer(recordCount));

        // Transform: Parse and validate records
        builder.AddTransform<RawRecord, ValidatedRecord>("validator", sp =>
            ActivatorUtilities.CreateInstance<ValidationTransformer>(sp),
            new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
            .ReceiveFrom("data-source");

        // Transform: Enrich with additional data
        builder.AddTransform<ValidatedRecord, EnrichedRecord>("enricher", sp =>
            ActivatorUtilities.CreateInstance<EnrichmentTransformer>(sp),
            new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
            .ReceiveFrom("validator");

        // Broadcast: Fan out enriched records for parallel processing
        // This demonstrates how a single enriched record can be processed by multiple paths simultaneously
        builder.AddBroadcast<EnrichedRecord>("broadcast")
            .ReceiveFrom("enricher");

        // Broadcast Fan-out Path 1: Metrics collector (lightweight processing)
        Uniun.DataFlow.Builder.Graph.StructuredDataFlowBuilderExtensions.AddProcessor<EnrichedRecord>(
            builder, "metrics-collector", (IServiceProvider sp) =>
                ActivatorUtilities.CreateInstance<MetricsCollectorProcessor>(sp),
            new BlockOptions { MaxConcurrency = 1, Capacity = 100 })
            .ReceiveFrom("broadcast");

        // Broadcast Fan-out Path 2: Audit logger (compliance tracking)
        Uniun.DataFlow.Builder.Graph.StructuredDataFlowBuilderExtensions.AddProcessor<EnrichedRecord>(
            builder, "audit-logger", (IServiceProvider sp) =>
                ActivatorUtilities.CreateInstance<AuditLoggerProcessor>(sp),
            new BlockOptions { MaxConcurrency = 2, Capacity = 100 })
            .ReceiveFrom("broadcast");

        // Routing: Route enriched records by category (TypeA, TypeB, TypeC)
        // Note: Router also receives from broadcast to demonstrate multiple consumers
        builder.AddRouter<EnrichedRecord>("router", record => record.Category)
            .ReceiveFrom("broadcast")
            .RegisterRoute("TypeA", context =>
            {
                var routeBuilder = context.RouteBuilder;

                // TypeA route: Process individual records
                routeBuilder.AddTransform<EnrichedRecord, ProcessedRecord>("processor", sp =>
                    ActivatorUtilities.CreateInstance<RecordProcessor>(sp),
                    new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
                    .AsEntry()
                    .AddProcessor("record-writer", sp =>
                        new RecordWriter(),
                        blockOptions);

                return routeBuilder;
            })
            .RegisterRoute("TypeB", context =>
            {
                var routeBuilder = context.RouteBuilder;

                // TypeB route: Batch and aggregate records
                routeBuilder.AddBatch<EnrichedRecord>(
                    "batcher",
                    maxBatchSize: batchSize,
                    windowPeriod: TimeSpan.FromMilliseconds(100))
                    .AsEntry()
                    .AddTransform<AggregatedBatch>("aggregator", sp =>
                        ActivatorUtilities.CreateInstance<AggregationTransformer>(sp),
                        new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
                    .AddProcessor("aggregation-writer", sp =>
                        new AggregationWriter(),
                        blockOptions);

                return routeBuilder;
            })
            .RegisterRoute("TypeC", context =>
            {
                var routeBuilder = context.RouteBuilder;

                // TypeC route: Store directly for analysis
                routeBuilder.AddProcessor<EnrichedRecord>("category-writer", sp =>
                    new CategoryWriter(context.RouteName),
                    blockOptions)
                    .AsEntry();

                return routeBuilder;
            });

        return builder;
    }

    // Data models
    public record RawRecord(int Id, string Data, DateTime Timestamp);
    public record ValidatedRecord(int Id, string Data, DateTime Timestamp, bool IsValid);
    public record EnrichedRecord(int Id, string Data, DateTime Timestamp, bool IsValid, string Category, decimal Value);
    public record ProcessedRecord(int Id, string Category, decimal Value, DateTime ProcessedAt);
    public record AggregatedBatch(string Category, int Count, decimal TotalValue, DateTime CreatedAt);

    // Producers
    private class DataSourceProducer : IStreamProducer<RawRecord>
    {
        private readonly int _count;

        public DataSourceProducer(int count)
        {
            _count = count;
        }

        public async IAsyncEnumerable<RawRecord> ProduceAsync(
            IDataFlowContext context,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            for (var i = 0; i < _count; i++)
            {
                cancellation.ThrowIfCancellationRequested();

                yield return new RawRecord(
                    i,
                    $"Data_{i}_{Guid.NewGuid():N}",
                    DateTime.UtcNow
                );

                // Simulate some source latency
                if (i % 100 == 0)
                {
                    await Task.Delay(1, cancellation);
                }
            }
        }
    }

    // Transformers
    private class ValidationTransformer : IStreamTransformer<RawRecord, ValidatedRecord>
    {
        public async IAsyncEnumerable<ValidatedRecord> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<RawRecord> input,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate validation logic
                var isValid = !string.IsNullOrEmpty(record.Data) && record.Id >= 0;

                yield return new ValidatedRecord(
                    record.Id,
                    record.Data,
                    record.Timestamp,
                    isValid
                );
            }
        }
    }

    private class EnrichmentTransformer : IStreamTransformer<ValidatedRecord, EnrichedRecord>
    {
        public async IAsyncEnumerable<EnrichedRecord> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<ValidatedRecord> input,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate enrichment with external data lookup
                await Task.Delay(1, cancellationToken);

                var category = (record.Id % 3) switch
                {
                    0 => "TypeA",
                    1 => "TypeB",
                    _ => "TypeC"
                };

                var value = (decimal)(record.Id % 1000) / 10m;

                yield return new EnrichedRecord(
                    record.Id,
                    record.Data,
                    record.Timestamp,
                    record.IsValid,
                    category,
                    value
                );
            }
        }
    }

    private class RecordProcessor : IStreamTransformer<EnrichedRecord, ProcessedRecord>
    {
        public async IAsyncEnumerable<ProcessedRecord> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                yield return new ProcessedRecord(
                    record.Id,
                    record.Category,
                    record.Value,
                    DateTime.UtcNow
                );
            }
        }
    }

    private class AggregationTransformer : IStreamTransformer<EnrichedRecord[], AggregatedBatch>
    {
        public async IAsyncEnumerable<AggregatedBatch> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord[]> input,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var batch in input.WithCancellation(cancellationToken))
            {
                // Group by category and aggregate
                var grouped = batch.GroupBy(r => r.Category);

                foreach (var group in grouped)
                {
                    yield return new AggregatedBatch(
                        group.Key,
                        group.Count(),
                        group.Sum(r => r.Value),
                        DateTime.UtcNow
                    );
                }
            }
        }
    }

    // Processors
    private class RecordWriter : IStreamProcessor<ProcessedRecord>
    {

        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<ProcessedRecord> input,
            CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate async database write operation
                await Task.Delay(2, cancellationToken);
            }
        }
    }

    private class AggregationWriter : IStreamProcessor<AggregatedBatch>
    {
        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<AggregatedBatch> input,
            CancellationToken cancellationToken)
        {
            await foreach (var batch in input.WithCancellation(cancellationToken))
            {
                // Simulate async batch write to database
                await Task.Delay(5, cancellationToken);
            }
        }
    }

    private class CategoryWriter : IStreamProcessor<EnrichedRecord>
    {
        private readonly string _category;

        public CategoryWriter(string category)
        {
            _category = category;
        }

        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord> input,
            CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate async categorized write to database
                await Task.Delay(3, cancellationToken);
            }
        }
    }

    // Broadcast path processors
    private class MetricsCollectorProcessor : IStreamProcessor<EnrichedRecord>
    {
        private static long _recordCount = 0;
        private static long _totalValueCents = 0;

        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord> input,
            CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate lightweight metrics collection
                Interlocked.Increment(ref _recordCount);
                Interlocked.Add(ref _totalValueCents, (long)(record.Value * 100));
            }
        }
    }

    private class AuditLoggerProcessor : IStreamProcessor<EnrichedRecord>
    {
        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord> input,
            CancellationToken cancellationToken)
        {
            await foreach (var record in input.WithCancellation(cancellationToken))
            {
                // Simulate audit logging (e.g., to external system)
                // In a real scenario, this would write to a log file or send to an audit service
                await Task.CompletedTask;
            }
        }
    }
}
