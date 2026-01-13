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
using DataFlow.POC.Tests.TestHelpers;
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
    /// Uses BlockHelpers for modern DI patterns and multiple concurrent actors for scalability.
    /// </summary>
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4)
    {
        // Create graph builder using modern pattern
        var builder = GraphHelpers.CreateGraphBuilder("SimpleEtlBenchmark-POC", serviceProvider);

        // Source: Generate raw data records using BlockHelpers.CreateProducer
        var dataSource = BlockHelpers.CreateProducer("data-source",
            ctx => ProduceRawRecords(recordCount, ctx.CancellationToken));

        // Transform: Parse and validate records - use multiple instances for concurrency
        var validatorServices = new ServiceCollection();
        validatorServices.AddScoped<ValidatorActor>();
        var validatorServiceProvider = validatorServices.BuildServiceProvider();
        var validatorScopeFactory = validatorServiceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var validators = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            validators.Add(BlockHelpers.CreateActor<RawRecord, ValidatedRecord, ValidatorActor>(
                $"validator-{i}", validatorScopeFactory));
        }

        // Transform: Enrich with additional data - use multiple instances for concurrency
        var enricherServices = new ServiceCollection();
        enricherServices.AddScoped<EnricherActor>();
        var enricherServiceProvider = enricherServices.BuildServiceProvider();
        var enricherScopeFactory = enricherServiceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var enrichers = new List<IBlock>();
        for (int i = 0; i < maxConcurrency; i++)
        {
            enrichers.Add(BlockHelpers.CreateActor<ValidatedRecord, EnrichedRecord, EnricherActor>(
                $"enricher-{i}", enricherScopeFactory));
        }

        // Terminal: Collect all enriched records
        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<CollectorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        var collectorScopeFactory = collectorServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var collector = BlockHelpers.CreateActor<EnrichedRecord, object, CollectorActor>(
            "collector", collectorScopeFactory);

        // Add all blocks to the graph
        builder.AddBlock(dataSource);
        foreach (var validator in validators)
            builder.AddBlock(validator);
        foreach (var enricher in enrichers)
            builder.AddBlock(enricher);
        builder.AddBlock(collector);

        // Connect blocks
        // Source to validators - fan out to multiple validators (broadcast pattern)
        foreach (var validator in validators)
        {
            builder.Connect(dataSource, validator);
        }

        // Validators to enrichers - connect each validator to each enricher (broadcast pattern)
        foreach (var validator in validators)
        {
            foreach (var enricher in enrichers)
            {
                builder.Connect(validator, enricher);
            }
        }

        // Enrichers to collector - all enrichers write to collector
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, collector);
        }

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
