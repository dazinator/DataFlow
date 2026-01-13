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
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Simplified POC ETL benchmark focusing on datasource → validators → enrichers.
/// This removes routing, broadcasting, and batching to isolate the core concurrency scaling issue.
/// Updated to use modern DI patterns with BlockHelpers and GraphHelpers.
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
    /// Configures a simplified ETL dataflow: DataSource → Validators → Enrichers → Collector
    /// Uses modern DI patterns with AddDataFlows to register blocks and graphs.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="recordCount">Number of records to process</param>
    /// <param name="maxConcurrency">Maximum concurrent actors</param>
    /// <param name="graphName">Name for the registered graph (default: "simple-etl")</param>
    public static void ConfigureDataFlow(
        IServiceCollection services,
        int recordCount,
        int maxConcurrency = 4,
        string graphName = "simple-etl")
    {
        // Register actors in main service collection
        services.AddScoped<ValidatorActor>();
        services.AddScoped<EnricherActor>();
        services.AddScoped<CollectorActor>();

        // Configure dataflow using AddDataFlows pattern
        services.AddDataFlows(graphName, df =>
        {
            // Register source block
            df.AddScopedBlock("data-source", sp => 
                BlockHelpers.CreateProducer("data-source",
                    ctx => ProduceRawRecords(recordCount, ctx.CancellationToken)));

            // Register validator blocks
            for (int i = 0; i < maxConcurrency; i++)
            {
                var index = i; // Capture for closure
                var validatorName = $"validator-{index}";
                df.AddScopedBlock(validatorName, sp =>
                {
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return BlockHelpers.CreateActor<RawRecord, ValidatedRecord, ValidatorActor>(
                        $"validator-{index}", scopeFactory);
                });
            }

            // Register enricher blocks
            for (int i = 0; i < maxConcurrency; i++)
            {
                var index = i; // Capture for closure
                var enricherName = $"enricher-{index}";
                df.AddScopedBlock(enricherName, sp =>
                {
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return BlockHelpers.CreateActor<ValidatedRecord, EnrichedRecord, EnricherActor>(
                        $"enricher-{index}", scopeFactory);
                });
            }

            // Register collector block
            df.AddScopedBlock("collector", sp =>
            {
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return BlockHelpers.CreateActor<EnrichedRecord, object, CollectorActor>(
                    "collector", scopeFactory);
            });

            // Register graph with connections
            df.AddGraph("graph", g =>
            {
                // Use all registered blocks
                g.UseBlock("data-source");
                
                // Add validators and connect to source
                for (int i = 0; i < maxConcurrency; i++)
                {
                    var validatorName = $"validator-{i}";
                    g.UseBlock(validatorName);
                    g.Connect("data-source", validatorName);
                }

                // Add enrichers with round-robin connections from validators
                for (int i = 0; i < maxConcurrency; i++)
                {
                    var enricherName = $"enricher-{i}";
                    g.UseBlock(enricherName);
                    
                    // Round-robin: each validator connects to one enricher
                    var validatorIndex = i % maxConcurrency;
                    g.Connect($"validator-{validatorIndex}", enricherName);
                }

                // Add collector and connect all enrichers to it
                g.UseBlock("collector");
                for (int i = 0; i < maxConcurrency; i++)
                {
                    g.Connect($"enricher-{i}", "collector");
                }
            });
        });
    }

    /// <summary>
    /// Builds a simplified ETL dataflow: DataSource → Validators → Enrichers → Collector
    /// Legacy method for backward compatibility - delegates to ConfigureDataFlow.
    /// </summary>
    [Obsolete("Use ConfigureDataFlow instead to follow modern DI patterns. This method creates a separate service provider.")]
    public static DataFlowGraph BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4)
    {
        var services = new ServiceCollection();
        ConfigureDataFlow(services, recordCount, maxConcurrency);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<DataFlowGraph>("simple-etl:graph");
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
