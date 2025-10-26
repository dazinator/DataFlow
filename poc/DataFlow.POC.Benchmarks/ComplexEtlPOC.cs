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
    /// Builds the complete ETL dataflow using POC DataFlowGraphBuilder.
    /// Uses POC architecture pattern: concurrency via multiple block instances + CompetingEdgeStrategy.
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

        // Buffer: Fan out from single source to multiple validators (competing consumers)
        var sourceBuffer = builder.Buffer<RawRecord>(capacity: 100, name: "source-buffer");

        // Transform: Parse and validate records - use multiple instances for concurrency
        var validators = new List<TransformerBlock<RawRecord, ValidatedRecord>>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            validators.Add(new TransformerBlock<RawRecord, ValidatedRecord>($"validator-{i}",
                (record, ctx) => ValidateRecord(record)));
        }

        // Buffer: Merge validator outputs into single competing channel for enrichers
        var validatorBuffer = builder.Buffer<ValidatedRecord>(capacity: 100, name: "validator-buffer");

        // Transform: Enrich with additional data - use multiple instances for concurrency
        var enrichers = new List<TransformerBlock<ValidatedRecord, EnrichedRecord>>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            enrichers.Add(new TransformerBlock<ValidatedRecord, EnrichedRecord>($"enricher-{i}",
                (record, ctx) => EnrichRecord(record, ctx.CancellationToken)));
        }

        // Buffer: Merge enricher outputs into single channel before broadcast
        var enricherBuffer = builder.Buffer<EnrichedRecord>(capacity: 100, name: "enricher-buffer");

        // Broadcast: Fan out enriched records for parallel processing
        var broadcast = new BroadcastBlock<EnrichedRecord>("broadcast");

        // Broadcast Fan-out Path 1: Metrics collector
        var metricsCollector = new ProcessorBlock<EnrichedRecord>("metrics-collector",
            (record, ctx) => CollectMetrics(record));

        // Broadcast Fan-out Path 2: Audit logger
        var auditLogger = new ProcessorBlock<EnrichedRecord>("audit-logger",
            (record, ctx) => LogAudit(record));

        // Routing: Route enriched records by category
        var router = new RouterBlock<EnrichedRecord>("router", record => record.Category);

        // TypeA route: Process individual records - use multiple instances for concurrency
        var typeAFilter = new RouteFilterBlock<EnrichedRecord>("typeA-filter", "TypeA");
        var typeAProcessors = new List<TransformerBlock<EnrichedRecord, ProcessedRecord>>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAProcessors.Add(new TransformerBlock<EnrichedRecord, ProcessedRecord>($"typeA-processor-{i}",
                (record, ctx) => ProcessRecord(record)));
        }
        // Buffer: Merge processor outputs into single competing channel for writers
        var typeAProcessorBuffer = builder.Buffer<ProcessedRecord>(capacity: 100, name: "typeA-processor-buffer");
        
        var typeAWriters = new List<ProcessorBlock<ProcessedRecord>>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAWriters.Add(new ProcessorBlock<ProcessedRecord>($"typeA-writer-{i}",
                (record, ctx) => WriteRecord(record, ctx.CancellationToken)));
        }

        // TypeB route: Batch and aggregate records
        var typeBFilter = new RouteFilterBlock<EnrichedRecord>("typeB-filter", "TypeB");
        var typeBBatcher = new BatchBlock<EnrichedRecord>("typeB-batcher", batchSize, TimeSpan.FromMilliseconds(100));
        var typeBAggregator = new TransformerBlock<EnrichedRecord[], AggregatedBatch>("typeB-aggregator",
            (batch, ctx) => AggregateRecords(batch));
        var typeBWriter = new ProcessorBlock<AggregatedBatch>("typeB-writer",
            (batch, ctx) => WriteAggregation(batch, ctx.CancellationToken));

        // TypeC route: Store directly
        var typeCFilter = new RouteFilterBlock<EnrichedRecord>("typeC-filter", "TypeC");
        var typeCWriter = new ProcessorBlock<EnrichedRecord>("typeC-writer",
            (record, ctx) => WriteCategoryRecord(record, "TypeC", ctx.CancellationToken));

        // Add all blocks to the graph
        builder.AddBlock(dataSource);
        
        foreach (var validator in validators)
            builder.AddBlock(validator);
        
        foreach (var enricher in enrichers)
            builder.AddBlock(enricher);
        
        builder
            .AddBlock(broadcast)
            .AddBlock(metricsCollector)
            .AddBlock(auditLogger)
            .AddBlock(router)
            // TypeA route
            .AddBlock(typeAFilter);
        
        foreach (var processor in typeAProcessors)
            builder.AddBlock(processor);
        
        foreach (var writer in typeAWriters)
            builder.AddBlock(writer);
        
        // TypeB route
        builder
            .AddBlock(typeBFilter)
            .AddBlock(typeBBatcher)
            .AddBlock(typeBAggregator)
            .AddBlock(typeBWriter)
            // TypeC route
            .AddBlock(typeCFilter)
            .AddBlock(typeCWriter);

        // Connect blocks using CompetingEdgeStrategy for concurrent processing
        // Source to buffer - single producer to shared buffer
        builder.Connect(dataSource, sourceBuffer);

        // Buffer to validators - all validators compete from shared buffer
        foreach (var validator in validators)
        {
            builder.Connect(sourceBuffer, validator);
        }

        // Validators to buffer - all validators write to shared buffer
        foreach (var validator in validators)
        {
            builder.Connect(validator, validatorBuffer);
        }

        // Buffer to enrichers - all enrichers compete from shared buffer
        foreach (var enricher in enrichers)
        {
            builder.Connect(validatorBuffer, enricher);
        }

        // Enrichers to buffer - all enrichers write to shared buffer before broadcast
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, enricherBuffer);
        }

        // Buffer to broadcast - single input stream for broadcast
        builder.Connect(enricherBuffer, broadcast);

        // Broadcast to multiple paths (broadcast strategy - all consumers get all items)
        builder
            .Connect(broadcast, metricsCollector, 100)
            .Connect(broadcast, auditLogger, 100)
            .Connect(broadcast, router, 100);

        // Router to type-specific routes
        builder
            .Connect(router, typeAFilter)
            .Connect(router, typeBFilter)
            .Connect(router, typeCFilter);

        // TypeA route: filter -> processors (competing) -> buffer -> writers (competing)
        builder.ConnectCompeting(typeAFilter, typeAProcessors.Cast<IBlock>().ToList(), 100);

        // Processors to buffer - all processors write to shared buffer
        foreach (var processor in typeAProcessors)
        {
            builder.Connect(processor, typeAProcessorBuffer);
        }

        // Buffer to writers - all writers compete from shared buffer
        foreach (var writer in typeAWriters)
        {
            builder.Connect(typeAProcessorBuffer, writer);
        }

        // TypeB route connections
        builder
            .Connect(typeBFilter, typeBBatcher)
            .Connect(typeBBatcher, typeBAggregator)
            .Connect(typeBAggregator, typeBWriter);

        // TypeC route connections
        builder.Connect(typeCFilter, typeCWriter);

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
