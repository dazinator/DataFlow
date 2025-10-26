namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;

/// <summary>
/// POC implementation of complex ETL dataflow for benchmarking.
/// This mirrors the non-POC ComplexEtlDataFlow structure for fair comparison.
/// </summary>
public static class ComplexEtlPOC
{
    /// <summary>
    /// Builds the complete ETL dataflow using POC DataFlowGraphBuilder
    /// </summary>
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4,
        int batchSize = 100)
    {
        var builder = new DataFlowGraphBuilder("ComplexEtlBenchmark-POC");

        // Source: Generate raw data records
        var dataSource = new ProducerBlock<RawRecord>("data-source",
            ctx => ProduceRawRecords(recordCount, ctx.CancellationToken));

        // Transform: Parse and validate records
        var validator = new TransformerBlock<RawRecord, ValidatedRecord>("validator",
            (record, ctx) =>
            {
                return ValidateRecord(record);
            });

        // Transform: Enrich with additional data
        var enricher = new TransformerBlock<ValidatedRecord, EnrichedRecord>("enricher",
            (record, ctx) =>
            {
                return EnrichRecord(record, ctx.CancellationToken);
            });

        // Broadcast: Fan out enriched records for parallel processing
        var broadcast = new BroadcastBlock<EnrichedRecord>("broadcast");

        // Broadcast Fan-out Path 1: Metrics collector
        var metricsCollector = new ProcessorBlock<EnrichedRecord>("metrics-collector",
            (record, ctx) =>
            {
                return CollectMetrics(record);
            });

        // Broadcast Fan-out Path 2: Audit logger
        var auditLogger = new ProcessorBlock<EnrichedRecord>("audit-logger",
            (record, ctx) =>
            {
                return LogAudit(record);
            });

        // Routing: Route enriched records by category
        var router = new RouterBlock<EnrichedRecord>("router", record => record.Category);

        // TypeA route: Process individual records
        var typeAFilter = new RouteFilterBlock<EnrichedRecord>("typeA-filter", "TypeA");
        var typeAProcessor = new TransformerBlock<EnrichedRecord, ProcessedRecord>("typeA-processor",
            (record, ctx) =>
            {
                return ProcessRecord(record);
            });
        var typeAWriter = new ProcessorBlock<ProcessedRecord>("typeA-writer",
            (record, ctx) =>
            {
                return WriteRecord(record, ctx.CancellationToken);
            });

        // TypeB route: Batch and aggregate records
        var typeBFilter = new RouteFilterBlock<EnrichedRecord>("typeB-filter", "TypeB");
        var typeBBatcher = new BatchBlock<EnrichedRecord>("typeB-batcher", batchSize, TimeSpan.FromMilliseconds(100));
        var typeBAggregator = new TransformerBlock<EnrichedRecord[], AggregatedBatch>("typeB-aggregator",
            (batch, ctx) =>
            {
                return AggregateRecords(batch);
            });
        var typeBWriter = new ProcessorBlock<AggregatedBatch>("typeB-writer",
            (batch, ctx) =>
            {
                return WriteAggregation(batch, ctx.CancellationToken);
            });

        // TypeC route: Store directly
        var typeCFilter = new RouteFilterBlock<EnrichedRecord>("typeC-filter", "TypeC");
        var typeCWriter = new ProcessorBlock<EnrichedRecord>("typeC-writer",
            (record, ctx) =>
            {
                return WriteCategoryRecord(record, "TypeC", ctx.CancellationToken);
            });

        // Build the graph
        builder
            .AddBlock(dataSource)
            .AddBlock(validator)
            .AddBlock(enricher)
            .AddBlock(broadcast)
            .AddBlock(metricsCollector)
            .AddBlock(auditLogger)
            .AddBlock(router)
            // TypeA route
            .AddBlock(typeAFilter)
            .AddBlock(typeAProcessor)
            .AddBlock(typeAWriter)
            // TypeB route
            .AddBlock(typeBFilter)
            .AddBlock(typeBBatcher)
            .AddBlock(typeBAggregator)
            .AddBlock(typeBWriter)
            // TypeC route
            .AddBlock(typeCFilter)
            .AddBlock(typeCWriter);

        // Connect blocks
        builder
            .Connect(dataSource, validator)
            .Connect(validator, enricher)
            .Connect(enricher, broadcast);

        // Broadcast to multiple paths
        builder
            .AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 100))
            .AddEdge(new Edge(broadcast, auditLogger, BufferMode.Bounded, 100))
            .AddEdge(new Edge(broadcast, router, BufferMode.Bounded, 100));

        // Router to type-specific routes
        builder
            .Connect(router, typeAFilter)
            .Connect(router, typeBFilter)
            .Connect(router, typeCFilter);

        // TypeA route connections
        builder
            .Connect(typeAFilter, typeAProcessor)
            .Connect(typeAProcessor, typeAWriter);

        // TypeB route connections
        builder
            .Connect(typeBFilter, typeBBatcher)
            .Connect(typeBBatcher, typeBAggregator)
            .Connect(typeBAggregator, typeBWriter);

        // TypeC route connections
        builder
            .Connect(typeCFilter, typeCWriter);

        return builder.Build();
    }

    // Data models (matching non-POC)
    public record RawRecord(int Id, string Data, DateTime Timestamp);
    public record ValidatedRecord(int Id, string Data, DateTime Timestamp, bool IsValid);
    public record EnrichedRecord(int Id, string Data, DateTime Timestamp, bool IsValid, string Category, decimal Value);
    public record ProcessedRecord(int Id, string Category, decimal Value, DateTime ProcessedAt);
    public record AggregatedBatch(string Category, int Count, decimal TotalValue, DateTime CreatedAt);

    // Producer
    private static async IAsyncEnumerable<RawRecord> ProduceRawRecords(
        int count,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        for (var i = 0; i < count; i++)
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

    // Transformers
    private static async IAsyncEnumerable<ValidatedRecord> ValidateRecord(RawRecord record)
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

    private static async IAsyncEnumerable<EnrichedRecord> EnrichRecord(
        ValidatedRecord record,
        CancellationToken cancellation)
    {
        // Simulate enrichment with external data lookup
        await Task.Delay(1, cancellation);

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

    private static async IAsyncEnumerable<ProcessedRecord> ProcessRecord(EnrichedRecord record)
    {
        yield return new ProcessedRecord(
            record.Id,
            record.Category,
            record.Value,
            DateTime.UtcNow
        );
    }

    private static async IAsyncEnumerable<AggregatedBatch> AggregateRecords(EnrichedRecord[] batch)
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

    // Processors
    private static Task CollectMetrics(EnrichedRecord record)
    {
        // Simulate lightweight metrics collection
        return Task.CompletedTask;
    }

    private static Task LogAudit(EnrichedRecord record)
    {
        // Simulate audit logging
        return Task.CompletedTask;
    }

    private static async Task WriteRecord(ProcessedRecord record, CancellationToken cancellation)
    {
        // Simulate async database write operation
        await Task.Delay(2, cancellation);
    }

    private static async Task WriteAggregation(AggregatedBatch batch, CancellationToken cancellation)
    {
        // Simulate async batch write to database
        await Task.Delay(5, cancellation);
    }

    private static async Task WriteCategoryRecord(EnrichedRecord record, string category, CancellationToken cancellation)
    {
        // Simulate async categorized write to database
        await Task.Delay(3, cancellation);
    }
}
