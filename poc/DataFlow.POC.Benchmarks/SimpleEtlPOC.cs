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

/// <summary>
/// Simplified POC ETL benchmark focusing on datasource → validators → enrichers.
/// This removes routing, broadcasting, and batching to isolate the core concurrency scaling issue.
/// CONVERTED: Now uses epoch-based architecture with EpochBufferBlock and EpochActorBlock.
/// </summary>
public static class SimpleEtlPOC
{
    /// <summary>
    /// Actor that validates raw records.
    /// </summary>
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

    /// <summary>
    /// Actor that enriches validated records.
    /// </summary>
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

    /// <summary>
    /// Actor that collects enriched records (no-op terminal).
    /// </summary>
    private class CollectorActor : IStreamActor<EnrichedRecord, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<EnrichedRecord> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var record in input.WithCancellation(context.CancellationToken))
            {
                // No-op collection
            }
            yield break;
        }
    }

    /// <summary>
    /// Builds a simplified ETL dataflow: DataSource → Validators → Enrichers → Collector
    /// Uses EpochBufferBlock at each stage to ensure proper fan-out/fan-in patterns.
    /// CONVERTED: Now uses full epoch-based architecture.
    /// </summary>
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4)
    {
        var builder = GraphHelpers.CreateGraphBuilder("SimpleEtlBenchmark-POC");

        // Source: Generate raw data records using deprecated ProducerBlock
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

        // Buffer: Merge enricher outputs into single channel for collector
        var enricherBuffer = new EpochBufferBlock<EnrichedRecord>(
            new BlockContext("enricher-buffer"),
            new BufferConfiguration(100));

        // Terminal: Collect all enriched records
        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<CollectorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        var collector = new EpochActorBlock<EnrichedRecord, object, CollectorActor>(
            new BlockContext("collector"),
            collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Add all blocks to the graph
        builder.AddBlock(dataSource);
        builder.AddBlock(segmenter);
        builder.AddBlock(sourceBuffer);
        foreach (var validator in validators)
            builder.AddBlock(validator);
        builder.AddBlock(validatorBuffer);
        foreach (var enricher in enrichers)
            builder.AddBlock(enricher);
        builder.AddBlock(enricherBuffer);
        builder.AddBlock(collector);

        // Connect blocks
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

        // Enrichers to buffer - all enrichers write to shared buffer
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, enricherBuffer);
        }

        // Buffer to collector
        builder.Connect(enricherBuffer, collector);

        return builder.Build();
    }

    // Data models (same as ComplexEtlPOC)
    public record RawRecord(int Id, string Data, DateTime Timestamp);
    public record ValidatedRecord(int Id, string Data, DateTime Timestamp, bool IsValid);
    public record EnrichedRecord(int Id, string Data, DateTime Timestamp, bool IsValid, string Category, decimal Value);

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

    // Transformers (same as ComplexEtlPOC but without category-specific logic)
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
        // Simulate enrichment with external data lookup (1ms delay)
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
}
