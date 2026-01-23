namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// POC implementation of complex ETL dataflow for benchmarking.
/// This mirrors the non-POC ComplexEtlDataFlow structure for fair comparison.
/// </summary>
public static class ComplexEtlPOC
{
    // Actors for transformation and processing
    private class ValidatorActor : IStreamActor<RawRecord, ValidatedRecord>
    {
        public async IAsyncEnumerable<ValidatedRecord> RunAsync(
            IAsyncEnumerable<RawRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await foreach (var validated in ValidateRecord(record))
                {
                    yield return validated;
                }
            }
        }
    }

    private class EnricherActor : IStreamActor<ValidatedRecord, EnrichedRecord>
    {
        public async IAsyncEnumerable<EnrichedRecord> RunAsync(
            IAsyncEnumerable<ValidatedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await foreach (var enriched in EnrichRecord(record, context.CancellationToken))
                {
                    yield return enriched;
                }
            }
        }
    }

    private class MetricsCollectorActor : IStreamActor<EnrichedRecord, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await CollectMetrics(record);
            }
            yield break;
        }
    }

    private class AuditLoggerActor : IStreamActor<EnrichedRecord, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await LogAudit(record);
            }
            yield break;
        }
    }

    private class TypeAProcessorActor : IStreamActor<EnrichedRecord, ProcessedRecord>
    {
        public async IAsyncEnumerable<ProcessedRecord> RunAsync(
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await foreach (var processed in ProcessRecord(record))
                {
                    yield return processed;
                }
            }
        }
    }

    private class TypeAWriterActor : IStreamActor<ProcessedRecord, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<ProcessedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await WriteRecord(record, context.CancellationToken);
            }
            yield break;
        }
    }

    private class TypeBAggregatorActor : IStreamActor<EnrichedRecord[], AggregatedBatch>
    {
        public async IAsyncEnumerable<AggregatedBatch> RunAsync(
            IAsyncEnumerable<EnrichedRecord[]> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                await foreach (var aggregated in AggregateRecords(batch))
                {
                    yield return aggregated;
                }
            }
        }
    }

    private class TypeBWriterActor : IStreamActor<AggregatedBatch, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<AggregatedBatch> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                await WriteAggregation(batch, context.CancellationToken);
            }
            yield break;
        }
    }

    private class TypeCWriterActor : IStreamActor<EnrichedRecord, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                await WriteCategoryRecord(record, "TypeC", context.CancellationToken);
            }
            yield break;
        }
    }

    /// <summary>
    /// Builds the complete ETL dataflow using POC DataFlowGraphBuilder.
    /// 
    /// ⚠️ OBSOLETE: This method used EpochSegmenterBlock which has been removed.
    /// Epoch segmentation is now done at graph level via ConfigureEpochs() API.
    /// To update, use graph.ConfigureEpochs() instead of EpochSegmenterBlock.
    /// </summary>
    [Obsolete("This method uses removed EpochSegmenterBlock. Use ConfigureEpochs() for graph-level epoch configuration.")]
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4,
        int batchSize = 100)
    {
        throw new NotSupportedException(
            "BuildDataFlow is obsolete. EpochSegmenterBlock has been removed. " +
            "Use graph-level ConfigureEpochs() API for epoch segmentation.");
        
        /* OBSOLETE CODE - kept for reference
        var builder = GraphHelpers.CreateGraphBuilder("ComplexEtlBenchmark-POC");

        // Source: Generate raw data records using deprecated PlainSourceBlock
        var dataSource = new ProducerBlock<RawRecord>("data-source",
            ctx => ProduceRawRecords(recordCount, ctx.CancellationToken));

        // Segmenter: Convert plain stream to epoch streams
        var segmenter = new EpochSegmenterBlock<RawRecord>(
            new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(100, "source"));

        // Buffer: Fan out from single source to multiple validators (competing consumers)
        var sourceBuffer = new EpochBufferBlock<RawRecord>(
            new BlockContext("source-buffer"),
            new BufferConfiguration(100));

        // Transform: Parse and validate records - use multiple instances for concurrency
        var validatorServices = new ServiceCollection();
        validatorServices.AddScoped<ValidatorActor>();
        var validatorServiceProvider = validatorServices.BuildServiceProvider();
        
        var validators = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            validators.Add(new EpochActorBlock<RawRecord, ValidatedRecord, ValidatorActor>(
                new BlockContext($"validator-{i}"),
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Buffer: Merge validator outputs into single competing channel for enrichers
        var validatorBuffer = new EpochBufferBlock<ValidatedRecord>(
            new BlockContext("validator-buffer"),
            new BufferConfiguration(100));

        // Transform: Enrich with additional data - use multiple instances for concurrency
        var enricherServices = new ServiceCollection();
        enricherServices.AddScoped<EnricherActor>();
        var enricherServiceProvider = enricherServices.BuildServiceProvider();
        
        var enrichers = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            enrichers.Add(new EpochActorBlock<ValidatedRecord, EnrichedRecord, EnricherActor>(
                new BlockContext($"enricher-{i}"),
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Buffer: Merge enricher outputs into single channel before broadcast
        var enricherBuffer = new EpochBufferBlock<EnrichedRecord>(
            new BlockContext("enricher-buffer"),
            new BufferConfiguration(100));

        // Broadcast: Fan out enriched records for parallel processing
        var broadcast = new DataFlow.POC.Blocks.BroadcastBlock<EnrichedRecord>(new BlockContext("broadcast"));

        // Broadcast Fan-out Path 1: Metrics collector
        var metricsServices = new ServiceCollection();
        metricsServices.AddScoped<MetricsCollectorActor>();
        var metricsServiceProvider = metricsServices.BuildServiceProvider();
        var metricsCollector = new EpochActorBlock<EnrichedRecord, object, MetricsCollectorActor>(
            new BlockContext("metrics-collector"),
            metricsServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Broadcast Fan-out Path 2: Audit logger
        var auditServices = new ServiceCollection();
        auditServices.AddScoped<AuditLoggerActor>();
        var auditServiceProvider = auditServices.BuildServiceProvider();
        var auditLogger = new EpochActorBlock<EnrichedRecord, object, AuditLoggerActor>(
            new BlockContext("audit-logger"),
            auditServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Routing: Route enriched records by category (using deprecated plain-type router for now)
        var router = new RouterBlock<EnrichedRecord>("router", record => record.Category);

        // TypeA route: Process individual records - use multiple instances for concurrency
        var typeAFilter = new RouteFilterBlock<EnrichedRecord>("typeA-filter", "TypeA");
        var typeAProcessorServices = new ServiceCollection();
        typeAProcessorServices.AddScoped<TypeAProcessorActor>();
        var typeAProcessorServiceProvider = typeAProcessorServices.BuildServiceProvider();
        
        var typeAProcessors = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAProcessors.Add(new EpochActorBlock<EnrichedRecord, ProcessedRecord, TypeAProcessorActor>(
                new BlockContext($"typeA-processor-{i}"),
                typeAProcessorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }
        // Buffer: Merge processor outputs into single competing channel for writers
        var typeAProcessorBuffer = new EpochBufferBlock<ProcessedRecord>(
            new BlockContext("typeA-processor-buffer"),
            new BufferConfiguration(100));
        
        var typeAWriterServices = new ServiceCollection();
        typeAWriterServices.AddScoped<TypeAWriterActor>();
        var typeAWriterServiceProvider = typeAWriterServices.BuildServiceProvider();
        
        var typeAWriters = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAWriters.Add(new EpochActorBlock<ProcessedRecord, object, TypeAWriterActor>(
                new BlockContext($"typeA-writer-{i}"),
                typeAWriterServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // TypeB route: Batch and aggregate records
        var typeBFilter = new RouteFilterBlock<EnrichedRecord>("typeB-filter", "TypeB");
        var typeBFilterBuffer = new EpochBufferBlock<EnrichedRecord>(
            new BlockContext("typeB-filter-buffer"),
            new BufferConfiguration(100));
        var typeBBatcher = new EpochBatchBlock<EnrichedRecord>(
            new BlockContext("typeB-batcher"),
            batchSize,
            TimeSpan.FromMilliseconds(100));
        
        var typeBServices = new ServiceCollection();
        typeBServices.AddScoped<TypeBAggregatorActor>();
        typeBServices.AddScoped<TypeBWriterActor>();
        var typeBServiceProvider = typeBServices.BuildServiceProvider();
        
        var typeBAggregator = new EpochActorBlock<EnrichedRecord[], AggregatedBatch, TypeBAggregatorActor>(
            new BlockContext("typeB-aggregator"),
            typeBServiceProvider.GetRequiredService<IServiceScopeFactory>());
        var typeBWriter = new EpochActorBlock<AggregatedBatch, object, TypeBWriterActor>(
            new BlockContext("typeB-writer"),
            typeBServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // TypeC route: Store directly - use multiple writers for concurrency
        var typeCFilter = new RouteFilterBlock<EnrichedRecord>("typeC-filter", "TypeC");
        var typeCFilterBuffer = new EpochBufferBlock<EnrichedRecord>(
            new BlockContext("typeC-filter-buffer"),
            new BufferConfiguration(100));
        var typeCWriterServices = new ServiceCollection();
        typeCWriterServices.AddScoped<TypeCWriterActor>();
        var typeCWriterServiceProvider = typeCWriterServices.BuildServiceProvider();
        
        var typeCWriters = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeCWriters.Add(new EpochActorBlock<EnrichedRecord, object, TypeCWriterActor>(
                new BlockContext($"typeC-writer-{i}"),
                typeCWriterServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Add all blocks to the graph
        builder.AddBlock(dataSource);
        builder.AddBlock(segmenter);
        builder.AddBlock(sourceBuffer);
        
        foreach (var validator in validators)
            builder.AddBlock(validator);
        
        builder.AddBlock(validatorBuffer);
        
        foreach (var enricher in enrichers)
            builder.AddBlock(enricher);
        
        builder
            .AddBlock(enricherBuffer)
            .AddBlock(broadcast)
            .AddBlock(metricsCollector)
            .AddBlock(auditLogger)
            .AddBlock(router)
            // TypeA route
            .AddBlock(typeAFilter);
        
        foreach (var processor in typeAProcessors)
            builder.AddBlock(processor);
        
        builder.AddBlock(typeAProcessorBuffer);
        
        foreach (var writer in typeAWriters)
            builder.AddBlock(writer);
        
        // TypeB route
        builder
            .AddBlock(typeBFilter)
            .AddBlock(typeBFilterBuffer)
            .AddBlock(typeBBatcher)
            .AddBlock(typeBAggregator)
            .AddBlock(typeBWriter)
            // TypeC route
            .AddBlock(typeCFilter)
            .AddBlock(typeCFilterBuffer);
        
        foreach (var writer in typeCWriters)
            builder.AddBlock(writer);

        // Connect blocks using CompetingEdgeStrategy for concurrent processing
        // Source to segmenter - convert plain to epoch streams
        builder.Connect(dataSource, segmenter);
        
        // Segmenter to buffer - single producer to shared buffer
        builder.Connect(segmenter, sourceBuffer);

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

        // Router to type-specific routes using routing strategy
        // This routes items directly to the correct filter based on route key,
        // eliminating the need for filters to iterate through all items
        builder.ConnectRouted(router, new Dictionary<string, IBlock>
        {
            ["TypeA"] = typeAFilter,
            ["TypeB"] = typeBFilter,
            ["TypeC"] = typeCFilter
        }, bufferCapacity: 100);

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

        // TypeB route connections: filter -> buffer -> batcher -> aggregator -> writer
        builder
            .Connect(typeBFilter, typeBFilterBuffer)
            .Connect(typeBFilterBuffer, typeBBatcher)
            .Connect(typeBBatcher, typeBAggregator)
            .Connect(typeBAggregator, typeBWriter);

        // TypeC route connections: filter -> buffer -> writers (competing)
        builder.Connect(typeCFilter, typeCFilterBuffer);
        
        foreach (var writer in typeCWriters)
        {
            builder.Connect(typeCFilterBuffer, writer);
        }

        return builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        */
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
        [EnumeratorCancellation] CancellationToken cancellation)
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
