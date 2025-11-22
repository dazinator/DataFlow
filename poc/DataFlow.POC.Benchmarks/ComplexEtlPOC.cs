namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

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
    /// Uses POC architecture pattern: concurrency via multiple block instances + CompetingEdgeStrategy.
    /// </summary>
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4,
        int batchSize = 100)
    {
        var builder = GraphHelpers.CreateGraphBuilder("ComplexEtlBenchmark-POC");

        // Source: Generate raw data records
        var dataSource = new ProducerBlock<RawRecord>("data-source",
            ctx => ProduceRawRecords(recordCount, ctx.CancellationToken));

        // Buffer: Fan out from single source to multiple validators (competing consumers)
        var sourceBuffer = builder.Buffer<RawRecord>(capacity: 100, name: "source-buffer");

        // Transform: Parse and validate records - use multiple instances for concurrency
        var validatorServices = new ServiceCollection();
        validatorServices.AddScoped<ValidatorActor>();
        var validatorServiceProvider = validatorServices.BuildServiceProvider();
        
        var validators = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            validators.Add(new ActorBlock<RawRecord, ValidatedRecord, ValidatorActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Buffer: Merge validator outputs into single competing channel for enrichers
        var validatorBuffer = builder.Buffer<ValidatedRecord>(capacity: 100, name: "validator-buffer");

        // Transform: Enrich with additional data - use multiple instances for concurrency
        var enricherServices = new ServiceCollection();
        enricherServices.AddScoped<EnricherActor>();
        var enricherServiceProvider = enricherServices.BuildServiceProvider();
        
        var enrichers = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            enrichers.Add(new ActorBlock<ValidatedRecord, EnrichedRecord, EnricherActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Buffer: Merge enricher outputs into single channel before broadcast
        var enricherBuffer = builder.Buffer<EnrichedRecord>(capacity: 100, name: "enricher-buffer");

        // Broadcast: Fan out enriched records for parallel processing
        var broadcast = new BroadcastBlock<EnrichedRecord>("broadcast");

        // Broadcast Fan-out Path 1: Metrics collector
        var metricsServices = new ServiceCollection();
        metricsServices.AddScoped<MetricsCollectorActor>();
        var metricsServiceProvider = metricsServices.BuildServiceProvider();
        var metricsCollector = new ActorBlock<EnrichedRecord, object, MetricsCollectorActor>(
            "metrics-collector",
            metricsServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Broadcast Fan-out Path 2: Audit logger
        var auditServices = new ServiceCollection();
        auditServices.AddScoped<AuditLoggerActor>();
        var auditServiceProvider = auditServices.BuildServiceProvider();
        var auditLogger = new ActorBlock<EnrichedRecord, object, AuditLoggerActor>(
            "audit-logger",
            auditServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Routing: Route enriched records by category
        var router = new RouterBlock<EnrichedRecord>("router", record => record.Category);

        // TypeA route: Process individual records - use multiple instances for concurrency
        var typeAFilter = new RouteFilterBlock<EnrichedRecord>("typeA-filter", "TypeA");
        var typeAProcessorServices = new ServiceCollection();
        typeAProcessorServices.AddScoped<TypeAProcessorActor>();
        var typeAProcessorServiceProvider = typeAProcessorServices.BuildServiceProvider();
        
        var typeAProcessors = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAProcessors.Add(new ActorBlock<EnrichedRecord, ProcessedRecord, TypeAProcessorActor>(
                $"typeA-processor-{i}",
                typeAProcessorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }
        // Buffer: Merge processor outputs into single competing channel for writers
        var typeAProcessorBuffer = builder.Buffer<ProcessedRecord>(capacity: 100, name: "typeA-processor-buffer");
        
        var typeAWriterServices = new ServiceCollection();
        typeAWriterServices.AddScoped<TypeAWriterActor>();
        var typeAWriterServiceProvider = typeAWriterServices.BuildServiceProvider();
        
        var typeAWriters = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeAWriters.Add(new ActorBlock<ProcessedRecord, object, TypeAWriterActor>(
                $"typeA-writer-{i}",
                typeAWriterServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // TypeB route: Batch and aggregate records
        var typeBFilter = new RouteFilterBlock<EnrichedRecord>("typeB-filter", "TypeB");
        var typeBFilterBuffer = builder.Buffer<EnrichedRecord>(capacity: 100, name: "typeB-filter-buffer");
        var typeBBatcher = new BatchBlock<EnrichedRecord>("typeB-batcher", batchSize, TimeSpan.FromMilliseconds(100));
        
        var typeBServices = new ServiceCollection();
        typeBServices.AddScoped<TypeBAggregatorActor>();
        typeBServices.AddScoped<TypeBWriterActor>();
        var typeBServiceProvider = typeBServices.BuildServiceProvider();
        
        var typeBAggregator = new ActorBlock<EnrichedRecord[], AggregatedBatch, TypeBAggregatorActor>(
            "typeB-aggregator",
            typeBServiceProvider.GetRequiredService<IServiceScopeFactory>());
        var typeBWriter = new ActorBlock<AggregatedBatch, object, TypeBWriterActor>(
            "typeB-writer",
            typeBServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // TypeC route: Store directly - use multiple writers for concurrency
        var typeCFilter = new RouteFilterBlock<EnrichedRecord>("typeC-filter", "TypeC");
        var typeCFilterBuffer = builder.Buffer<EnrichedRecord>(capacity: 100, name: "typeC-filter-buffer");
        var typeCWriterServices = new ServiceCollection();
        typeCWriterServices.AddScoped<TypeCWriterActor>();
        var typeCWriterServiceProvider = typeCWriterServices.BuildServiceProvider();
        
        var typeCWriters = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            typeCWriters.Add(new ActorBlock<EnrichedRecord, object, TypeCWriterActor>(
                $"typeC-writer-{i}",
                typeCWriterServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

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
            .AddBlock(typeCFilter);
        
        foreach (var writer in typeCWriters)
            builder.AddBlock(writer);

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
